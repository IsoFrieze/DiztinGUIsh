#!/usr/bin/env python3
"""
textpack — fixed-width game text tables <-> YAML, with byte-identical round-trip.

Part of "dizpack", the stock codec toolset that DiztinGUIsh vendors into a game repo
on export. The game repo must NEVER need Diz at build time — only Python 3 and the
packages in requirements.txt (PyYAML, for the content file).

This is a GENERIC tool (like gfxpack), not a game tool: the `text.` asset prefix
dispatches here and ALL game specifics live in the manifest — the character table it
points at, the fixed record width, the pad byte, and the named-token vocabulary. The
first codec it implements, `text.ct.mapped`, is Chrono Trigger's 8px fixed-width name
tables (item/tech names), but nothing below is CT-specific.

Design invariants (do not break — byte-identity depends on them):
  * The MANIFEST is the authority. The .tbl is an ergonomic glyph map; the YAML is dumb
    content. Neither may hide a byte: everything a byte can be is spelled out.
  * EXTRACT IS EXACT, COMPILE PADS. Extract emits every one of `record_width` bytes
    verbatim as a token, so the round-trip is trivially byte-identical. Compile right-pads
    a short authored string to width with the manifest `pad` byte and HALTS on over-width
    — it never silently truncates.
  * FAIL LOUD. A missing/duplicate record index, an over-width record, an unknown token,
    an ambiguous table — every one is a hard error, never a best-effort guess. Silent
    drift is exactly what forfeits byte-identity.
  * DAY-ONE PROPERTY. With an EMPTY .tbl and no tokens, every byte still round-trips as a
    raw `[$NN]` escape. The .tbl and tokens only make the text readable; they are never
    load-bearing for correctness.

The four escape rules (parsing a record string -> bytes):
  1. `[NAME]`  — a manifest named token (icon / control code). Resolved against the
                 manifest's `tokens` map, NEVER the .tbl.
  2. `[$NN]`   — a raw byte, two hex digits. Always available, even with an empty .tbl.
  3. `[[`      — a literal `[` glyph (only if the .tbl actually maps `[`).
  4. anything else — a single glyph, looked up in the .tbl (char -> byte).
Rendering (bytes -> string) is the inverse and is deterministic: a byte is a token if the
manifest names it, else a glyph if the .tbl maps it, else a `[$NN]` escape.

Commands:
  extract  raw bytes + tbl + tokens -> <root>/<name>.yaml + <root>/<name>.json
  compile  yaml + manifest + tbl    -> raw .bin
  verify   yaml + manifest + tbl    -> compile and assert sha256 (and optional ROM bytes)
  selftest                          -> in-memory codec assertions; needs no files (CI gate)

Example (CT item-name table: 242 records x 11 bytes):
  textpack.py extract --rom names.bin --offset 0 --length 2662 --width 11 \
      --name text/item_names --type text.ct.mapped --root assets/src \
      --tbl text/ct_8px.tbl --tokens assets/src/text/ct_8px.tokens.json --snes-addr CC0B5E
  textpack.py compile --name text/item_names --search assets/src --out build/item_names.bin
  textpack.py verify  --name text/item_names --search assets/src
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys

# --------------------------------------------------------------------------------------
# Versioning. Manifests carry {"type": ..., "ver": ...}. `ver` omitted => LATEST. When a
# codec's BYTE-LEVEL semantics change, add a version and keep the old handler so existing
# manifests keep building. An unimplemented version is a HARD ERROR — never best-effort.
# --------------------------------------------------------------------------------------
LATEST_VER = "v1"
SUPPORTED_VERS = {"v1"}
TOOL_VERSION = "textpack/1.0.0"

_HEX2 = re.compile(r"[0-9A-Fa-f]{2}")
_IDENT = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")


def die(msg: str) -> "None":
    print(f"textpack: error: {msg}", file=sys.stderr)
    raise SystemExit(2)


try:
    import yaml
except ImportError:
    die("PyYAML is required for the content file but is not installed. "
        "Run: pip install -r tools/vendor/dizpack/requirements.txt (or: pip install pyyaml)")


class _NoDupLoader(yaml.SafeLoader):
    """SafeLoader that REJECTS duplicate mapping keys. Stock PyYAML silently keeps the
    last of a duplicated key, which would let a duplicated record index slip through as a
    dense map — exactly the fail-loud violation the plan's 'halt on duplicate index' gate
    forbids. SystemExit (from die) is a BaseException, so PyYAML does not swallow it."""


def _construct_no_dup(loader, node, deep=False):
    mapping = {}
    for key_node, val_node in node.value:
        key = loader.construct_object(key_node, deep=deep)
        if key in mapping:
            die(f"duplicate key {key!r} in YAML mapping")
        mapping[key] = loader.construct_object(val_node, deep=deep)
    return mapping


_NoDupLoader.add_constructor(
    yaml.resolver.BaseResolver.DEFAULT_MAPPING_TAG, _construct_no_dup)


# ======================================================================================
# Character table (.tbl) — a pure font map: one `HEX=glyph` line per single character.
#
# Returns two maps: dec (byte -> glyph, for extract) and enc (glyph -> byte, for compile).
# `enc` MUST be injective — if two bytes rendered the same glyph, compile could not know
# which byte the author meant, so byte-identity would be lost. We reject such a table at
# load rather than silently picking one, which is the fail-loud posture in a nutshell.
# ======================================================================================
def load_tbl(path: str) -> "tuple[dict, dict]":
    dec: "dict[int, str]" = {}
    enc: "dict[str, int]" = {}
    try:
        raw_lines = open(path, encoding="utf-8").read().split("\n")
    except OSError as e:
        die(f"{path}: cannot read character table: {e}")
    for lineno, raw in enumerate(raw_lines, 1):
        line = raw.rstrip("\r")           # keep everything else, incl. a trailing space glyph
        if not line.strip() or line.lstrip()[:1] in ("#", ";"):
            continue
        if "=" not in line:
            die(f"{path}:{lineno}: no '=' in {line!r}; expected HEX=glyph")
        key, _, glyph = line.partition("=")
        key = key.strip()
        if not _HEX2.fullmatch(key):
            die(f"{path}:{lineno}: key {key!r} must be exactly two hex digits")
        b = int(key, 16)
        if len(glyph) != 1:
            die(f"{path}:{lineno}: byte ${b:02X} maps to {glyph!r}; a .tbl entry must be "
                f"exactly one glyph character (control codes/icons are manifest tokens)")
        if b in dec:
            die(f"{path}:{lineno}: byte ${b:02X} is mapped more than once")
        if glyph in enc:
            die(f"{path}:{lineno}: glyph {glyph!r} is mapped by both ${enc[glyph]:02X} and "
                f"${b:02X}; a table must be injective or compile cannot round-trip it")
        dec[b] = glyph
        enc[glyph] = b
    return dec, enc


def parse_tokens(tokmap: "dict | None", tbl_dec: "dict[int, str]",
                 where: str) -> "tuple[dict, dict]":
    """Named-token map: {NAME: '0xNN'} -> (enc: NAME->byte, dec: byte->NAME).

    Tokens and .tbl glyphs are DISJOINT sets of bytes — a byte is either a glyph or a
    token, never both, so rendering is unambiguous. Multiple names may alias one byte
    (aliases are additive: naming `$06` "pause" must never change how already-authored
    bytes parse); the FIRST-declared name is the canonical one used when rendering.
    """
    enc: "dict[str, int]" = {}
    dec: "dict[int, str]" = {}
    for name, val in (tokmap or {}).items():
        if not _IDENT.fullmatch(name):
            die(f"{where}: token name {name!r} must be an identifier "
                f"(letter/underscore then letters/digits/underscore)")
        try:
            b = int(str(val), 0)
        except (TypeError, ValueError):
            die(f"{where}: token {name!r} value {val!r} is not a byte literal (e.g. '0x20')")
        if not 0 <= b <= 0xFF:
            die(f"{where}: token {name!r} -> {b}; a byte must be 0..255")
        if b in tbl_dec:
            die(f"{where}: token {name!r} -> ${b:02X}, but that byte is also the .tbl glyph "
                f"{tbl_dec[b]!r}; a byte is either a glyph or a token, not both")
        if name in enc:
            die(f"{where}: token {name!r} declared twice")
        enc[name] = b
        dec.setdefault(b, name)   # first declaration wins as the canonical render
    return enc, dec


# ======================================================================================
# Escape grammar — the single source of truth for bytes <-> string. render() and parse()
# are exact inverses for every byte, which is what the round-trip rests on.
# ======================================================================================
def render_record(data: bytes, tbl_dec: "dict[int, str]",
                  tok_dec: "dict[int, str]") -> str:
    """bytes -> escaped string. One token per byte, verbatim (extract is exact)."""
    out = []
    for b in data:
        if b in tok_dec:
            out.append(f"[{tok_dec[b]}]")
        elif b in tbl_dec:
            glyph = tbl_dec[b]
            out.append("[[" if glyph == "[" else glyph)   # escape a literal '['
        else:
            out.append(f"[${b:02X}]")
    return "".join(out)


def parse_record(s: str, tbl_enc: "dict[str, int]",
                 tok_enc: "dict[str, int]", where: str) -> bytes:
    """escaped string -> bytes (inverse of render_record)."""
    out = bytearray()
    i, n = 0, len(s)
    while i < n:
        c = s[i]
        if c == "[":
            if s[i + 1:i + 2] == "[":                      # rule 3: literal '['
                if "[" not in tbl_enc:
                    die(f"{where}: '[[' means a literal '[' glyph, but the .tbl has none")
                out.append(tbl_enc["["])
                i += 2
                continue
            j = s.find("]", i + 1)
            if j < 0:
                die(f"{where}: unterminated '[' at position {i} in {s!r}")
            body = s[i + 1:j]
            if body.startswith("$"):                       # rule 2: raw byte
                hx = body[1:]
                if not _HEX2.fullmatch(hx):
                    die(f"{where}: bad raw byte [{body}] -- need [$NN] with two hex digits")
                out.append(int(hx, 16))
            else:                                           # rule 1: named token
                if body not in tok_enc:
                    die(f"{where}: unknown token [{body}] -- not in the manifest's tokens")
                out.append(tok_enc[body])
            i = j + 1
        else:                                               # rule 4: a .tbl glyph
            if c not in tbl_enc:
                die(f"{where}: character {c!r} is not in the .tbl; write it as [$NN] for a "
                    f"raw byte, or add it to the table")
            out.append(tbl_enc[c])
            i += 1
    return bytes(out)


# ======================================================================================
# Manifest + asset resolution
# ======================================================================================
def resolve_asset(name: str, roots: "list[str]") -> "tuple[str, str, str]":
    """Find <root>/<name>.json across ordered layers (first match wins).
    Returns (layer_root, manifest_path, yaml_path). The .yaml comes from the SAME layer."""
    for root in roots:
        mpath = os.path.join(root, name + ".json")
        if os.path.isfile(mpath):
            return root, mpath, os.path.join(root, name + ".yaml")
    base = roots[-1] if roots else "(none)"
    die(f"asset '{name}' not found in any layer: {roots}. "
        f"The base layer '{base}' must contain it.")


def resolve_file(ref: str, roots: "list[str]") -> str:
    """Find a shared file (e.g. a .tbl) by relative ref across ordered layers."""
    for root in roots:
        p = os.path.join(root, ref)
        if os.path.isfile(p):
            return p
    die(f"file '{ref}' not found in any layer: {roots}")


def load_manifest(path: str) -> dict:
    try:
        man = json.load(open(path, encoding="utf-8"))
    except json.JSONDecodeError as e:
        die(f"{path}: invalid JSON: {e}")

    ver = man.get("ver") or LATEST_VER
    if ver not in SUPPORTED_VERS:
        die(f"{path}: manifest version '{ver}' is not implemented by {TOOL_VERSION} "
            f"(supported: {sorted(SUPPORTED_VERS)}). Refusing to guess.")
    man["ver"] = ver

    typ = man.get("type", "")
    if not typ.startswith("text."):
        die(f"{path}: type '{typ}' is not a text.* type; textpack cannot handle it")

    text = man.get("text")
    if not isinstance(text, dict):
        die(f"{path}: 'text' block missing or not an object")
    for k in ("tbl", "count", "record_width", "pad"):
        if k not in text:
            die(f"{path}: text.{k} is required")
    if not isinstance(text["count"], int) or text["count"] < 0:
        die(f"{path}: text.count must be a non-negative integer")
    if not isinstance(text["record_width"], int) or text["record_width"] < 1:
        die(f"{path}: text.record_width must be a positive integer")
    try:
        text["_pad_byte"] = int(str(text["pad"]), 0)
    except (TypeError, ValueError):
        die(f"{path}: text.pad {text['pad']!r} is not a byte literal (e.g. '0xEF')")
    if not 0 <= text["_pad_byte"] <= 0xFF:
        die(f"{path}: text.pad {text['pad']!r} is out of range 0..255")
    tokens = text.get("tokens")
    if tokens is not None and not isinstance(tokens, dict):
        die(f"{path}: text.tokens must be an object of NAME -> '0xNN'")
    return man


def load_records(yaml_path: str, count: int) -> "list[str]":
    """Read the YAML content file -> a dense list of `count` record strings.

    Keys must be exactly the dense range 0..count-1: a missing or duplicate (or
    out-of-range) index is a hard error, so a hand-edit that drops or dupes a row is
    caught here rather than silently shifting every downstream record."""
    try:
        doc = yaml.load(open(yaml_path, encoding="utf-8"), Loader=_NoDupLoader)
    except yaml.YAMLError as e:
        die(f"{yaml_path}: invalid YAML: {e}")
    if not isinstance(doc, dict) or "records" not in doc:
        die(f"{yaml_path}: expected a top-level 'records:' map")
    rec = doc["records"]
    if not isinstance(rec, dict):
        die(f"{yaml_path}: 'records' must be a map of integer index -> string")

    seen: "dict[int, str]" = {}
    for k, v in rec.items():
        if not isinstance(k, int):
            die(f"{yaml_path}: record key {k!r} is not an integer index")
        if k in seen:
            die(f"{yaml_path}: duplicate record index {k}")
        if not isinstance(v, str):
            die(f"{yaml_path}: record {k} value {v!r} is not a string "
                f"(quote it -- a bare number or word can parse as int/bool)")
        seen[k] = v
    missing = [i for i in range(count) if i not in seen]
    if missing:
        die(f"{yaml_path}: missing record index/indices {missing[:8]}"
            f"{' ...' if len(missing) > 8 else ''} (indices must be dense 0..{count - 1})")
    extra = [k for k in seen if k < 0 or k >= count]
    if extra:
        die(f"{yaml_path}: record index/indices {sorted(extra)[:8]} out of range "
            f"0..{count - 1} (manifest text.count={count})")
    return [seen[i] for i in range(count)]


def compile_records(records: "list[str]", width: int, pad_byte: int,
                    tbl_enc: dict, tok_enc: dict, where: str) -> bytes:
    """Encode each record to exactly `width` bytes: parse, halt if over width, else
    right-pad with the pad byte. Returns the concatenated blob."""
    out = bytearray()
    for i, s in enumerate(records):
        data = parse_record(s, tbl_enc, tok_enc, f"{where} record {i}")
        if len(data) > width:
            die(f"{where}: record {i} encodes to {len(data)} bytes but record_width is "
                f"{width}; it is over width. Shorten it -- compile never truncates.")
        out += data + bytes([pad_byte]) * (width - len(data))
    return bytes(out)


def compile_asset(name: str, roots: "list[str]") -> "tuple[bytes, dict, str]":
    layer, mpath, ypath = resolve_asset(name, roots)
    man = load_manifest(mpath)
    if not os.path.isfile(ypath):
        die(f"{ypath}: manifest resolved from layer '{layer}' but its .yaml is missing")
    text = man["text"]
    tbl_path = resolve_file(text["tbl"], roots)
    tbl_dec, tbl_enc = load_tbl(tbl_path)
    tok_enc, _ = parse_tokens(text.get("tokens"), tbl_dec, mpath)
    records = load_records(ypath, text["count"])
    blob = compile_records(records, text["record_width"], text["_pad_byte"],
                           tbl_enc, tok_enc, name)
    expect = text["count"] * text["record_width"]
    if len(blob) != expect:
        die(f"{name}: internal -- compiled {len(blob)} bytes, expected {expect}")
    src_len = man.get("source", {}).get("length")
    if src_len is not None and len(blob) != src_len:
        die(f"{name}: compiled {len(blob)} bytes but manifest declares source.length={src_len}")
    return blob, man, layer


# ======================================================================================
# YAML emission — hand-written for byte-deterministic output. We do NOT use yaml.dump:
# its formatting varies by version and it would not guarantee unconditional double-quoting
# (needed so leading/trailing spaces in a record survive) or stable key order.
# ======================================================================================
def _yaml_quote(s: str) -> str:
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'


def write_yaml(path: str, name: str, typ: str, records: "list[str]") -> None:
    lines = [
        f"# {name} — {typ}",
        f"# {len(records)} records. Edit VALUES only; keys must stay dense 0..{len(records) - 1}.",
        "# Glyphs come from the .tbl; [Name] tokens and [$NN] raw bytes from the manifest.",
        "records:",
    ]
    for i, s in enumerate(records):
        lines.append(f"  {i}: {_yaml_quote(s)}")
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")


# ======================================================================================
# Commands
# ======================================================================================
def cmd_extract(a) -> int:
    with open(a.rom, "rb") as f:
        rom = f.read()
    off, length, width = a.offset, a.length, a.width
    if width < 1:
        die("--width must be >= 1")
    if off + length > len(rom):
        die(f"range 0x{off:X}+{length} exceeds input size {len(rom)}")
    if length % width:
        die(f"length {length} is not a multiple of --width {width}")
    typ = a.type
    if not typ.startswith("text."):
        die(f"--type '{typ}' must be a text.* type")

    blob = rom[off:off + length]
    count = length // width
    sha = hashlib.sha256(blob).hexdigest()

    tbl_path = resolve_file(a.tbl, [a.root])
    tbl_dec, tbl_enc = load_tbl(tbl_path)

    tokmap = {}
    if a.tokens:
        tok_doc = json.load(open(a.tokens, encoding="utf-8"))
        tokmap = tok_doc.get("tokens", tok_doc) if isinstance(tok_doc, dict) else {}
    tok_enc, tok_dec = parse_tokens(tokmap, tbl_dec, a.tokens or "(--tokens)")

    if a.pad is not None:
        pad_byte = int(a.pad, 0)
    elif " " in tbl_enc:
        pad_byte = tbl_enc[" "]      # default: the native space glyph
    else:
        die("no --pad given and the .tbl has no space glyph to default from")
    if not 0 <= pad_byte <= 0xFF:
        die(f"--pad {a.pad} out of range 0..255")

    records = [render_record(blob[i * width:(i + 1) * width], tbl_dec, tok_dec)
               for i in range(count)]

    man = {
        "name": a.name,
        "type": typ,
        "ver": LATEST_VER,
        "source": {"length": length, "source_sha256": sha},
        "text": {
            "tbl": a.tbl,
            "count": count,
            "record_width": width,
            "pad": f"0x{pad_byte:02X}",
            "tokens": tokmap,
        },
        "generated_by": TOOL_VERSION,
    }
    if a.snes_addr:
        man["source"]["snes_addr"] = a.snes_addr
    if a.rom_offset is not None:
        man["source"]["rom_offset"] = f"0x{a.rom_offset:X}"

    mpath = os.path.join(a.root, a.name + ".json")
    ypath = os.path.join(a.root, a.name + ".yaml")
    os.makedirs(os.path.dirname(os.path.abspath(mpath)), exist_ok=True)
    with open(mpath, "w", encoding="utf-8", newline="\n") as f:
        json.dump(man, f, indent=2)
        f.write("\n")
    write_yaml(ypath, a.name, typ, records)

    print(f"extracted {count} records of width {width} ({length} bytes) from 0x{off:X}")
    print(f"  yaml     : {ypath}")
    print(f"  manifest : {mpath}")
    print(f"  tbl      : {tbl_path}")
    print(f"  pad      : 0x{pad_byte:02X}")
    print(f"  sha256   : {sha}")

    # Immediate self-check: re-encode what we just wrote back to the source bytes.
    back = compile_records(records, width, pad_byte, tbl_enc, tok_enc, a.name)
    if back != blob:
        die("SELF-CHECK FAILED: re-encoding the extracted records did not reproduce the "
            "source bytes. The codec is wrong -- do not trust this asset.")
    print("  self-check: re-encode reproduces source bytes exactly  [OK]")
    return 0


def cmd_compile(a) -> int:
    blob, man, layer = compile_asset(a.name, a.search)
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    with open(a.out, "wb") as f:
        f.write(blob)
    print(f"compiled {a.name} from layer '{layer}' -> {a.out} ({len(blob)} bytes)")
    return 0


def cmd_verify(a) -> int:
    blob, man, layer = compile_asset(a.name, a.search)
    got = hashlib.sha256(blob).hexdigest()
    want = man.get("source", {}).get("source_sha256")
    ok = True
    print(f"asset      : {a.name}  (layer '{layer}', {man['type']} {man['ver']})")
    print(f"compiled   : {len(blob)} bytes")
    print(f"sha256     : {got}")
    if want:
        print("manifest   : " + ("MATCH  [OK]" if got == want
                                  else f"MISMATCH (expected {want})  [FAIL]"))
        ok = ok and got == want
    else:
        print("manifest   : no source_sha256 recorded -- skipped")
    if a.rom:
        src = man.get("source", {})
        off = int(str(src.get("rom_offset", "0")), 0)
        length = src.get("length", len(blob))
        original = open(a.rom, "rb").read()[off:off + length]
        if blob == original:
            print(f"rom @0x{off:X}: BYTE-IDENTICAL  [OK]")
        else:
            diff = sum(1 for x, y in zip(blob, original) if x != y)
            print(f"rom @0x{off:X}: DIFFERS in {diff} byte(s)  [FAIL]")
            ok = False
    print("RESULT     :", "PASS" if ok else "FAIL")
    return 0 if ok else 1


# ======================================================================================
# selftest — in-memory codec assertions. No files, no ROM: this is the public-CI gate.
# ======================================================================================
def _expect_die(fn, needle: str, label: str) -> None:
    try:
        fn()
    except SystemExit:
        return
    raise AssertionError(f"selftest: expected {label} to fail-loud, but it succeeded")


def cmd_selftest(a) -> int:
    # A tiny table + tokens exercising all four escape rules, incl. a '[' glyph.
    tbl_dec = {0xA0: "A", 0xBA: "a", 0xEF: " ", 0x5B: "["}
    tbl_enc = {v: k for k, v in tbl_dec.items()}
    tok_enc, tok_dec = parse_tokens({"Blade": "0x20", "Sword": "0x24"}, tbl_dec, "selftest")

    # 1. render/parse are exact inverses for EVERY byte 0..255 (bootstrap: unmapped -> [$NN]).
    allbytes = bytes(range(256))
    s = render_record(allbytes, tbl_dec, tok_dec)
    assert parse_record(s, tbl_enc, tok_enc, "selftest") == allbytes, "full-range round-trip"

    # 2. each escape rule renders as expected.
    assert render_record(bytes([0x20]), tbl_dec, tok_dec) == "[Blade]", "token render"
    assert render_record(bytes([0xA0]), tbl_dec, tok_dec) == "A", "glyph render"
    assert render_record(bytes([0x5B]), tbl_dec, tok_dec) == "[[", "literal-[ render"
    assert render_record(bytes([0x99]), tbl_dec, tok_dec) == "[$99]", "raw render"
    assert parse_record("[Blade]Aa [[[$99]", tbl_enc, tok_enc, "selftest") == \
        bytes([0x20, 0xA0, 0xBA, 0xEF, 0x5B, 0x99]), "mixed parse"

    # 3. empty table: everything is a raw escape and still round-trips.
    e_s = render_record(allbytes, {}, {})
    assert parse_record(e_s, {}, {}, "selftest") == allbytes, "empty-tbl bootstrap"
    # byte 0xA0 (the 'A' glyph in the real table) must render as a raw escape here, not 'A'
    assert render_record(bytes([0xA0]), {}, {}) == "[$A0]", "empty-tbl uses raw escapes only"

    # 4. determinism: rendering the same bytes twice is identical.
    assert render_record(allbytes, tbl_dec, tok_dec) == s, "render determinism"

    # 5. compile pads short records and preserves exact width.
    blob = compile_records(["A", "", "[Blade]A"], 3, 0xEF, tbl_enc, tok_enc, "selftest")
    assert blob == bytes([0xA0, 0xEF, 0xEF, 0xEF, 0xEF, 0xEF, 0x20, 0xA0, 0xEF]), "pad"

    # 6. over-width halts.
    _expect_die(lambda: compile_records(["AAAA"], 3, 0xEF, tbl_enc, tok_enc, "selftest"),
                "over width", "over-width record")
    # 7. missing / duplicate / out-of-range index halts (via a temp yaml).
    import tempfile
    def _rt(body):
        p = os.path.join(tempfile.mkdtemp(), "r.yaml")
        open(p, "w", encoding="utf-8").write(body)
        return lambda: load_records(p, 3)
    _expect_die(_rt('records:\n  0: "A"\n  2: "A"\n'), "missing", "missing index")
    _expect_die(_rt('records:\n  0: "A"\n  0: "A"\n  1: "A"\n  2: "A"\n'),
                "duplicate", "duplicate index")   # caught by _NoDupLoader before dense check
    _expect_die(_rt('records:\n  0: "A"\n  1: "A"\n  2: "A"\n  5: "A"\n'), "range", "out-of-range")

    # 8. unknown token and unmapped glyph on compile both halt.
    _expect_die(lambda: parse_record("[Nope]", tbl_enc, tok_enc, "selftest"),
                "unknown token", "unknown token")
    _expect_die(lambda: parse_record("Z", tbl_enc, tok_enc, "selftest"),
                "not in the .tbl", "unmapped glyph")

    # 9. table injectivity is enforced at load (two bytes -> same glyph is rejected).
    import tempfile as _tf
    def _tbl(body):
        p = os.path.join(_tf.mkdtemp(), "t.tbl")
        open(p, "w", encoding="utf-8").write(body)
        return lambda: load_tbl(p)
    _expect_die(_tbl("A0=x\nA1=x\n"), "injective", "non-injective table")
    # a byte cannot be both a glyph and a token.
    _expect_die(lambda: parse_tokens({"Dup": "0xA0"}, tbl_dec, "selftest"),
                "glyph or a token", "glyph/token collision")

    print("selftest: all codec invariants hold  [OK]")
    return 0


# ======================================================================================
def main(argv=None) -> int:
    p = argparse.ArgumentParser(
        prog="textpack", description="fixed-width game text <-> YAML (byte-identical)")
    p.add_argument("--version", action="version", version=TOOL_VERSION)
    sub = p.add_subparsers(dest="cmd", required=True)

    def add_search(sp):
        sp.add_argument("--search", action="append", default=None, metavar="ROOT",
                        help="asset layer root; repeat in priority order (highest first)")

    e = sub.add_parser("extract", help="raw bytes -> yaml + manifest")
    e.add_argument("--rom", required=True, help="raw bytes to read (a ROM or a .bin slice)")
    e.add_argument("--offset", default=0, type=lambda s: int(s, 0))
    e.add_argument("--length", required=True, type=lambda s: int(s, 0))
    e.add_argument("--width", required=True, type=int, help="fixed record width in bytes")
    e.add_argument("--name", required=True, help="logical name, e.g. text/item_names")
    e.add_argument("--type", default="text.ct.mapped", help="manifest type (text.*)")
    e.add_argument("--root", default="assets/src", help="layer root to write into")
    e.add_argument("--tbl", required=True, help="character table, relative to --root")
    e.add_argument("--tokens", default=None, help="JSON named-token map (icons/codes)")
    e.add_argument("--pad", default=None, help="pad byte, e.g. 0xEF (default: the space glyph)")
    e.add_argument("--snes-addr", default=None, dest="snes_addr")
    e.add_argument("--rom-offset", default=None, dest="rom_offset",
                   type=lambda s: int(s, 0), help="ROM file offset, for later verify --rom")
    e.set_defaults(fn=cmd_extract)

    c = sub.add_parser("compile", help="yaml + manifest -> raw .bin")
    c.add_argument("--name", required=True)
    add_search(c)
    c.add_argument("--out", required=True)
    c.set_defaults(fn=cmd_compile)

    v = sub.add_parser("verify", help="compile and assert byte-identity")
    v.add_argument("--name", required=True)
    add_search(v)
    v.add_argument("--rom", default=None, help="also compare against ROM bytes at rom_offset")
    v.set_defaults(fn=cmd_verify)

    st = sub.add_parser("selftest", help="in-memory codec assertions (needs no files)")
    st.set_defaults(fn=cmd_selftest)

    a = p.parse_args(argv)
    if getattr(a, "search", None) is None and a.cmd in ("compile", "verify"):
        a.search = ["assets/src"]
    return a.fn(a)


if __name__ == "__main__":
    sys.exit(main())

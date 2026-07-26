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
  extract  manifest + ROM + tbl  -> the editable .yaml (ROM ground truth; re-runnable)
  fork     effective bundle      -> a private copy under <mods>/<mod>/ (manifest + .yaml)
  compile  yaml + manifest + tbl -> raw .bin
  verify   yaml + manifest + tbl -> compile and assert sha256 (and optional ROM bytes)
  selftest                       -> codec + layering assertions; needs no repo (CI gate)

Layering: an asset is addressed by LOGICAL NAME and resolved against ordered
COMPLETE-BUNDLE roots (`--search`, highest priority first: mod layers, then the
hand-authored layer) and finally a base PAIR — manifests from `--base-manifests`,
content from `--base-content`. See resolve_asset for why the base is a pair and
everything above it is not.

Example (CT item-name table: 242 records x 11 bytes):
  textpack.py extract --manifest generated/assets/text/item_names.json \
      --rom rom/ct-us-orig.sfc --out extracted/text/item_names.yaml
  textpack.py fork    --name text/item_names --mod mymod
  textpack.py compile --name text/item_names --out build/assets/text/item_names.bin
  textpack.py verify  --name text/item_names
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import sys

# --------------------------------------------------------------------------------------
# Versioning. Manifests carry {"type": ..., "ver": ...}. `ver` omitted => LATEST. When a
# codec's BYTE-LEVEL semantics change, add a version and keep the old handler so existing
# manifests keep building. An unimplemented version is a HARD ERROR — never best-effort.
# --------------------------------------------------------------------------------------
LATEST_VER = "v1"
SUPPORTED_VERS = {"v1"}
TOOL_VERSION = "textpack/1.0.0"

# --------------------------------------------------------------------------------------
# Layering defaults. Repo-root-relative, and overridable on every command that resolves an
# asset, so nothing here is baked into the build graph.
#   search roots  -- ordered, complete-bundle layers: mod overlays first, hand-authored last
#   base pair     -- manifests come from the exporter's output dir, content from the dir the
#                    `extract` command fills from the ROM
# --------------------------------------------------------------------------------------
DEFAULT_SEARCH_ROOTS = ("assets",)
DEFAULT_BASE_MANIFESTS = "generated/assets"
DEFAULT_BASE_CONTENT = "extracted"
DEFAULT_MODS_DIR = "mods"

# The editable content file for a text asset. One extension, always.
CONTENT_EXT = ".yaml"

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
def resolve_asset(name: str, search_roots: "list[str]", base_manifests: str,
                  base_content: str, ext: str = CONTENT_EXT) -> "tuple[str, str, str]":
    """Resolve a logical asset name -> (manifest_path, content_path, layer_label).

    Per-asset-BUNDLE resolution: a manifest and its content must describe each other, so
    they must come from the same place. An override layer that supplied only the .yaml
    would be compiled against a manifest describing different records; only the manifest,
    and the stock text would be compiled with the override's record width. Both are silent
    corruption, so both are refused.

    1. Walk `search_roots` in priority order. A root matches only if BOTH
       <root>/<name>.json and <root>/<name><ext> exist there. A root holding exactly one
       half is a misconfiguration and HALTS -- skipping it would quietly build something
       other than what the layer was created to change.
    2. Otherwise fall back to the base PAIR: manifest from `base_manifests`, content from
       `base_content`. These two directories are one logical bundle that the repo layout
       splits in two (regenerated description vs. regenerated content), which is why the
       same-layer rule is relaxed here and nowhere else. Missing either half HALTS.
    """
    for root in search_roots:
        mpath = os.path.join(root, name + ".json")
        cpath = os.path.join(root, name + ext)
        has_m, has_c = os.path.isfile(mpath), os.path.isfile(cpath)
        if has_m and has_c:
            return mpath, cpath, root
        if has_m or has_c:
            present, absent = (mpath, cpath) if has_m else (cpath, mpath)
            die(f"layer root '{root}' holds only half of asset '{name}': {present} exists "
                f"but {absent} does not. A search root must carry a COMPLETE bundle -- "
                f"the manifest and its content must come from the same layer. Add the "
                f"missing file (see the `fork` command) or remove the other one.")
    mpath = os.path.join(base_manifests, name + ".json")
    cpath = os.path.join(base_content, name + ext)
    absent = [p for p in (mpath, cpath) if not os.path.isfile(p)]
    if absent:
        die(f"asset '{name}' not found. Searched complete-bundle roots "
            f"{list(search_roots)}, then the base pair (manifests '{base_manifests}', "
            f"content '{base_content}'); missing there: {absent}. The base pair must "
            f"always hold the asset -- manifests come from export, content from `extract`.")
    return mpath, cpath, f"{base_manifests} + {base_content}"


def resolve_file(ref: str, search_roots: "list[str]", base_content: str,
                 base_manifests: str) -> str:
    """Find a SHARED file (a .tbl, a token map) by repo-relative ref.

    Shared files are not bundles -- nothing pairs with them -- so they use a plain
    first-match-wins walk over every layer: the search roots in priority order, then the
    base content dir, then the base manifest dir.
    """
    roots = [*search_roots, base_content, base_manifests]
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


def compile_asset(name: str, search_roots: "list[str]", base_manifests: str,
                  base_content: str) -> "tuple[bytes, dict, str]":
    mpath, ypath, layer = resolve_asset(name, search_roots, base_manifests, base_content)
    man = load_manifest(mpath)
    text = man["text"]
    tbl_path = resolve_file(text["tbl"], search_roots, base_content, base_manifests)
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
def read_rom_slice(rom_path: str, man: dict, manifest_path: str) -> bytes:
    """Read the ROM bytes a manifest describes, and prove they are the right ones.

    The `source` block is a claim about a specific cartridge. Checking its sha256 before
    decoding is what turns "you pointed at some ROM" into a hard error instead of a
    plausible-looking asset built from the wrong bytes -- a wrong region, a different
    revision, or a headered dump all land here.
    """
    src = man.get("source") or {}
    if "rom_offset" not in src or "length" not in src:
        die(f"{manifest_path}: extract needs source.rom_offset and source.length, and the "
            f"manifest has no ROM provenance. A hand-authored asset is not extractable: "
            f"its content is the source, there is nothing to regenerate it from.")
    off = int(str(src["rom_offset"]), 0)
    length = src["length"]
    if not isinstance(length, int) or length < 0:
        die(f"{manifest_path}: source.length {length!r} must be a non-negative integer")
    with open(rom_path, "rb") as f:
        rom = f.read()
    if off + length > len(rom):
        die(f"{rom_path}: range 0x{off:X}+{length} exceeds the file size {len(rom)}. "
            f"{manifest_path} describes data this ROM does not contain.")
    blob = rom[off:off + length]

    want = src.get("source_sha256")
    if not want:
        die(f"{manifest_path}: source.source_sha256 is missing. Extraction is only safe "
            f"when the bytes can be proven to be the ones the manifest describes.")
    got = hashlib.sha256(blob).hexdigest()
    if got != want:
        die(f"{rom_path}: bytes at 0x{off:X}+{length} hash to {got}, but "
            f"{manifest_path} declares source_sha256 {want}. This is not the ROM the "
            f"asset was exported from (wrong version, wrong region, or a headered dump). "
            f"Refusing to extract.")
    return blob


def cmd_extract(a) -> int:
    """ROM -> the editable .yaml, driven by ONE explicit manifest.

    Extraction is ROM ground truth, so it deliberately does not resolve the manifest
    through the layer search: a mod override must not be able to change what "the original
    data" means. The .tbl and any other shared file still resolve through the layers,
    because those are presentation, not data.

    There is no edited copy to protect here (edits live in a mod layer), so extract simply
    overwrites and is always safe to re-run. Its output is byte-deterministic.
    """
    man = load_manifest(a.manifest)
    text = man["text"]
    blob = read_rom_slice(a.rom, man, a.manifest)

    width, count = text["record_width"], text["count"]
    expect = count * width
    if len(blob) != expect:
        die(f"{a.manifest}: describes {count} records of width {width} ({expect} bytes) "
            f"but source.length is {len(blob)}. The manifest contradicts itself; slicing "
            f"records out of it would produce plausible but wrong text.")

    tbl_path = resolve_file(text["tbl"], a.search, a.base_content, a.base_manifests)
    tbl_dec, tbl_enc = load_tbl(tbl_path)
    tok_enc, tok_dec = parse_tokens(text.get("tokens"), tbl_dec, a.manifest)

    records = [render_record(blob[i * width:(i + 1) * width], tbl_dec, tok_dec)
               for i in range(count)]
    write_yaml(a.out, man["name"], man["type"], records)

    # Immediate self-check: re-encode what we just wrote back to the source bytes. If the
    # .tbl or tokens disagree with the data this catches it here, not at ROM-build time.
    back = compile_records(records, width, text["_pad_byte"], tbl_enc, tok_enc, man["name"])
    if back != blob:
        die("SELF-CHECK FAILED: re-encoding the extracted records did not reproduce the "
            "source bytes. Do not trust this asset.")

    src = man["source"]
    print(f"extracted {man['name']} ({count} records of width {width}, {len(blob)} bytes) "
          f"from {a.rom} @{src['rom_offset']}")
    print(f"  manifest : {a.manifest}")
    print(f"  tbl      : {tbl_path}")
    print(f"  yaml     : {a.out}")
    print(f"  sha256   : {src['source_sha256']}  [matches ROM]")
    print("  self-check: re-encode reproduces source bytes exactly  [OK]")
    return 0


def cmd_fork(a) -> int:
    """Copy the currently-effective bundle into a mod layer, so it can be edited there.

    Resolution is exactly compile's, so `fork` always branches from whatever the build is
    using right now -- including an already-forked lower-priority mod. Both halves are
    copied together: that is what keeps the complete-bundle rule satisfiable by hand.

    It never overwrites. A second fork onto an edited copy would destroy the edits, and
    there is no way to tell that apart from a legitimate re-fork.
    """
    mpath, cpath, layer = resolve_asset(a.name, a.search, a.base_manifests, a.base_content)
    dest_root = os.path.join(a.mods_dir, a.mod)
    dst_m = os.path.join(dest_root, a.name + ".json")
    dst_c = os.path.join(dest_root, a.name + CONTENT_EXT)

    existing = [p for p in (dst_m, dst_c) if os.path.exists(p)]
    if existing:
        die(f"refusing to overwrite {existing} -- '{a.name}' is already forked into mod "
            f"'{a.mod}'. Delete those files first if you really want to restart from the "
            f"current bundle; editing them in place is the normal workflow.")

    os.makedirs(os.path.dirname(os.path.abspath(dst_m)), exist_ok=True)
    shutil.copyfile(mpath, dst_m)
    shutil.copyfile(cpath, dst_c)

    print(f"forked {a.name} from layer '{layer}' into mod '{a.mod}'")
    print(f"  manifest : {mpath}  ->  {dst_m}")
    print(f"  yaml     : {cpath}  ->  {dst_c}")
    print(f"  edit {dst_c}, then build with --search {dest_root} ahead of the other roots")
    return 0


def cmd_compile(a) -> int:
    blob, man, layer = compile_asset(a.name, a.search, a.base_manifests, a.base_content)
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    with open(a.out, "wb") as f:
        f.write(blob)
    print(f"compiled {a.name} from layer '{layer}' -> {a.out} ({len(blob)} bytes)")
    return 0


def cmd_verify(a) -> int:
    blob, man, layer = compile_asset(a.name, a.search, a.base_manifests, a.base_content)
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
# selftest — codec + layering assertions against throwaway temp trees. Needs no repo and
# no ROM: this is the public-CI gate.
# ======================================================================================
def _expect_die(fn, needle: str, label: str) -> None:
    try:
        fn()
    except SystemExit:
        return
    raise AssertionError(f"selftest: expected {label} to fail-loud, but it succeeded")


def _quiet(fn):
    """Run a command function with its progress output swallowed."""
    import contextlib
    import io
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        return fn()


def _mkfile(path: str, body: "str | bytes") -> str:
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    if isinstance(body, bytes):
        with open(path, "wb") as f:
            f.write(body)
    else:
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(body)
    return path


def _selftest_layering() -> None:
    """Resolution: mod bundle wins, half a bundle halts, base pair is the fallback."""
    import tempfile
    tmp = tempfile.mkdtemp(prefix="textpack-layers-")
    R = lambda *p: os.path.join(tmp, *p)
    name = "text/demo"
    gen, ext, assets, mod = R("generated", "assets"), R("extracted"), R("assets"), R("mods", "m")
    roots = [mod, assets]

    # 1. base pair: manifest and content live in DIFFERENT directories and still resolve.
    _mkfile(R("generated", "assets", "text", "demo.json"), "{}")
    _mkfile(R("extracted", "text", "demo.yaml"), "")
    m, c, layer = resolve_asset(name, roots, gen, ext)
    N = os.path.normpath
    assert (N(m), N(c)) == (N(R("generated", "assets", "text", "demo.json")),
                            N(R("extracted", "text", "demo.yaml"))), \
        f"base-pair fallback: {(m, c)}"

    # 2. a complete bundle in a search root outranks the base pair...
    _mkfile(R("assets", "text", "demo.json"), "{}")
    _mkfile(R("assets", "text", "demo.yaml"), "")
    m, c, layer = resolve_asset(name, roots, gen, ext)
    assert layer == assets and m.startswith(assets) and c.startswith(assets), \
        f"assets bundle should win over the base pair, got layer {layer!r}"

    # 3. ...and a higher-priority mod bundle outranks that.
    _mkfile(R("mods", "m", "text", "demo.json"), "{}")
    _mkfile(R("mods", "m", "text", "demo.yaml"), "")
    m, c, layer = resolve_asset(name, roots, gen, ext)
    assert layer == mod and m.startswith(mod) and c.startswith(mod), \
        f"mod bundle should win, got layer {layer!r}"

    # 4. half a bundle in a search root HALTS -- it must never be silently skipped.
    os.remove(R("mods", "m", "text", "demo.yaml"))
    _expect_die(lambda: resolve_asset(name, roots, gen, ext),
                "half", "manifest-only mod layer")
    os.remove(R("mods", "m", "text", "demo.json"))
    _mkfile(R("mods", "m", "text", "demo.yaml"), "")
    _expect_die(lambda: resolve_asset(name, roots, gen, ext),
                "half", "content-only mod layer")
    os.remove(R("mods", "m", "text", "demo.yaml"))

    # 5. a base pair missing either half halts too.
    os.remove(R("assets", "text", "demo.json"))
    os.remove(R("assets", "text", "demo.yaml"))
    os.remove(R("extracted", "text", "demo.yaml"))
    _expect_die(lambda: resolve_asset(name, roots, gen, ext),
                "not found", "base pair without content")

    # 6. shared files are NOT bundles: plain first-match over search roots, then the base
    #    content dir, then the base manifest dir.
    found = lambda: N(resolve_file("text/x.tbl", roots, ext, gen))
    _mkfile(R("generated", "assets", "text", "x.tbl"), "")
    assert found() == N(R("generated", "assets", "text", "x.tbl")), "tbl from base manifests"
    _mkfile(R("extracted", "text", "x.tbl"), "")
    assert found() == N(R("extracted", "text", "x.tbl")), "base content outranks manifests"
    _mkfile(R("assets", "text", "x.tbl"), "")
    assert found() == N(R("assets", "text", "x.tbl")), "search root outranks the base"
    _mkfile(R("mods", "m", "text", "x.tbl"), "")
    assert found() == N(R("mods", "m", "text", "x.tbl")), "mod root outranks assets"
    _expect_die(lambda: resolve_file("text/nope.tbl", roots, ext, gen),
                "not found", "unresolvable shared file")


def _selftest_extract_fork() -> None:
    """extract: ROM slice + sha gate + determinism. fork: copies both halves, once only."""
    import tempfile
    tmp = tempfile.mkdtemp(prefix="textpack-extract-")
    R = lambda *p: os.path.join(tmp, *p)
    gen, ext, assets = R("generated", "assets"), R("extracted"), R("assets")

    _mkfile(R("assets", "text", "demo.tbl"), "A0=A\nBA=a\nEF= \n")
    data = bytes([0xA0, 0xBA, 0xEF, 0x20, 0xA0, 0x99])       # 2 records x 3 bytes
    rom = _mkfile(R("fake.sfc"), bytes(0x10) + data + bytes(0x10))

    def manifest(sha, count=2, width=3):
        return _mkfile(R("generated", "assets", "text", "demo.json"), json.dumps({
            "name": "text/demo", "type": "text.ct.mapped", "ver": "v1",
            "source": {"rom_offset": "0x10", "length": len(data), "source_sha256": sha},
            "text": {"tbl": "text/demo.tbl", "count": count, "record_width": width,
                     "pad": "0xEF", "tokens": {"Blade": "0x20"}},
        }, indent=2) + "\n")

    good = hashlib.sha256(data).hexdigest()
    mpath = manifest(good)
    layer_args = ["--search", assets, "--base-manifests", gen, "--base-content", ext]

    out = R("extracted", "text", "demo.yaml")
    _quiet(lambda: main(["extract", "--manifest", mpath, "--rom", rom, "--out", out] + layer_args))
    first = open(out, "rb").read()
    assert b'0: "Aa "' in first and b'1: "[Blade]A[$99]"' in first, \
        f"extract decoded wrongly: {first!r}"

    # Determinism: a second extract of the same inputs must produce the same bytes.
    _quiet(lambda: main(["extract", "--manifest", mpath, "--rom", rom, "--out", out] + layer_args))
    assert open(out, "rb").read() == first, "extract is not byte-deterministic"

    # The extracted content compiles back to exactly the ROM bytes (base-pair resolution).
    blob, _, _ = compile_asset("text/demo", [assets], gen, ext)
    assert blob == data, "extract -> compile is not byte-identical"

    # Wrong ROM: the sha gate must halt rather than decode whatever it was pointed at.
    manifest("0" * 64)
    _expect_die(lambda: _quiet(lambda: main(
        ["extract", "--manifest", mpath, "--rom", rom, "--out", out] + layer_args)),
        "source_sha256", "extract against the wrong ROM")
    # A manifest whose geometry contradicts its own length halts before decoding.
    manifest(good, count=2, width=4)
    _expect_die(lambda: _quiet(lambda: main(
        ["extract", "--manifest", mpath, "--rom", rom, "--out", out] + layer_args)),
        "contradicts", "manifest geometry vs source.length")
    manifest(good)

    # fork copies BOTH halves out of the effective layer, and refuses to do it twice.
    mods = R("mods")
    fork_args = ["fork", "--name", "text/demo", "--mod", "m", "--mods-dir", mods] + layer_args
    _quiet(lambda: main(fork_args))
    assert open(R("mods", "m", "text", "demo.json"), "rb").read() == open(mpath, "rb").read()
    assert open(R("mods", "m", "text", "demo.yaml"), "rb").read() == first
    _expect_die(lambda: _quiet(lambda: main(fork_args)), "overwrite", "re-forking an asset")

    # The forked bundle now outranks the base pair for compile.
    _, _, layer = resolve_asset("text/demo", [R("mods", "m"), assets], gen, ext)
    assert layer == R("mods", "m"), f"forked mod should win, got {layer!r}"


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

    # 10. layer resolution and the ROM-driven commands, against throwaway temp trees.
    _selftest_layering()
    _selftest_extract_fork()

    print("selftest: all codec and layering invariants hold  [OK]")
    return 0


# ======================================================================================
def main(argv=None) -> int:
    p = argparse.ArgumentParser(
        prog="textpack", description="fixed-width game text <-> YAML (byte-identical)")
    p.add_argument("--version", action="version", version=TOOL_VERSION)
    sub = p.add_subparsers(dest="cmd", required=True)

    def add_layers(sp):
        # Repeatable and ordered: the mod-overlay mechanism is just more roots up front.
        sp.add_argument("--search", action="append", default=None, metavar="ROOT",
                        help="complete-bundle layer root; repeat in priority order "
                             f"(highest first). Default: {list(DEFAULT_SEARCH_ROOTS)}")
        sp.add_argument("--base-manifests", default=DEFAULT_BASE_MANIFESTS,
                        dest="base_manifests", metavar="DIR",
                        help=f"base-layer manifest root (default {DEFAULT_BASE_MANIFESTS})")
        sp.add_argument("--base-content", default=DEFAULT_BASE_CONTENT,
                        dest="base_content", metavar="DIR",
                        help=f"base-layer content root (default {DEFAULT_BASE_CONTENT})")

    e = sub.add_parser("extract", help="manifest + ROM -> the editable yaml")
    e.add_argument("--manifest", required=True,
                   help="the manifest to extract by, e.g. generated/assets/text/x.json. "
                        "An explicit path, NOT a layer lookup: extraction is ROM ground "
                        "truth and must not be reachable by a mod override")
    e.add_argument("--rom", required=True, help="the original ROM to slice")
    e.add_argument("--out", required=True, help="the .yaml to write")
    add_layers(e)   # only used to resolve shared files such as the .tbl
    e.set_defaults(fn=cmd_extract)

    fk = sub.add_parser("fork", help="copy the effective bundle into a mod layer")
    fk.add_argument("--name", required=True, help="logical name, e.g. text/item_names")
    fk.add_argument("--mod", required=True, help="mod layer name to fork into")
    fk.add_argument("--mods-dir", default=DEFAULT_MODS_DIR, dest="mods_dir", metavar="DIR",
                    help=f"directory holding mod layers (default {DEFAULT_MODS_DIR})")
    add_layers(fk)
    fk.set_defaults(fn=cmd_fork)

    c = sub.add_parser("compile", help="yaml + manifest -> raw .bin")
    c.add_argument("--name", required=True)
    add_layers(c)
    c.add_argument("--out", required=True)
    c.set_defaults(fn=cmd_compile)

    v = sub.add_parser("verify", help="compile and assert byte-identity")
    v.add_argument("--name", required=True)
    add_layers(v)
    v.add_argument("--rom", default=None, help="also compare against ROM bytes at rom_offset")
    v.set_defaults(fn=cmd_verify)

    st = sub.add_parser("selftest", help="codec + layering assertions (needs no repo)")
    st.set_defaults(fn=cmd_selftest)

    a = p.parse_args(argv)
    if getattr(a, "search", None) is None:
        a.search = list(DEFAULT_SEARCH_ROOTS)
    return a.fn(a)


if __name__ == "__main__":
    sys.exit(main())

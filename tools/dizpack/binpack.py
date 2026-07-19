#!/usr/bin/env python3
"""
binpack — generic verbatim binary asset <-> ROM bytes, byte-identical round-trip.

Part of "dizpack", the stock codec toolset that DiztinGUIsh vendors into a game repo
on export. The game repo must NEVER need Diz at build time — only Python 3. Unlike
gfxpack, binpack has NO third-party dependency (no Pillow): it is pure byte I/O.

What it is FOR
--------------
Some assets have no editable "view" and no codec at all -- the bytes in the ROM already
ARE the asset. Chrono Trigger's BRR audio samples are the first case: a `.brr` file is the
raw ADPCM stream, there is no PNG-equivalent lossy representation, and reinsertion is a
straight copy. `asset-pipeline-design.md` §8.1 forbids Diz from encoding payload bytes in
C#, so the passthrough "codec" lives here, vendored, exactly like gfxpack.

binpack is deliberately GENERIC, not BRR-specific: it copies bytes and picks the payload
file extension with `--ext`. `audio.snes.brr` is today's consumer; `palette.snes.bgr555`
and `tilemap.snes` (both named in the asset taxonomy) get it for free later, by declaring a
different `--ext` and type. See regions-as-partition-plan.md §B.4.

Why a SEPARATE module from gfxpack
----------------------------------
`gfxpack.load_manifest` hard-rejects any `type` not starting with `gfx.snes.`, so binpack
cannot be a mode of it. binpack reuses gfxpack's CONVENTIONS by mirroring them here (not by
importing): the `resolve_asset` search-path (first-match-wins layer roots), `die`,
ver-omitted-means-latest, and options-shallow-merged-over-the-type-block. `romcheck` (the
whole-ROM build oracle) is NOT duplicated here -- it stays shared in gfxpack.py.

Round-trip model (mirrors gfxpack's, minus the codec)
-----------------------------------------------------
The editable source of truth is the payload file (`<name><ext>`, e.g. `<name>.brr`); the
compiled artifact the assembler `incbin`s is `<name>.bin`. For a verbatim asset the two are
byte-identical, so every step below is a plain copy plus an integrity check.

Commands:
  extract  ROM bytes         -> <root>/<name><ext> + <root>/<name>.json
  seed     manifest + .bin    -> <layer>/<name><ext>   (copy; Diz handoff, idempotent)
  compile  <name><ext>        -> <out>.bin (copy), asserts source_sha256
  verify   <name><ext>        -> recompute sha and assert it matches the manifest
                                 (and optionally the live ROM bytes)

The payload extension is resolved as: explicit `--ext` > the `ext` recorded in the manifest
type-block on extract > `.bin`. So a ninja rule may pass `--ext .brr` OR rely on the
manifest -- both work.

Example (a BRR sample, verbatim from a ROM offset):
  binpack.py extract --rom rom.sfc --offset 0x7730F --length 3744 \
                     --type audio.snes.brr --ext .brr --name audio/AudioBRR_00 --root assets/src
  binpack.py compile --name audio/AudioBRR_00 --search assets/src --out build/AudioBRR_00.bin
  binpack.py verify  --name audio/AudioBRR_00 --search assets/src --rom rom.sfc
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys

# --------------------------------------------------------------------------------------
# Versioning (same discipline as gfxpack).
#
# Manifests carry {"type": ..., "ver": ...}. `ver` omitted => LATEST. A version we don't
# implement is a HARD ERROR, never a silent best-effort -- silent drift is exactly what
# breaks byte-identity.
# --------------------------------------------------------------------------------------
LATEST_VER = "v1"
SUPPORTED_VERS = {"v1"}
TOOL_VERSION = "binpack/1.0.0"

DEFAULT_EXT = ".bin"


def die(msg: str) -> "None":
    print(f"binpack: error: {msg}", file=sys.stderr)
    raise SystemExit(2)


# ======================================================================================
# Manifest: resolve, load, validate
#
# The conventions here are copied from gfxpack ON PURPOSE (not imported): a game repo must
# be able to run either tool with nothing but Python, and keeping them independent means a
# change to one can never silently perturb the other's byte output.
# ======================================================================================
def resolve_asset(name: str, roots: "list[str]") -> "tuple[str, str]":
    """Find an asset's manifest by logical name across ordered layer roots (first match wins).

    Returns (layer_root, manifest_path). The payload path is built by the caller as
    <layer_root>/<name><ext>, since binpack's extension is not fixed the way gfxpack's is.
    Per-asset-BUNDLE resolution: the manifest and payload always come from the SAME layer,
    so bytes and format can never be mismatched across mod layers.
    """
    for root in roots:
        mpath = os.path.join(root, name + ".json")
        if os.path.isfile(mpath):
            return root, mpath
    base = roots[-1] if roots else "(none)"
    die(f"asset '{name}' not found in any layer: {roots}. "
        f"The base layer '{base}' must always contain it.")


def block_name_for(typ: str) -> str:
    """The type-specific manifest block is named by the type's first dotted segment:
    `audio.snes.brr` -> `audio`, `palette.snes.bgr555` -> `palette`. Mirrors gfxpack's
    `gfx` block for `gfx.snes.*`."""
    return typ.split(".", 1)[0] if typ else ""


def load_manifest(path: str) -> dict:
    try:
        with open(path, "r", encoding="utf-8") as f:
            man = json.load(f)
    except json.JSONDecodeError as e:
        die(f"{path}: invalid JSON: {e}")

    ver = man.get("ver") or LATEST_VER  # omitted => latest
    if ver not in SUPPORTED_VERS:
        die(f"{path}: manifest version '{ver}' is not implemented by {TOOL_VERSION} "
            f"(supported: {sorted(SUPPORTED_VERS)}). Refusing to guess.")
    man["ver"] = ver

    typ = man.get("type", "")
    if not typ:
        die(f"{path}: manifest has no 'type'")
    # gfx.snes.* assets have a real planar codec and an editable PNG view -- copying their
    # bytes verbatim would treat the .png as the payload and silently corrupt. Refuse them
    # and point at the right tool (the mirror image of gfxpack rejecting non-gfx types).
    if typ.startswith("gfx."):
        die(f"{path}: type '{typ}' is a graphics type -- use gfxpack.py, not binpack "
            f"(binpack is verbatim passthrough and has no planar codec).")

    # "options" is Diz's free-form passthrough: whatever the author typed into the region
    # editor, verbatim. Shallow-merged OVER the type block, so options wins on conflict --
    # same rule gfxpack uses for its gfx block. binpack does not interpret the merged block
    # for the copy itself (the bytes are the bytes); it is honored so metadata like `ext`
    # can be overridden and so the two tools behave consistently.
    block = block_name_for(typ)
    meta = dict(man.get(block) or {})
    options = man.get("options")
    if options is not None:
        if not isinstance(options, dict):
            die(f"{path}: 'options' must be a JSON object, got {type(options).__name__}")
        meta.update(options)
    man[block] = meta

    if man.get("export_only"):
        die(f"{path}: asset is marked export_only (e.g. compressed source data) and "
            f"cannot be compiled back -- it is not round-trippable.")
    return man


def manifest_ext(man: dict, override: "str | None") -> str:
    """Resolve the payload file extension: explicit --ext wins, else the `ext` recorded in
    the type block on extract, else `.bin`."""
    if override:
        return override
    meta = man.get(block_name_for(man.get("type", "")), {})
    ext = meta.get("ext")
    return ext if isinstance(ext, str) and ext else DEFAULT_EXT


def check_integrity(blob: bytes, man: dict, where: str) -> str:
    """Assert the blob matches the manifest's declared source (length + sha256). Returns the
    computed sha256. `source_sha256` is REQUIRED -- it is the whole point of a passthrough
    asset: the only thing that keeps a verbatim copy honest is that its hash still matches
    the ROM bytes it was extracted from."""
    src = man.get("source") or {}
    want_len = src.get("length")
    if want_len is not None and len(blob) != want_len:
        die(f"{where}: {len(blob)} bytes, but the manifest declares source.length={want_len}. "
            f"The payload does not match the data the manifest describes.")

    want_sha = src.get("source_sha256")
    if not want_sha:
        die(f"{where}: manifest has no source.source_sha256 -- a verbatim asset cannot be "
            f"integrity-checked without it. Re-extract the asset.")
    got_sha = hashlib.sha256(blob).hexdigest()
    if got_sha != want_sha:
        die(f"{where}: sha256 {got_sha} does not match the manifest's source_sha256 "
            f"{want_sha}. The bytes have drifted from what was extracted; a verbatim asset "
            f"is not editable (there is no lossy view to reconcile).")
    return got_sha


# ======================================================================================
# Commands
# ======================================================================================
def cmd_extract(a) -> int:
    with open(a.rom, "rb") as f:
        rom = f.read()
    off, length = a.offset, a.length
    if length < 0:
        die("--length must be >= 0")
    if off + length > len(rom):
        die(f"range 0x{off:X}+{length} exceeds ROM size {len(rom)}")

    typ = a.type
    if typ.startswith("gfx."):
        die(f"--type '{typ}' is a graphics type -- use gfxpack.py, not binpack.")
    ext = a.ext or DEFAULT_EXT
    if not ext.startswith("."):
        die(f"--ext must start with a dot, got {ext!r}")

    blob = rom[off:off + length]
    sha = hashlib.sha256(blob).hexdigest()

    payload_path = os.path.join(a.root, a.name + ext)
    man_path = os.path.join(a.root, a.name + ".json")

    os.makedirs(os.path.dirname(os.path.abspath(payload_path)), exist_ok=True)
    with open(payload_path, "wb") as f:
        f.write(blob)

    # Key order mirrors gfxpack + the settled manifest convention:
    #   name / type / ver / source / <block> / [options] / generated_by
    man = {
        "name": a.name,
        "type": typ,
        "ver": LATEST_VER,
        "source": {
            "rom_offset": f"0x{off:X}",
            "length": length,
            "source_sha256": sha,
        },
        block_name_for(typ): {"ext": ext},
        "compressed": False,
        "export_only": False,
        "generated_by": TOOL_VERSION,
    }
    if a.snes_addr:
        man["source"]["snes_addr"] = a.snes_addr

    os.makedirs(os.path.dirname(os.path.abspath(man_path)), exist_ok=True)
    with open(man_path, "w", encoding="utf-8") as f:
        json.dump(man, f, indent=2)
        f.write("\n")

    print(f"extracted {length} bytes ({typ}) from 0x{off:X}")
    print(f"  payload  : {payload_path}")
    print(f"  manifest : {man_path}")
    print(f"  sha256   : {sha}")

    # Immediate self-check: what we just wrote must read back as the source bytes.
    with open(payload_path, "rb") as f:
        back = f.read()
    if back != blob:
        die("SELF-CHECK FAILED: re-reading the extracted payload did not reproduce the "
            "source bytes. Do not trust this asset.")
    print("  self-check: payload reproduces source bytes exactly  [OK]")
    return 0


def cmd_seed(a) -> int:
    """Copy the exported raw .bin into the editable payload file (`<name><ext>`).

    This is the Diz handoff path -- Diz exports a .bin seed, `seed` creates the editable
    payload from it if one doesn't already exist. Idempotent by default: refuses to clobber
    an existing payload (that file may be the user's edited copy).
    """
    layer, mpath = resolve_asset(a.name, a.search)
    man = load_manifest(mpath)
    ext = manifest_ext(man, a.ext)
    payload_path = os.path.join(layer, a.name + ext)

    bin_path = a.bin or os.path.join(layer, a.name + ".bin")
    if not os.path.isfile(bin_path):
        die(f"{bin_path}: no raw .bin to seed from")
    with open(bin_path, "rb") as f:
        blob = f.read()

    # The manifest is a claim about these bytes -- check it rather than trusting it.
    got_sha = check_integrity(blob, man, bin_path)

    if os.path.exists(payload_path) and not a.force:
        print(f"seed: {payload_path} already exists, leaving it alone (use --force to overwrite)")
        return 0

    os.makedirs(os.path.dirname(os.path.abspath(payload_path)), exist_ok=True)
    with open(payload_path, "wb") as f:
        f.write(blob)

    print(f"seeded {a.name} from layer '{layer}' ({len(blob)} bytes, {man['type']})")
    print(f"  payload   : {payload_path}")
    print(f"  manifest  : {mpath}")
    print(f"  sha256    : {got_sha}  [matches manifest]")
    return 0


def compile_asset(name: str, roots: "list[str]", ext_override: "str | None") -> "tuple[bytes, dict, str]":
    """Resolve and read the editable payload, asserting it still matches the manifest.
    Returns (blob, manifest, layer)."""
    layer, mpath = resolve_asset(name, roots)
    man = load_manifest(mpath)
    ext = manifest_ext(man, ext_override)
    payload_path = os.path.join(layer, name + ext)
    if not os.path.isfile(payload_path):
        die(f"{payload_path}: manifest resolved from layer '{layer}' but its payload is missing")
    with open(payload_path, "rb") as f:
        blob = f.read()
    check_integrity(blob, man, payload_path)
    return blob, man, layer


def cmd_compile(a) -> int:
    blob, man, layer = compile_asset(a.name, a.search, a.ext)
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    with open(a.out, "wb") as f:
        f.write(blob)
    print(f"compiled {a.name} from layer '{layer}' -> {a.out} ({len(blob)} bytes)")
    return 0


def cmd_verify(a) -> int:
    layer, mpath = resolve_asset(a.name, a.search)
    man = load_manifest(mpath)
    ext = manifest_ext(man, a.ext)
    payload_path = os.path.join(layer, a.name + ext)
    if not os.path.isfile(payload_path):
        die(f"{payload_path}: manifest resolved from layer '{layer}' but its payload is missing")
    with open(payload_path, "rb") as f:
        blob = f.read()

    got = hashlib.sha256(blob).hexdigest()
    want = (man.get("source") or {}).get("source_sha256")
    ok = True

    print(f"asset      : {a.name}  (layer '{layer}', {man['type']} {man['ver']})")
    print(f"payload    : {len(blob)} bytes")
    print(f"sha256     : {got}")

    if want:
        if got == want:
            print("manifest   : MATCH  [OK]")
        else:
            print(f"manifest   : MISMATCH  (expected {want})  [FAIL]")
            ok = False
    else:
        print("manifest   : no source_sha256 recorded  [FAIL]")
        ok = False

    # Strongest check: compare against the live ROM bytes at the recorded offset.
    if a.rom:
        src = man.get("source") or {}
        off = int(str(src.get("rom_offset", "0")), 0)
        length = src.get("length", len(blob))
        with open(a.rom, "rb") as f:
            rom = f.read()
        original = rom[off:off + length]
        if blob == original:
            print(f"rom @0x{off:X}: BYTE-IDENTICAL  [OK]")
        else:
            diff = sum(1 for x, y in zip(blob, original) if x != y)
            print(f"rom @0x{off:X}: DIFFERS in {diff} byte(s)  [FAIL]")
            ok = False

    print("RESULT     :", "PASS" if ok else "FAIL")
    return 0 if ok else 1


def add_search(sp):
    # Repeatable and ordered: the mod-overlay mechanism is just more roots up front.
    sp.add_argument("--search", action="append", default=None, metavar="ROOT",
                    help="asset layer root; repeat in priority order "
                         "(highest first, base layer last)")
    sp.add_argument("--ext", default=None,
                    help="payload file extension (e.g. .brr). Default: the ext recorded in "
                         "the manifest on extract, else .bin")


def main(argv=None) -> int:
    p = argparse.ArgumentParser(
        prog="binpack",
        description="generic verbatim binary asset <-> ROM bytes (byte-identical round-trip)")
    p.add_argument("--version", action="version", version=TOOL_VERSION)
    sub = p.add_subparsers(dest="cmd", required=True)

    e = sub.add_parser("extract", help="ROM bytes -> payload file + manifest")
    e.add_argument("--rom", required=True)
    e.add_argument("--offset", required=True, type=lambda s: int(s, 0))
    e.add_argument("--length", required=True, type=lambda s: int(s, 0))
    e.add_argument("--type", required=True,
                   help="manifest type contract, e.g. audio.snes.brr (must not be gfx.*)")
    e.add_argument("--ext", default=DEFAULT_EXT,
                   help=f"payload file extension (default {DEFAULT_EXT})")
    e.add_argument("--name", required=True, help="logical name, e.g. audio/AudioBRR_00")
    e.add_argument("--root", default="assets/src", help="layer root to write into")
    e.add_argument("--snes-addr", default=None, dest="snes_addr")
    e.set_defaults(fn=cmd_extract)

    s = sub.add_parser("seed", help="existing manifest + raw .bin -> payload file (Diz handoff)")
    s.add_argument("--name", required=True, help="logical name, e.g. audio/AudioBRR_00")
    add_search(s)
    s.add_argument("--bin", default=None,
                   help="raw .bin to seed from (default: <layer>/<name>.bin)")
    s.add_argument("--force", action="store_true",
                   help="overwrite an existing payload (destroys edits -- off by default)")
    s.set_defaults(fn=cmd_seed)

    c = sub.add_parser("compile", help="payload file -> raw .bin, assert source_sha256")
    c.add_argument("--name", required=True)
    add_search(c)
    c.add_argument("--out", required=True)
    c.set_defaults(fn=cmd_compile)

    v = sub.add_parser("verify", help="assert the payload still matches the manifest (and optionally the ROM)")
    v.add_argument("--name", required=True)
    add_search(v)
    v.add_argument("--rom", default=None,
                   help="also compare against live ROM bytes at the manifest offset")
    v.set_defaults(fn=cmd_verify)

    a = p.parse_args(argv)
    if getattr(a, "search", None) is None and a.cmd in ("seed", "compile", "verify"):
        a.search = ["assets/src"]
    return a.fn(a)


if __name__ == "__main__":
    sys.exit(main())

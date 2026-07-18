#!/usr/bin/env python3
"""
gfxpack — SNES planar graphics <-> indexed PNG, with byte-identical round-trip.

Part of "dizpack", the stock codec toolset that DiztinGUIsh vendors into a game repo
on export. The game repo must NEVER need Diz at build time, so this is plain
Python 3 with **stdlib only** (no Pillow/numpy).

Design invariants (do not break — byte-identity depends on them):
  * The MANIFEST is the authority. The PNG is dumb pixels.
  * COLOR-INDEX PASSTHROUGH: a PNG pixel value IS the raw tile pixel index. The PNG's
    embedded palette is viewer-only and is IGNORED on compile.
  * Assets are addressed by LOGICAL NAME + an ordered list of SEARCH ROOTS
    (first-match-wins). Today that's usually one root; mod overlays later are just
    more roots, in priority order.

Commands:
  extract  ROM bytes            -> <root>/<name>.png + <root>/<name>.json
  compile  png + manifest       -> raw .bin
  verify   png + manifest       -> compile and assert sha256 matches the manifest
                                   (and optionally the live ROM bytes)

Example (an uncompressed 2bpp font sheet, 224 tiles @ ROM offset 0x40000):
  gfxpack.py extract --rom rom.sfc --offset 0x40000 --length 3584 --bpp 2 \
                     --name gfx/font --root assets/src --layout-width 16
  gfxpack.py compile --name gfx/font --search assets/src --out build/font.bin
  gfxpack.py verify  --name gfx/font --search assets/src --rom rom.sfc
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import struct
import sys
import zlib

# --------------------------------------------------------------------------------------
# Versioning
#
# Manifests carry {"type": ..., "ver": ...}. `ver` omitted => LATEST (today's default).
# When a codec's BYTE-LEVEL semantics change, add a new version and keep the old handler
# so existing manifests keep building. A version we don't implement is a HARD ERROR --
# never a silent best-effort, because silent drift is exactly what breaks byte-identity.
# --------------------------------------------------------------------------------------
LATEST_VER = "v1"
SUPPORTED_VERS = {"v1"}
TOOL_VERSION = "gfxpack/1.0.0"

SUPPORTED_BPP = (2, 4, 8)
TILE_W = TILE_H = 8


def die(msg: str) -> "None":
    print(f"gfxpack: error: {msg}", file=sys.stderr)
    raise SystemExit(2)


# ======================================================================================
# Minimal PNG codec (stdlib only) — indexed-color (color type 3) only.
# ======================================================================================
PNG_SIG = b"\x89PNG\r\n\x1a\n"


def _chunk(tag: bytes, data: bytes) -> bytes:
    return (struct.pack(">I", len(data)) + tag + data
            + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))


def png_write_indexed(path: str, width: int, height: int,
                      indices: bytes, palette_rgb: "list[tuple[int, int, int]]") -> None:
    """Write an 8-bit indexed PNG. `indices` is width*height raw index bytes."""
    if len(indices) != width * height:
        die(f"internal: index buffer {len(indices)} != {width}*{height}")
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 3, 0, 0, 0)  # 8-bit, colortype 3
    plte = b"".join(bytes(c) for c in palette_rgb)
    raw = bytearray()
    for y in range(height):
        raw.append(0)  # filter type 0 (None) — we always write unfiltered
        raw += indices[y * width:(y + 1) * width]
    body = (PNG_SIG + _chunk(b"IHDR", ihdr) + _chunk(b"PLTE", plte)
            + _chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + _chunk(b"IEND", b""))
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, "wb") as f:
        f.write(body)


def _paeth(a: int, b: int, c: int) -> int:
    p = a + b - c
    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
    if pa <= pb and pa <= pc:
        return a
    return b if pb <= pc else c


def png_read_indexed(path: str) -> "tuple[int, int, bytes]":
    """Read an indexed PNG -> (width, height, index bytes, one byte per pixel).

    Accepts bit depths 1/2/4/8 (image editors often re-save low-color indexed images at
    a reduced depth) and all five scanline filters. The palette is deliberately ignored:
    we return raw indices, which is what makes the round-trip exact.
    """
    with open(path, "rb") as f:
        data = f.read()
    if not data.startswith(PNG_SIG):
        die(f"{path}: not a PNG")

    pos = len(PNG_SIG)
    width = height = depth = ctype = interlace = None
    idat = bytearray()
    while pos + 8 <= len(data):
        (ln,) = struct.unpack(">I", data[pos:pos + 4])
        tag = data[pos + 4:pos + 8]
        chunk = data[pos + 8:pos + 8 + ln]
        pos += 12 + ln  # length + tag + data + crc
        if tag == b"IHDR":
            width, height, depth, ctype, _comp, _filt, interlace = struct.unpack(
                ">IIBBBBB", chunk)
        elif tag == b"IDAT":
            idat += chunk
        elif tag == b"IEND":
            break

    if width is None:
        die(f"{path}: missing IHDR")
    if ctype != 3:
        die(f"{path}: color type {ctype}; an INDEXED (palette) PNG is required so that "
            f"pixel values are raw indices. Re-save as indexed/palette mode.")
    if interlace != 0:
        die(f"{path}: interlaced PNGs are not supported; re-save without interlacing")
    if depth not in (1, 2, 4, 8):
        die(f"{path}: unsupported bit depth {depth}")

    raw = zlib.decompress(bytes(idat))
    stride = (width * depth + 7) // 8
    bpp = 1  # filtering unit for indexed images is 1 byte
    out = bytearray()
    prev = bytearray(stride)
    p = 0
    for _y in range(height):
        if p >= len(raw):
            die(f"{path}: truncated image data")
        ft = raw[p]
        p += 1
        line = bytearray(raw[p:p + stride])
        p += stride
        if ft == 1:
            for i in range(bpp, stride):
                line[i] = (line[i] + line[i - bpp]) & 0xFF
        elif ft == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 0xFF
        elif ft == 3:
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                line[i] = (line[i] + ((a + prev[i]) >> 1)) & 0xFF
        elif ft == 4:
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                c = prev[i - bpp] if i >= bpp else 0
                line[i] = (line[i] + _paeth(a, prev[i], c)) & 0xFF
        elif ft != 0:
            die(f"{path}: bad filter type {ft}")
        out += line
        prev = line

    if depth == 8:
        pixels = bytes(out)
    else:  # unpack sub-byte indices, MSB-first within each byte
        per = 8 // depth
        mask = (1 << depth) - 1
        px = bytearray()
        for y in range(height):
            row = out[y * stride:(y + 1) * stride]
            for x in range(width):
                b = row[x // per]
                shift = 8 - depth * (x % per + 1)
                px.append((b >> shift) & mask)
        pixels = bytes(px)
    return width, height, pixels


# ======================================================================================
# SNES planar tile codec
#
# A tile is 8x8. Bitplanes are stored in PAIRS: for each pair, 8 rows of 2 bytes
# (low-plane byte, high-plane byte). 2bpp = 1 pair (16 bytes); 4bpp = 2 pairs (32 bytes,
# i.e. two stacked 2bpp halves); 8bpp = 4 pairs (64 bytes). Bit 7 is the LEFTMOST pixel.
# ======================================================================================
def tile_size(bpp: int) -> int:
    return bpp * 8


def decode_tile(buf: bytes, off: int, bpp: int) -> "list[int]":
    """Decode one tile -> 64 palette indices, row-major."""
    px = [0] * 64
    for pair in range(bpp // 2):
        base = off + pair * 16
        lo_shift = pair * 2
        for y in range(8):
            lo = buf[base + y * 2]
            hi = buf[base + y * 2 + 1]
            row = y * 8
            for x in range(8):
                bit = 7 - x
                v = ((lo >> bit) & 1) | (((hi >> bit) & 1) << 1)
                px[row + x] |= v << lo_shift
    return px


def encode_tile(px: "list[int]", bpp: int) -> bytes:
    """Encode 64 palette indices -> planar tile bytes (inverse of decode_tile)."""
    out = bytearray(tile_size(bpp))
    for pair in range(bpp // 2):
        base = pair * 16
        lo_shift = pair * 2
        for y in range(8):
            lo = hi = 0
            row = y * 8
            for x in range(8):
                v = (px[row + x] >> lo_shift) & 0b11
                bit = 7 - x
                lo |= (v & 1) << bit
                hi |= ((v >> 1) & 1) << bit
            out[base + y * 2] = lo
            out[base + y * 2 + 1] = hi
    return bytes(out)


def bytes_to_indices(blob: bytes, bpp: int, tiles: int,
                     layout_w: int) -> "tuple[int, int, bytes]":
    """Planar tile bytes -> a (width, height, indices) image laid out in a tile grid."""
    rows = (tiles + layout_w - 1) // layout_w
    width, height = layout_w * TILE_W, rows * TILE_H
    img = bytearray(width * height)  # unused cells in a ragged last row stay 0
    ts = tile_size(bpp)
    for t in range(tiles):
        px = decode_tile(blob, t * ts, bpp)
        tx, ty = (t % layout_w) * TILE_W, (t // layout_w) * TILE_H
        for y in range(TILE_H):
            dst = (ty + y) * width + tx
            img[dst:dst + TILE_W] = bytes(px[y * 8:(y + 1) * 8])
    return width, height, bytes(img)


def indices_to_bytes(img: bytes, width: int, bpp: int, tiles: int,
                     layout_w: int) -> bytes:
    """Image indices -> planar tile bytes (inverse of bytes_to_indices)."""
    out = bytearray()
    maxv = (1 << bpp) - 1
    for t in range(tiles):
        tx, ty = (t % layout_w) * TILE_W, (t // layout_w) * TILE_H
        px = []
        for y in range(TILE_H):
            src = (ty + y) * width + tx
            row = img[src:src + TILE_W]
            for v in row:
                if v > maxv:
                    die(f"pixel index {v} exceeds max {maxv} for {bpp}bpp (tile {t}). "
                        f"The edited PNG uses more colors than the format allows.")
            px.extend(row)
        out += encode_tile(px, bpp)
    return bytes(out)


def default_palette(bpp: int) -> "list[tuple[int, int, int]]":
    """Viewer-only grayscale ramp. NOT used on compile — indices are what matter."""
    n = 1 << bpp
    return [(i * 255 // (n - 1),) * 3 for i in range(n)]


# ======================================================================================
# Manifest: load, resolve, validate
# ======================================================================================
def resolve_asset(name: str, roots: "list[str]") -> "tuple[str, str, str]":
    """Find an asset by logical name across ordered layer roots (first match wins).

    Per-asset-BUNDLE resolution: the manifest and PNG always come from the SAME layer,
    so pixels and format can never be mismatched across layers.
    Returns (layer_root, manifest_path, png_path).
    """
    for root in roots:
        mpath = os.path.join(root, name + ".json")
        if os.path.isfile(mpath):
            return root, mpath, os.path.join(root, name + ".png")
    base = roots[-1] if roots else "(none)"
    die(f"asset '{name}' not found in any layer: {roots}. "
        f"The base layer '{base}' must always contain it.")


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
    if not typ.startswith("gfx.snes."):
        die(f"{path}: type '{typ}' is not a gfx.snes.* type; gfxpack cannot handle it")

    gfx = man.get("gfx") or {}
    bpp = gfx.get("bpp")
    if bpp not in SUPPORTED_BPP:
        die(f"{path}: gfx.bpp must be one of {SUPPORTED_BPP}, got {bpp!r}")
    if typ != f"gfx.snes.{bpp}bpp":
        die(f"{path}: type '{typ}' disagrees with gfx.bpp={bpp}")
    if gfx.get("tile_w", TILE_W) != TILE_W or gfx.get("tile_h", TILE_H) != TILE_H:
        die(f"{path}: only {TILE_W}x{TILE_H} tiles are supported")
    if not gfx.get("tiles"):
        die(f"{path}: gfx.tiles is required")

    if man.get("export_only"):
        die(f"{path}: asset is marked export_only (e.g. compressed source data) and "
            f"cannot be compiled back — it is not round-trippable.")
    return man


def compile_asset(name: str, roots: "list[str]") -> "tuple[bytes, dict, str]":
    """Resolve, read PNG, and encode to raw planar bytes. Returns (blob, manifest, layer)."""
    layer, mpath, ppath = resolve_asset(name, roots)
    man = load_manifest(mpath)
    if not os.path.isfile(ppath):
        die(f"{ppath}: manifest resolved from layer '{layer}' but its PNG is missing")

    gfx = man["gfx"]
    bpp, tiles = gfx["bpp"], gfx["tiles"]
    layout_w = gfx.get("layout_width_tiles", 16)

    width, height, img = png_read_indexed(ppath)

    # Pinned invariants: reject edits that changed the geometry.
    rows = (tiles + layout_w - 1) // layout_w
    exp_w, exp_h = layout_w * TILE_W, rows * TILE_H
    if (width, height) != (exp_w, exp_h):
        die(f"{ppath}: image is {width}x{height} but the manifest requires {exp_w}x{exp_h} "
            f"({tiles} tiles, {layout_w} per row). Keep the canvas size unchanged.")

    blob = indices_to_bytes(img, width, bpp, tiles, layout_w)

    expected_len = man.get("source", {}).get("length")
    if expected_len is not None and len(blob) != expected_len:
        die(f"{name}: encoded {len(blob)} bytes but manifest declares {expected_len}")
    return blob, man, layer


# ======================================================================================
# Commands
# ======================================================================================
def cmd_extract(a) -> int:
    with open(a.rom, "rb") as f:
        rom = f.read()
    off, length, bpp = a.offset, a.length, a.bpp
    if bpp not in SUPPORTED_BPP:
        die(f"--bpp must be one of {SUPPORTED_BPP}")
    if off + length > len(rom):
        die(f"range 0x{off:X}+{length} exceeds ROM size {len(rom)}")
    ts = tile_size(bpp)
    if length % ts:
        die(f"length {length} is not a multiple of the {bpp}bpp tile size ({ts})")

    blob = rom[off:off + length]
    tiles = length // ts
    layout_w = a.layout_width
    sha = hashlib.sha256(blob).hexdigest()

    width, height, img = bytes_to_indices(blob, bpp, tiles, layout_w)
    png_path = os.path.join(a.root, a.name + ".png")
    man_path = os.path.join(a.root, a.name + ".json")
    png_write_indexed(png_path, width, height, img, default_palette(bpp))

    man = {
        "name": a.name,
        "type": f"gfx.snes.{bpp}bpp",
        "ver": LATEST_VER,
        "source": {
            "rom_offset": f"0x{off:X}",
            "length": length,
            "source_sha256": sha,
        },
        "gfx": {
            "bpp": bpp, "tile_w": TILE_W, "tile_h": TILE_H, "tiles": tiles,
            "plane_order": "snes-interleaved-pairs",
            "layout_width_tiles": layout_w,
        },
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

    print(f"extracted {tiles} tiles ({length} bytes, {bpp}bpp) from 0x{off:X}")
    print(f"  png      : {png_path}  ({width}x{height})")
    print(f"  manifest : {man_path}")
    print(f"  sha256   : {sha}")

    # Immediate self-check: the asset we just wrote must rebuild to the original bytes.
    back = indices_to_bytes(img, width, bpp, tiles, layout_w)
    if back != blob:
        die("SELF-CHECK FAILED: re-encoding the extracted image did not reproduce the "
            "source bytes. The codec is wrong — do not trust this asset.")
    print("  self-check: re-encode reproduces source bytes exactly  [OK]")
    return 0


def cmd_seed(a) -> int:
    """Render a PNG from an EXISTING manifest + raw .bin.

    This is the Diz handoff path. `extract` writes the manifest itself, which makes it
    useless for validating someone else's manifest -- it would just overwrite it and then
    trivially agree with itself. `seed` instead treats the manifest as authoritative input,
    so a manifest that misdescribes the bytes fails loudly here.

    Idempotent by default: refuses to clobber an existing PNG, because that PNG is the
    artist's edited copy and regenerating it from the .bin would silently discard work.
    """
    layer, mpath, ppath = resolve_asset(a.name, a.search)
    man = load_manifest(mpath)

    bin_path = a.bin or os.path.join(layer, a.name + ".bin")
    if not os.path.isfile(bin_path):
        die(f"{bin_path}: no raw .bin to seed from")
    with open(bin_path, "rb") as f:
        blob = f.read()

    gfx = man["gfx"]
    bpp, tiles, layout_w = gfx["bpp"], gfx["tiles"], gfx.get("layout_width_tiles", 16)
    ts = tile_size(bpp)

    # The manifest is a claim about these bytes. Check it rather than trusting it --
    # a wrong bpp/tile count here produces a plausible-looking but wrong image.
    if len(blob) != tiles * ts:
        die(f"{bin_path}: {len(blob)} bytes, but the manifest describes {tiles} tiles at "
            f"{bpp}bpp ({tiles * ts} bytes). The manifest does not match the data.")

    expected_len = man.get("source", {}).get("length")
    if expected_len is not None and len(blob) != expected_len:
        die(f"{bin_path}: {len(blob)} bytes but manifest declares source.length={expected_len}")

    want_sha = man.get("source", {}).get("source_sha256")
    got_sha = hashlib.sha256(blob).hexdigest()
    if want_sha and got_sha != want_sha:
        die(f"{bin_path}: sha256 {got_sha} does not match the manifest's "
            f"source_sha256 {want_sha}. Refusing to seed from bytes the manifest "
            "does not describe.")

    if os.path.exists(ppath) and not a.force:
        print(f"seed: {ppath} already exists, leaving it alone (use --force to overwrite)")
        return 0

    width, height, img = bytes_to_indices(blob, bpp, tiles, layout_w)
    png_write_indexed(ppath, width, height, img, default_palette(bpp))

    back = indices_to_bytes(img, width, bpp, tiles, layout_w)
    if back != blob:
        die("SELF-CHECK FAILED: re-encoding the seeded image did not reproduce the "
            "source bytes. Do not trust this asset.")

    print(f"seeded {a.name} from layer '{layer}' ({tiles} tiles, {bpp}bpp)")
    print(f"  png       : {ppath}  ({width}x{height})")
    print(f"  manifest  : {mpath}")
    print(f"  sha256    : {got_sha}  [matches manifest]" if want_sha else f"  sha256    : {got_sha}")
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
        if got == want:
            print("manifest   : MATCH  [OK]")
        else:
            print(f"manifest   : MISMATCH  (expected {want})  [FAIL]")
            ok = False
    else:
        print("manifest   : no source_sha256 recorded — skipped")

    # Strongest check: compare against the live ROM bytes at the recorded offset.
    if a.rom:
        src = man.get("source", {})
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


def cmd_romcheck(a) -> int:
    """Compare a rebuilt ROM against the original. This is the build's oracle.

    Whole-ROM byte-identity is the only check that actually proves the pipeline is correct:
    every per-asset check can pass while the assembled result is still wrong (wrong incbin
    path, wrong region bounds, an asset that never got recompiled).
    """
    if not os.path.isfile(a.rom):
        die(f"{a.rom}: rebuilt ROM not found")
    if not os.path.isfile(a.expect_file):
        die(f"{a.expect_file}: original ROM not found -- nothing to compare against. "
            "The build cannot verify itself without it.")

    with open(a.rom, "rb") as f:
        got = f.read()
    with open(a.expect_file, "rb") as f:
        want = f.read()

    got_sha = hashlib.sha256(got).hexdigest()
    want_sha = hashlib.sha256(want).hexdigest()

    print(f"rebuilt  : {a.rom}  ({len(got)} bytes)")
    print(f"original : {a.expect_file}  ({len(want)} bytes)")
    print(f"sha256   : {got_sha}")

    if got == want:
        print("RESULT   : BYTE-IDENTICAL  [OK]")
        return 0

    print(f"expected : {want_sha}")
    if len(got) != len(want):
        print(f"RESULT   : SIZE MISMATCH ({len(got)} vs {len(want)})  [FAIL]")
        return 1

    # Point at the first divergence -- for a ROM, "they differ" alone is useless.
    diffs = [i for i, (x, y) in enumerate(zip(got, want)) if x != y]
    first = diffs[0]
    print(f"RESULT   : DIFFERS in {len(diffs)} byte(s)  [FAIL]")
    print(f"  first difference at 0x{first:X}: got 0x{got[first]:02X}, expected 0x{want[first]:02X}")
    if len(diffs) > 1:
        print(f"  last difference at  0x{diffs[-1]:X}")
    return 1


def main(argv=None) -> int:
    p = argparse.ArgumentParser(
        prog="gfxpack", description="SNES planar graphics <-> indexed PNG (round-trip)")
    p.add_argument("--version", action="version", version=TOOL_VERSION)
    sub = p.add_subparsers(dest="cmd", required=True)

    def add_search(sp):
        # Repeatable and ordered: the mod-overlay mechanism is just more roots up front.
        sp.add_argument("--search", action="append", default=None, metavar="ROOT",
                        help="asset layer root; repeat in priority order "
                             "(highest first, base layer last)")

    e = sub.add_parser("extract", help="ROM bytes -> PNG + manifest")
    e.add_argument("--rom", required=True)
    e.add_argument("--offset", required=True, type=lambda s: int(s, 0))
    e.add_argument("--length", required=True, type=lambda s: int(s, 0))
    e.add_argument("--bpp", required=True, type=int)
    e.add_argument("--name", required=True, help="logical name, e.g. gfx/font")
    e.add_argument("--root", default="assets/src", help="layer root to write into")
    e.add_argument("--layout-width", type=int, default=16, dest="layout_width",
                   help="tiles per row in the PNG (cosmetic; recorded in the manifest)")
    e.add_argument("--snes-addr", default=None, dest="snes_addr")
    e.set_defaults(fn=cmd_extract)

    s = sub.add_parser("seed", help="existing manifest + raw .bin -> PNG (Diz handoff)")
    s.add_argument("--name", required=True, help="logical name, e.g. gfx/font")
    add_search(s)
    s.add_argument("--bin", default=None,
                   help="raw .bin to seed from (default: <layer>/<name>.bin)")
    s.add_argument("--force", action="store_true",
                   help="overwrite an existing PNG (destroys edits -- off by default)")
    s.set_defaults(fn=cmd_seed)

    c = sub.add_parser("compile", help="PNG + manifest -> raw .bin")
    c.add_argument("--name", required=True)
    add_search(c)
    c.add_argument("--out", required=True)
    c.set_defaults(fn=cmd_compile)

    v = sub.add_parser("verify", help="compile and assert byte-identity")
    v.add_argument("--name", required=True)
    add_search(v)
    v.add_argument("--rom", default=None,
                   help="also compare against live ROM bytes at the manifest offset")
    v.set_defaults(fn=cmd_verify)

    r = sub.add_parser("romcheck", help="assert a rebuilt ROM matches the original (build oracle)")
    r.add_argument("--rom", required=True, help="the rebuilt ROM")
    r.add_argument("--expect-file", required=True, dest="expect_file",
                   help="the original ROM to compare against")
    r.set_defaults(fn=cmd_romcheck)

    a = p.parse_args(argv)
    if getattr(a, "search", None) is None and a.cmd in ("seed", "compile", "verify"):
        a.search = ["assets/src"]
    return a.fn(a)


if __name__ == "__main__":
    sys.exit(main())

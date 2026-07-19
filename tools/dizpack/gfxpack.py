#!/usr/bin/env python3
"""
gfxpack — SNES planar graphics <-> indexed PNG, with byte-identical round-trip.

Part of "dizpack", the stock codec toolset that DiztinGUIsh vendors into a game repo
on export. The game repo must NEVER need Diz at build time — only Python 3 and the
packages in requirements.txt (Pillow, for PNG I/O).

Design invariants (do not break — byte-identity depends on them):
  * The MANIFEST is the authority. The PNG is dumb pixels.
  * COLOR-INDEX PASSTHROUGH: a PNG pixel value IS the raw tile pixel index. The PNG's
    embedded palette is viewer-only and is IGNORED on compile. Indices — not PNG
    bytes — are canonical: the same PNG may be re-encoded to different bytes by a
    different Pillow version, and that's fine, because compile only reads indices.
  * Assets are addressed by LOGICAL NAME + an ordered list of SEARCH ROOTS
    (first-match-wins). Today that's usually one root; mod overlays later are just
    more roots, in priority order.

PNG input notes: only INDEXED (palette-mode) PNGs are accepted — anything else is
rejected rather than silently quantized, because quantization would scramble the
indices, which are the data. Sub-byte bit depths (1/2/4-bit) and interlaced PNGs
are fine: Pillow decodes both into plain per-pixel indices.

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
import sys

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

# A "cell" generalizes the 8x8 tile: 8 pixels wide, `cell_h` rows tall. cell_h defaults to
# 8, in which case a cell IS an 8x8 tile and every formula below reduces to the classic
# tile case -- verified byte-identical for bpp 2/4/8. Fonts and other non-tile-aligned
# bitmaps (CT's main font is 8x12, 24 bytes/glyph) just declare a different cell_h.
DEFAULT_VIEW = {"order": "row_major"}


def die(msg: str) -> "None":
    print(f"gfxpack: error: {msg}", file=sys.stderr)
    raise SystemExit(2)


try:
    from PIL import Image, UnidentifiedImageError
except ImportError:
    die("Pillow is required for PNG I/O but is not installed. "
        "Run: pip install -r tools/vendor/dizpack/requirements.txt "
        "(or: pip install Pillow)")


# ======================================================================================
# PNG I/O (Pillow) — indexed/palette mode ("P") only.
#
# Determinism note: for a GIVEN Pillow version, saving the same indices produces the
# same PNG bytes (no randomized options are used). Different Pillow versions may encode
# the same image to different bytes — that's accepted, because the pixel INDICES are
# canonical, not the PNG file bytes.
# ======================================================================================
def png_write_indexed(path: str, width: int, height: int,
                      indices: bytes, palette_rgb: "list[tuple[int, int, int]]") -> None:
    """Write an indexed PNG. `indices` is width*height raw index bytes."""
    if len(indices) != width * height:
        die(f"internal: index buffer {len(indices)} != {width}*{height}")
    img = Image.frombytes("P", (width, height), bytes(indices))
    img.putpalette([c for rgb in palette_rgb for c in rgb])
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    img.save(path, format="PNG")


def png_read_indexed(path: str) -> "tuple[int, int, bytes]":
    """Read an indexed PNG -> (width, height, index bytes, one byte per pixel).

    Pillow transparently decodes 1/2/4/8-bit indexed PNGs (image editors often re-save
    low-color images at a reduced depth) and interlaced PNGs into plain per-pixel
    indices, so both are accepted. The palette is deliberately ignored: we return raw
    indices, which is what makes the round-trip exact.
    """
    try:
        img = Image.open(path)
    except UnidentifiedImageError:
        die(f"{path}: not a PNG (not any recognizable image format)")
    with img:
        if img.format != "PNG":
            die(f"{path}: is a {img.format} file, not a PNG; re-save it as PNG")
        if img.mode != "P":
            die(f"{path}: image mode '{img.mode}'; an INDEXED (palette) PNG is required "
                f"so that pixel values are raw indices. Re-save as indexed/palette mode "
                f"(auto-converting here could silently scramble the indices).")
        width, height = img.size
        return width, height, img.tobytes()


# ======================================================================================
# SNES planar tile codec
#
# A tile is 8x8. Bitplanes are stored in PAIRS: for each pair, 8 rows of 2 bytes
# (low-plane byte, high-plane byte). 2bpp = 1 pair (16 bytes); 4bpp = 2 pairs (32 bytes,
# i.e. two stacked 2bpp halves); 8bpp = 4 pairs (64 bytes). Bit 7 is the LEFTMOST pixel.
# ======================================================================================
def cell_size(bpp: int, cell_h: int = TILE_H) -> int:
    """Bytes per cell. At cell_h=8 this is the classic tile size (bpp*8)."""
    return bpp * cell_h


def tile_size(bpp: int) -> int:
    """Back-compat alias: an 8-row cell."""
    return cell_size(bpp, TILE_H)


def decode_cell(buf: bytes, off: int, bpp: int, cell_h: int = TILE_H) -> "list[int]":
    """Decode one cell -> 8*cell_h palette indices, row-major.

    Bitplane pairs are stacked across the WHOLE cell, so the pair stride is 2*cell_h
    (= 16 at cell_h=8, matching the tile codec exactly).
    """
    px = [0] * (8 * cell_h)
    for pair in range(bpp // 2):
        base = off + pair * 2 * cell_h
        lo_shift = pair * 2
        for y in range(cell_h):
            lo = buf[base + y * 2]
            hi = buf[base + y * 2 + 1]
            row = y * 8
            for x in range(8):
                bit = 7 - x
                v = ((lo >> bit) & 1) | (((hi >> bit) & 1) << 1)
                px[row + x] |= v << lo_shift
    return px


def encode_cell(px: "list[int]", bpp: int, cell_h: int = TILE_H) -> bytes:
    """Encode 8*cell_h palette indices -> planar bytes (inverse of decode_cell)."""
    out = bytearray(cell_size(bpp, cell_h))
    for pair in range(bpp // 2):
        base = pair * 2 * cell_h
        lo_shift = pair * 2
        for y in range(cell_h):
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


# ======================================================================================
# View: cell index -> position on the PNG canvas.
#
# PURELY COSMETIC. The .bin is always the cells in stream order; a view only decides where
# each cell is DRAWN, so an artist can see a font/sprite the way it's meant to read. The
# view is applied identically on extract and compile, so the round-trip stays byte-exact.
#
# Correctness rests on ONE property: the mapping must be a BIJECTION (a permutation --
# every cell lands on its own canvas slot, no two collide). If two cells shared a slot,
# compile would silently read the same pixels twice and drop the other cell's data. That is
# why resolve_view validates rather than trusting the manifest.
# ======================================================================================
def resolve_view(view: dict, count: int, layout_w: int) -> "tuple[list[int], int, int]":
    """-> (positions, grid_w, grid_h) in CELLS. positions[i] = canvas slot of cell i."""
    order = view.get("order", "row_major")

    if order == "row_major":
        grid_w = layout_w
        grid_h = (count + grid_w - 1) // grid_w
        positions = list(range(count))

    elif order == "column_major":
        # Reading DOWN a column gives consecutive cells:  0 3 6 / 1 4 7 / 2 5 8
        grid_h = view.get("rows") or layout_w
        if grid_h < 1:
            die(f"view.rows must be >= 1, got {grid_h!r}")
        grid_w = (count + grid_h - 1) // grid_h
        positions = [(i % grid_h) * grid_w + (i // grid_h) for i in range(count)]

    elif order == "explicit":
        positions = view.get("cells")
        if not isinstance(positions, list) or len(positions) != count:
            die(f"view.cells must be a list of exactly {count} canvas slots "
                f"(got {len(positions) if isinstance(positions, list) else type(positions).__name__})")
        if not all(isinstance(p, int) and p >= 0 for p in positions):
            die("view.cells must contain only non-negative integers")
        # The canvas GROWS to fit the highest slot used, so gaps are legal -- a font may
        # deliberately leave holes. Nothing here bounds how sparse a view may be; a typo'd
        # large slot yields a mostly-empty canvas rather than an error.
        grid_w = layout_w
        grid_h = (max(positions) + grid_w) // grid_w if positions else 0

    else:
        die(f"view.order '{order}' is not implemented "
            f"(supported: row_major, column_major, explicit). Refusing to guess.")

    # The bijection check. Everything above is only safe because of this.
    if len(set(positions)) != len(positions):
        dupes = sorted({p for p in positions if positions.count(p) > 1})[:5]
        die(f"view maps two or more cells to the same canvas slot {dupes} -- the mapping "
            f"must be a permutation, or compiling would silently discard pixel data.")
    # Internal invariant. Unreachable for the modes above (each sizes its own canvas), but
    # kept so a future view mode can't quietly place a cell off-canvas.
    limit = grid_w * grid_h
    if positions and max(positions) >= limit:
        die(f"internal: view places a cell at slot {max(positions)} but the canvas only "
            f"holds {limit} cells ({grid_w}x{grid_h})")
    return positions, grid_w, grid_h


def cell_origin(pos: int, grid_w: int, cell_h: int) -> "tuple[int, int]":
    """Pixel origin of canvas slot `pos`. SINGLE source of truth for placement --
    extract and compile MUST agree here or the round-trip silently corrupts."""
    return (pos % grid_w) * TILE_W, (pos // grid_w) * cell_h


def bytes_to_indices(blob: bytes, bpp: int, tiles: int, layout_w: int,
                     cell_h: int = TILE_H,
                     view: dict = None) -> "tuple[int, int, bytes]":
    """Planar cell bytes -> a (width, height, indices) image laid out on the canvas."""
    positions, grid_w, grid_h = resolve_view(view or DEFAULT_VIEW, tiles, layout_w)
    width, height = grid_w * TILE_W, grid_h * cell_h
    img = bytearray(width * height)  # unused slots in a ragged last row stay 0
    cs = cell_size(bpp, cell_h)
    for t in range(tiles):
        px = decode_cell(blob, t * cs, bpp, cell_h)
        tx, ty = cell_origin(positions[t], grid_w, cell_h)
        for y in range(cell_h):
            dst = (ty + y) * width + tx
            img[dst:dst + TILE_W] = bytes(px[y * 8:(y + 1) * 8])
    return width, height, bytes(img)


def validate_pixel_indices(img: bytes, width: int, bpp: int, path: str) -> None:
    """Reject any pixel index that doesn't fit in `bpp` bits.

    Must run before encoding: encode_tile masks each index down to `bpp` bits, so an
    out-of-range pixel would otherwise be silently truncated into a wrong-but-plausible
    tile. Reports the first offending pixel in raster order.
    """
    maxv = (1 << bpp) - 1
    if not img or max(img) <= maxv:
        return
    i = next(i for i, v in enumerate(img) if v > maxv)
    x, y = i % width, i // width
    die(f"{path}: pixel ({x}, {y}) has palette index {img[i]}, but {bpp}bpp only allows "
        f"indices 0..{maxv}. The edited PNG uses more palette slots than the format "
        f"has -- repaint that pixel with an in-range palette entry.")


def indices_to_bytes(img: bytes, width: int, bpp: int, tiles: int, layout_w: int,
                     cell_h: int = TILE_H, view: dict = None) -> bytes:
    """Image indices -> planar cell bytes (inverse of bytes_to_indices).

    Callers feeding user-edited pixels must run validate_pixel_indices first
    (compile_asset does); encode_cell silently masks out-of-range values.
    """
    positions, grid_w, _ = resolve_view(view or DEFAULT_VIEW, tiles, layout_w)
    out = bytearray()
    for t in range(tiles):
        tx, ty = cell_origin(positions[t], grid_w, cell_h)
        px = []
        for y in range(cell_h):
            src = (ty + y) * width + tx
            px.extend(img[src:src + TILE_W])
        out += encode_cell(px, bpp, cell_h)
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

    gfx = dict(man.get("gfx") or {})

    # "options" is Diz's free-form passthrough: whatever the author typed into the region
    # editor, verbatim. Diz does not own this vocabulary, so it does not validate it --
    # everything below does. Shallow-merged OVER gfx, so options wins on conflict.
    options = man.get("options")
    if options is not None:
        if not isinstance(options, dict):
            die(f"{path}: 'options' must be a JSON object, got {type(options).__name__}")
        gfx.update(options)
    man["gfx"] = gfx

    bpp = gfx.get("bpp")
    if bpp not in SUPPORTED_BPP:
        die(f"{path}: gfx.bpp must be one of {SUPPORTED_BPP}, got {bpp!r}")
    if typ != f"gfx.snes.{bpp}bpp":
        die(f"{path}: type '{typ}' disagrees with gfx.bpp={bpp}")
    if gfx.get("tile_w", TILE_W) != TILE_W:
        die(f"{path}: only {TILE_W}-pixel-wide cells are supported (gfx.tile_w)")
    # cell_h generalizes tile_h: 8 = a classic tile, anything else = a taller bitmap cell
    # (e.g. a 12-row font glyph). tile_h is still accepted as the legacy spelling.
    cell_h = gfx.get("cell_h", gfx.get("tile_h", TILE_H))
    if not isinstance(cell_h, int) or cell_h < 1:
        die(f"{path}: gfx.cell_h must be a positive integer, got {cell_h!r}")
    gfx["cell_h"] = cell_h
    if not gfx.get("tiles"):
        die(f"{path}: gfx.tiles is required")

    view = gfx.get("view") or DEFAULT_VIEW
    if not isinstance(view, dict):
        die(f"{path}: gfx.view must be an object, got {type(view).__name__}")
    # Validate the mapping NOW (bijection + in-bounds) rather than letting a bad view
    # surface later as a confusing byte mismatch from the round-trip self-check.
    resolve_view(view, gfx["tiles"], gfx.get("layout_width_tiles", 16))
    gfx["view"] = view

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
    cell_h, view = gfx["cell_h"], gfx["view"]

    width, height, img = png_read_indexed(ppath)

    # Pinned invariants: reject edits that changed the geometry.
    _, grid_w, grid_h = resolve_view(view, tiles, layout_w)
    exp_w, exp_h = grid_w * TILE_W, grid_h * cell_h
    if (width, height) != (exp_w, exp_h):
        die(f"{ppath}: image is {width}x{height} but the manifest requires {exp_w}x{exp_h} "
            f"({tiles} cells of 8x{cell_h}, view '{view.get('order', 'row_major')}'). "
            f"Keep the canvas size unchanged.")

    validate_pixel_indices(img, width, bpp, ppath)
    blob = indices_to_bytes(img, width, bpp, tiles, layout_w, cell_h, view)

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
    cell_h = a.cell_height
    if cell_h < 1:
        die("--cell-height must be >= 1")
    view = {"order": a.view_order}
    if a.view_order == "column_major":
        view["rows"] = a.view_rows or a.layout_width
    cs = cell_size(bpp, cell_h)
    if length % cs:
        die(f"length {length} is not a multiple of the {bpp}bpp cell size "
            f"({cs} = {bpp}bpp x {cell_h} rows)")

    blob = rom[off:off + length]
    tiles = length // cs
    layout_w = a.layout_width
    sha = hashlib.sha256(blob).hexdigest()

    width, height, img = bytes_to_indices(blob, bpp, tiles, layout_w, cell_h, view)
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
    # Only record the generalized fields when they differ from the defaults, so ordinary
    # tile sheets keep producing exactly the manifests they always did. When cell_h is not
    # 8 the legacy tile_h is dropped rather than left contradicting it.
    if cell_h != TILE_H:
        del man["gfx"]["tile_h"]
        man["gfx"]["cell_h"] = cell_h
    if view != DEFAULT_VIEW:
        man["gfx"]["view"] = view
    if a.snes_addr:
        man["source"]["snes_addr"] = a.snes_addr
    os.makedirs(os.path.dirname(os.path.abspath(man_path)), exist_ok=True)
    with open(man_path, "w", encoding="utf-8") as f:
        json.dump(man, f, indent=2)
        f.write("\n")

    geom = f"{tiles} cells of 8x{cell_h}" if cell_h != TILE_H else f"{tiles} tiles"
    print(f"extracted {geom} ({length} bytes, {bpp}bpp) from 0x{off:X}")
    print(f"  png      : {png_path}  ({width}x{height})")
    print(f"  manifest : {man_path}")
    print(f"  sha256   : {sha}")
    if view != DEFAULT_VIEW:
        print(f"  view     : {view['order']} (cosmetic; .bin is unaffected)")

    # Immediate self-check: the asset we just wrote must rebuild to the original bytes.
    back = indices_to_bytes(img, width, bpp, tiles, layout_w, cell_h, view)
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
    cell_h, view = gfx["cell_h"], gfx["view"]
    cs = cell_size(bpp, cell_h)

    # The manifest is a claim about these bytes. Check it rather than trusting it --
    # a wrong bpp/cell_h/cell count here produces a plausible-looking but wrong image.
    if len(blob) != tiles * cs:
        die(f"{bin_path}: {len(blob)} bytes, but the manifest describes {tiles} cells of "
            f"8x{cell_h} at {bpp}bpp ({tiles * cs} bytes). "
            f"The manifest does not match the data.")

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

    width, height, img = bytes_to_indices(blob, bpp, tiles, layout_w, cell_h, view)
    png_write_indexed(ppath, width, height, img, default_palette(bpp))

    back = indices_to_bytes(img, width, bpp, tiles, layout_w, cell_h, view)
    if back != blob:
        die("SELF-CHECK FAILED: re-encoding the seeded image did not reproduce the "
            "source bytes. Do not trust this asset.")

    geom = f"{tiles} cells of 8x{cell_h}" if cell_h != TILE_H else f"{tiles} tiles"
    print(f"seeded {a.name} from layer '{layer}' ({geom}, {bpp}bpp)")
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
    e.add_argument("--cell-height", type=int, default=TILE_H, dest="cell_height",
                   help="pixel rows per cell (default 8 = a classic tile). Use e.g. 12 for "
                        "a non-tile-aligned bitmap font. Changes how bytes group into "
                        "pixels -- a wrong value gives wrong pixels, same as --bpp")
    e.add_argument("--view-order", default="row_major", dest="view_order",
                   choices=("row_major", "column_major"),
                   help="cosmetic PNG layout; does not affect the .bin (default row_major)")
    e.add_argument("--view-rows", type=int, default=None, dest="view_rows",
                   help="cells per column when --view-order=column_major")
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

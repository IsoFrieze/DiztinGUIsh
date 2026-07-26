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
  * Assets are addressed by LOGICAL NAME and resolved against ordered COMPLETE-BUNDLE
    roots (`--search`, highest priority first: mod layers, then the hand-authored
    layer) and finally a base PAIR — manifests from `--base-manifests`, content from
    `--base-content`. See resolve_asset.

PNG input notes: only INDEXED (palette-mode) PNGs are accepted — anything else is
rejected rather than silently quantized, because quantization would scramble the
indices, which are the data. Sub-byte bit depths (1/2/4-bit) and interlaced PNGs
are fine: Pillow decodes both into plain per-pixel indices.

Commands:
  extract  manifest + ROM   -> the editable PNG (ROM ground truth; re-runnable)
  fork     effective bundle -> a private copy under <mods>/<mod>/ (manifest + PNG)
  compile  png + manifest   -> raw .bin
  verify   png + manifest   -> compile and assert sha256 matches the manifest
                               (and optionally the live ROM bytes)
  romcheck rebuilt ROM      -> assert it matches the original (the build's oracle)
  selftest                  -> codec + layering assertions; needs no repo (CI gate)

Example (an uncompressed 2bpp font sheet):
  gfxpack.py extract --manifest generated/assets/gfx/font.json \
                     --rom rom/ct-us-orig.sfc --out extracted/gfx/font.png
  gfxpack.py fork    --name gfx/font --mod mymod
  gfxpack.py compile --name gfx/font --out build/assets/gfx/font.bin
  gfxpack.py verify  --name gfx/font --rom rom/ct-us-orig.sfc
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
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

# --------------------------------------------------------------------------------------
# Layering defaults (same scheme as the other codecs; see resolve_asset).
#   search roots -- ordered, complete-bundle layers: mod overlays first, hand-authored last
#   base pair    -- manifests from the exporter's output dir, content from the dir the
#                   `extract` command fills from the ROM
# --------------------------------------------------------------------------------------
DEFAULT_SEARCH_ROOTS = ("assets",)
DEFAULT_BASE_MANIFESTS = "generated/assets"
DEFAULT_BASE_CONTENT = "extracted"
DEFAULT_MODS_DIR = "mods"

# The editable content file for a graphics asset. One extension, always.
CONTENT_EXT = ".png"

# PNG encoder settings. Both are pinned rather than left to Pillow's defaults because the
# extracted PNGs are a tracked, diffable artifact: the same indices must produce the same
# file bytes on every machine and every run, or every re-extract becomes diff churn.
# `optimize` is off because it selects filters/levels heuristically; `compress_level` is
# stated outright so a change in Pillow's default cannot silently rewrite every asset.
# Nothing here affects correctness -- compile reads pixel INDICES, never PNG bytes.
PNG_COMPRESS_LEVEL = 9
PNG_OPTIMIZE = False

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
# Determinism: writing the same indices must produce the same file bytes, so the encoder
# options are pinned (see PNG_COMPRESS_LEVEL) and NO ancillary chunks are written. In
# particular no `pnginfo=` is passed, which is what keeps Pillow from stamping a `tIME`
# chunk — a timestamp would make every re-extract a spurious diff. Different Pillow
# versions may still encode the same image to different bytes; that is accepted, because
# the pixel INDICES are canonical, not the PNG file bytes.
# ======================================================================================
def png_write_indexed(path: str, width: int, height: int,
                      indices: bytes, palette_rgb: "list[tuple[int, int, int]]") -> None:
    """Write an indexed PNG. `indices` is width*height raw index bytes."""
    if len(indices) != width * height:
        die(f"internal: index buffer {len(indices)} != {width}*{height}")
    img = Image.frombytes("P", (width, height), bytes(indices))
    img.putpalette([c for rgb in palette_rgb for c in rgb])
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    img.save(path, format="PNG",
             optimize=PNG_OPTIMIZE, compress_level=PNG_COMPRESS_LEVEL)


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
def resolve_asset(name: str, search_roots: "list[str]", base_manifests: str,
                  base_content: str, ext: str = CONTENT_EXT) -> "tuple[str, str, str]":
    """Resolve a logical asset name -> (manifest_path, png_path, layer_label).

    Per-asset-BUNDLE resolution: a manifest and its PNG must describe each other, so they
    must come from the same place. An override layer supplying only the PNG would be
    decoded with someone else's bpp/geometry -- pixels and format mismatched, silently.

    1. Walk `search_roots` in priority order. A root matches only if BOTH
       <root>/<name>.json and <root>/<name><ext> exist there. A root holding exactly one
       half is a misconfiguration and HALTS -- skipping it would quietly build something
       other than what the layer was created to change.
    2. Otherwise fall back to the base PAIR: manifest from `base_manifests`, PNG from
       `base_content`. Those two directories are one logical bundle that the repo layout
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
                f"the manifest and its PNG must come from the same layer. Add the missing "
                f"file (see the `fork` command) or remove the other one.")
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
    """Find a SHARED file by repo-relative ref: plain first-match-wins over the search
    roots in priority order, then the base content dir, then the base manifest dir.
    Shared files are not bundles -- nothing pairs with them -- so the same-layer rule
    does not apply."""
    roots = [*search_roots, base_content, base_manifests]
    for root in roots:
        p = os.path.join(root, ref)
        if os.path.isfile(p):
            return p
    die(f"file '{ref}' not found in any layer: {roots}")


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


def compile_asset(name: str, search_roots: "list[str]", base_manifests: str,
                  base_content: str) -> "tuple[bytes, dict, str]":
    """Resolve, read PNG, and encode to raw planar bytes. Returns (blob, manifest, layer)."""
    mpath, ppath, layer = resolve_asset(name, search_roots, base_manifests, base_content)
    man = load_manifest(mpath)

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
    """ROM -> the editable PNG, driven by ONE explicit manifest.

    Extraction is ROM ground truth, so it deliberately does not resolve the manifest
    through the layer search: a mod override must not be able to change what "the original
    data" means. There is no edited copy to protect here (edits live in a mod layer), so
    extract simply overwrites and is always safe to re-run. Its output is byte-deterministic
    for a given Pillow version -- see png_write_indexed.
    """
    man = load_manifest(a.manifest)
    blob = read_rom_slice(a.rom, man, a.manifest)

    gfx = man["gfx"]
    bpp, tiles, layout_w = gfx["bpp"], gfx["tiles"], gfx.get("layout_width_tiles", 16)
    cell_h, view = gfx["cell_h"], gfx["view"]
    cs = cell_size(bpp, cell_h)

    # The manifest is a claim about these bytes. Check it rather than trusting it -- a
    # wrong bpp/cell_h/cell count would produce a plausible-looking but wrong image.
    if len(blob) != tiles * cs:
        die(f"{a.manifest}: describes {tiles} cells of 8x{cell_h} at {bpp}bpp "
            f"({tiles * cs} bytes) but source.length is {len(blob)}. The manifest "
            f"contradicts itself; decoding it would produce wrong pixels.")

    width, height, img = bytes_to_indices(blob, bpp, tiles, layout_w, cell_h, view)
    png_write_indexed(a.out, width, height, img, default_palette(bpp))

    # Immediate self-check: the asset we just wrote must rebuild to the original bytes.
    back = indices_to_bytes(img, width, bpp, tiles, layout_w, cell_h, view)
    if back != blob:
        die("SELF-CHECK FAILED: re-encoding the extracted image did not reproduce the "
            "source bytes. Do not trust this asset.")

    src = man["source"]
    geom = f"{tiles} cells of 8x{cell_h}" if cell_h != TILE_H else f"{tiles} tiles"
    print(f"extracted {man['name']} ({geom}, {len(blob)} bytes, {bpp}bpp) "
          f"from {a.rom} @{src['rom_offset']}")
    print(f"  manifest : {a.manifest}")
    print(f"  png      : {a.out}  ({width}x{height})")
    print(f"  sha256   : {src['source_sha256']}  [matches ROM]")
    if view != DEFAULT_VIEW:
        print(f"  view     : {view.get('order')} (cosmetic; the .bin is unaffected)")
    print("  self-check: re-encode reproduces source bytes exactly  [OK]")
    return 0


def cmd_fork(a) -> int:
    """Copy the currently-effective bundle into a mod layer, so it can be edited there.

    Resolution is exactly compile's, so `fork` always branches from whatever the build is
    using right now -- including an already-forked lower-priority mod. Both halves are
    copied together: that is what keeps the complete-bundle rule satisfiable by hand.

    It never overwrites. A second fork onto an edited copy would destroy the artist's work,
    and there is no way to tell that apart from a legitimate re-fork.
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
    print(f"  png      : {cpath}  ->  {dst_c}")
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


# ======================================================================================
# selftest — codec, determinism and layering assertions against throwaway temp trees.
# Needs no repo and no ROM: this is the public-CI gate.
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


def _demo_blob(bpp: int, tiles: int, cell_h: int = TILE_H) -> bytes:
    """Deterministic pseudo-random planar bytes, enough to exercise every bitplane."""
    n = cell_size(bpp, cell_h) * tiles
    return bytes((i * 37 + 11) & 0xFF for i in range(n))


def _selftest_codec() -> None:
    """decode/encode are exact inverses, and the view mapping is validated not trusted."""
    for bpp in SUPPORTED_BPP:
        for cell_h in (TILE_H, 12):
            blob = _demo_blob(bpp, 7, cell_h)
            w, h, img = bytes_to_indices(blob, bpp, 7, 4, cell_h, DEFAULT_VIEW)
            assert indices_to_bytes(img, w, bpp, 7, 4, cell_h, DEFAULT_VIEW) == blob, \
                f"planar round-trip failed at {bpp}bpp, cell_h={cell_h}"
            assert max(img) <= (1 << bpp) - 1, f"{bpp}bpp produced an out-of-range index"

    # column_major is a permutation of the same cells, so it must round-trip too, and it
    # must NOT change the compiled bytes -- the view is cosmetic.
    view = {"order": "column_major", "rows": 3}
    blob = _demo_blob(4, 9)
    w, h, img = bytes_to_indices(blob, 4, 9, 3, TILE_H, view)
    assert indices_to_bytes(img, w, 4, 9, 3, TILE_H, view) == blob, "column_major round-trip"

    # A view that maps two cells onto one slot would silently discard pixels: refuse it.
    _expect_die(lambda: resolve_view({"order": "explicit", "cells": [0, 0, 1]}, 3, 4),
                "same canvas slot", "a non-bijective explicit view")
    _expect_die(lambda: resolve_view({"order": "diagonal"}, 3, 4),
                "not implemented", "an unknown view order")
    # An index too large for the declared bpp must be caught before encode_cell masks it.
    _expect_die(lambda: validate_pixel_indices(bytes([0, 1, 9]), 3, 2, "selftest"),
                "only allows", "an out-of-range pixel index")


def _selftest_png_determinism() -> None:
    """Encoding the same indices twice must produce byte-identical PNG files."""
    import tempfile
    tmp = tempfile.mkdtemp(prefix="gfxpack-png-")
    blob = _demo_blob(4, 12)
    w, h, img = bytes_to_indices(blob, 4, 12, 4)
    a = os.path.join(tmp, "a.png")
    b = os.path.join(tmp, "b.png")
    png_write_indexed(a, w, h, img, default_palette(4))
    png_write_indexed(b, w, h, img, default_palette(4))
    first, second = open(a, "rb").read(), open(b, "rb").read()
    assert first == second, "PNG encoding is not byte-deterministic across runs"
    # No timestamp chunk: a tIME would make every re-extract a spurious diff.
    assert b"tIME" not in first, "PNG carries a tIME chunk, which is not deterministic"
    # And the pixels survive the file round-trip.
    rw, rh, rimg = png_read_indexed(a)
    assert (rw, rh, rimg) == (w, h, img), "PNG write/read did not preserve the indices"


def _selftest_layering() -> None:
    """Resolution: mod bundle wins, half a bundle halts, base pair is the fallback."""
    import tempfile
    tmp = tempfile.mkdtemp(prefix="gfxpack-layers-")
    R = lambda *p: os.path.join(tmp, *p)
    N = os.path.normpath
    name = "gfx/demo"
    gen, content = R("generated", "assets"), R("extracted")
    assets, mod = R("assets"), R("mods", "m")
    roots = [mod, assets]

    # 1. base pair: manifest and PNG live in DIFFERENT directories and still resolve.
    _mkfile(R("generated", "assets", "gfx", "demo.json"), "{}")
    _mkfile(R("extracted", "gfx", "demo.png"), b"")
    m, c, layer = resolve_asset(name, roots, gen, content)
    assert (N(m), N(c)) == (N(R("generated", "assets", "gfx", "demo.json")),
                            N(R("extracted", "gfx", "demo.png"))), \
        f"base-pair fallback: {(m, c)}"

    # 2. a complete bundle in a search root outranks the base pair...
    _mkfile(R("assets", "gfx", "demo.json"), "{}")
    _mkfile(R("assets", "gfx", "demo.png"), b"")
    assert resolve_asset(name, roots, gen, content)[2] == assets, \
        "assets bundle should win over the base pair"

    # 3. ...and a higher-priority mod bundle outranks that.
    _mkfile(R("mods", "m", "gfx", "demo.json"), "{}")
    _mkfile(R("mods", "m", "gfx", "demo.png"), b"")
    assert resolve_asset(name, roots, gen, content)[2] == mod, "mod bundle should win"

    # 4. half a bundle in a search root HALTS -- it must never be silently skipped.
    os.remove(R("mods", "m", "gfx", "demo.png"))
    _expect_die(lambda: resolve_asset(name, roots, gen, content),
                "half", "manifest-only mod layer")
    os.remove(R("mods", "m", "gfx", "demo.json"))
    _mkfile(R("mods", "m", "gfx", "demo.png"), b"")
    _expect_die(lambda: resolve_asset(name, roots, gen, content),
                "half", "PNG-only mod layer")
    os.remove(R("mods", "m", "gfx", "demo.png"))

    # 5. a base pair missing either half halts too.
    os.remove(R("assets", "gfx", "demo.json"))
    os.remove(R("assets", "gfx", "demo.png"))
    os.remove(R("extracted", "gfx", "demo.png"))
    _expect_die(lambda: resolve_asset(name, roots, gen, content),
                "not found", "base pair without content")

    # 6. shared files are NOT bundles: plain first-match over search roots, then the base
    #    content dir, then the base manifest dir.
    found = lambda: N(resolve_file("gfx/palette.pal", roots, content, gen))
    _mkfile(R("generated", "assets", "gfx", "palette.pal"), "")
    assert found() == N(R("generated", "assets", "gfx", "palette.pal")), "from base manifests"
    _mkfile(R("extracted", "gfx", "palette.pal"), "")
    assert found() == N(R("extracted", "gfx", "palette.pal")), "base content outranks manifests"
    _mkfile(R("assets", "gfx", "palette.pal"), "")
    assert found() == N(R("assets", "gfx", "palette.pal")), "search root outranks the base"
    _mkfile(R("mods", "m", "gfx", "palette.pal"), "")
    assert found() == N(R("mods", "m", "gfx", "palette.pal")), "mod root outranks assets"
    _expect_die(lambda: resolve_file("gfx/nope.pal", roots, content, gen),
                "not found", "unresolvable shared file")


def _selftest_extract_fork() -> None:
    """extract: ROM slice + sha gate + determinism. fork: copies both halves, once only."""
    import tempfile
    tmp = tempfile.mkdtemp(prefix="gfxpack-extract-")
    R = lambda *p: os.path.join(tmp, *p)
    gen, content, assets = R("generated", "assets"), R("extracted"), R("assets")

    bpp, tiles, layout_w = 4, 12, 4
    data = _demo_blob(bpp, tiles)
    rom = _mkfile(R("fake.sfc"), bytes(0x10) + data + bytes(0x10))
    good = hashlib.sha256(data).hexdigest()
    mpath = R("generated", "assets", "gfx", "demo.json")

    def manifest(sha, count=tiles):
        return _mkfile(mpath, json.dumps({
            "name": "gfx/demo", "type": f"gfx.snes.{bpp}bpp", "ver": "v1",
            "source": {"rom_offset": "0x10", "length": len(data), "source_sha256": sha},
            "gfx": {"bpp": bpp, "tile_w": TILE_W, "tile_h": TILE_H, "tiles": count,
                    "plane_order": "snes-interleaved-pairs",
                    "layout_width_tiles": layout_w},
        }, indent=2) + "\n")

    manifest(good)
    layer_args = ["--search", assets, "--base-manifests", gen, "--base-content", content]
    out = R("extracted", "gfx", "demo.png")
    run = lambda: main(["extract", "--manifest", mpath, "--rom", rom, "--out", out] + layer_args)

    _quiet(run)
    first = open(out, "rb").read()
    _quiet(run)
    assert open(out, "rb").read() == first, "extract is not byte-deterministic"

    # The extracted PNG compiles back to exactly the ROM bytes (base-pair resolution).
    blob, _, _ = compile_asset("gfx/demo", [assets], gen, content)
    assert blob == data, "extract -> compile is not byte-identical"

    # Wrong ROM: the sha gate must halt rather than decode whatever it was pointed at.
    manifest("0" * 64)
    _expect_die(lambda: _quiet(run), "source_sha256", "extract against the wrong ROM")
    # A manifest whose geometry contradicts its own length halts before decoding.
    manifest(good, count=tiles - 1)
    _expect_die(lambda: _quiet(run), "contradicts", "manifest geometry vs source.length")
    manifest(good)

    # fork copies BOTH halves out of the effective layer, and refuses to do it twice.
    fork_args = (["fork", "--name", "gfx/demo", "--mod", "m", "--mods-dir", R("mods")]
                 + layer_args)
    _quiet(lambda: main(fork_args))
    assert open(R("mods", "m", "gfx", "demo.json"), "rb").read() == open(mpath, "rb").read()
    assert open(R("mods", "m", "gfx", "demo.png"), "rb").read() == first
    _expect_die(lambda: _quiet(lambda: main(fork_args)), "overwrite", "re-forking an asset")

    # The forked bundle now outranks the base pair for compile.
    assert resolve_asset("gfx/demo", [R("mods", "m"), assets], gen, content)[2] \
        == R("mods", "m"), "forked mod should win"


def cmd_selftest(a) -> int:
    _selftest_codec()
    _selftest_png_determinism()
    _selftest_layering()
    _selftest_extract_fork()
    print("selftest: all codec, determinism and layering invariants hold  [OK]")
    return 0


def main(argv=None) -> int:
    p = argparse.ArgumentParser(
        prog="gfxpack", description="SNES planar graphics <-> indexed PNG (round-trip)")
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

    e = sub.add_parser("extract", help="manifest + ROM -> the editable PNG")
    e.add_argument("--manifest", required=True,
                   help="the manifest to extract by, e.g. generated/assets/gfx/x.json. "
                        "An explicit path, NOT a layer lookup: extraction is ROM ground "
                        "truth and must not be reachable by a mod override")
    e.add_argument("--rom", required=True, help="the original ROM to slice")
    e.add_argument("--out", required=True, help="the .png to write")
    add_layers(e)   # only used to resolve shared files
    e.set_defaults(fn=cmd_extract)

    fk = sub.add_parser("fork", help="copy the effective bundle into a mod layer")
    fk.add_argument("--name", required=True, help="logical name, e.g. gfx/font")
    fk.add_argument("--mod", required=True, help="mod layer name to fork into")
    fk.add_argument("--mods-dir", default=DEFAULT_MODS_DIR, dest="mods_dir", metavar="DIR",
                    help=f"directory holding mod layers (default {DEFAULT_MODS_DIR})")
    add_layers(fk)
    fk.set_defaults(fn=cmd_fork)

    c = sub.add_parser("compile", help="PNG + manifest -> raw .bin")
    c.add_argument("--name", required=True)
    add_layers(c)
    c.add_argument("--out", required=True)
    c.set_defaults(fn=cmd_compile)

    v = sub.add_parser("verify", help="compile and assert byte-identity")
    v.add_argument("--name", required=True)
    add_layers(v)
    v.add_argument("--rom", default=None,
                   help="also compare against live ROM bytes at the manifest offset")
    v.set_defaults(fn=cmd_verify)

    r = sub.add_parser("romcheck", help="assert a rebuilt ROM matches the original (build oracle)")
    r.add_argument("--rom", required=True, help="the rebuilt ROM")
    r.add_argument("--expect-file", required=True, dest="expect_file",
                   help="the original ROM to compare against")
    r.set_defaults(fn=cmd_romcheck)

    st = sub.add_parser("selftest", help="codec + layering assertions (needs no repo)")
    st.set_defaults(fn=cmd_selftest)

    a = p.parse_args(argv)
    if getattr(a, "search", None) is None:
        a.search = list(DEFAULT_SEARCH_ROOTS)
    return a.fn(a)


if __name__ == "__main__":
    sys.exit(main())

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
  extract  manifest + ROM -> the payload file (ROM ground truth; re-runnable)
  decode   manifest + file -> the payload file (buffer mode; no ROM)
  fork     effective bundle -> a private copy under <mods>/<mod>/ (manifest + payload)
  compile  <name><ext>    -> <out>.bin (copy), checks source_sha256
  encode   <name><ext>    -> raw bytes for one slot of a larger buffer (buffer mode)
  verify   <name><ext>    -> recompute sha and assert it matches the manifest
                             (and optionally the live ROM bytes)
  selftest                -> integrity + layering assertions; needs no repo (CI gate)

BUFFER MODE (`decode`/`encode`) exists because not every asset sits at a ROM offset. An
asset may be one MEMBER of a larger buffer -- typically the decompressed form of a
compressed container, cut into per-member files by a generic slicing tool. Such an asset's
manifest is an ordinary leaf manifest whose `source` block records
`{length, source_sha256, member_of, at}` and has NO `rom_offset`: the hash is of the
member's own (decompressed) bytes. `decode` is `extract` with the ROM slice replaced by a
verbatim file read; `encode` is `compile` writing the bytes that belong in that buffer slot.
`encode` takes `--name`, not an input path, so a mod layer overrides a member exactly as it
overrides any other asset.

The payload extension is resolved as: explicit `--ext` > the `ext` recorded in the
manifest's type block > `.bin`. So a ninja rule may pass `--ext .brr` OR rely on the
manifest -- both work.

Layering: an asset is addressed by LOGICAL NAME and resolved against ordered
COMPLETE-BUNDLE roots (`--search`, highest priority first: mod layers, then the
hand-authored layer) and finally a base PAIR — manifests from `--base-manifests`,
content from `--base-content`. See resolve_asset.

Example (a BRR sample, verbatim from a ROM offset):
  binpack.py extract --manifest generated/assets/audio/AudioBRR_00.json \
                     --rom rom/ct-us-orig.sfc --out extracted/audio/AudioBRR_00.brr
  binpack.py fork    --name audio/AudioBRR_00 --mod mymod
  binpack.py compile --name audio/AudioBRR_00 --out build/assets/audio/AudioBRR_00.bin
  binpack.py verify  --name audio/AudioBRR_00 --rom rom/ct-us-orig.sfc

Example (the same sample as a member of a decompressed container buffer):
  binpack.py decode  --manifest generated/assets/audio/AudioBRR_00.json \
      --in build/extract/audio/AudioBRR_00.bin --out extracted/audio/AudioBRR_00.brr
  binpack.py encode  --name audio/AudioBRR_00 --out build/encode/audio/AudioBRR_00.bin
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
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
def resolve_ext(name: str, search_roots: "list[str]", base_manifests: str,
                override: "str | None") -> str:
    """Decide which payload extension this asset uses, BEFORE resolving the bundle.

    Unlike the other codecs, binpack's content extension is not fixed by the tool -- it is
    declared per asset. So the extension has to be settled first, from the highest-priority
    manifest that exists anywhere in the search path; only then can 'is this layer holding
    a complete bundle?' even be asked.
    """
    if override:
        if not override.startswith("."):
            die(f"--ext must start with a dot, got {override!r}")
        return override
    for root in [*search_roots, base_manifests]:
        mpath = os.path.join(root, name + ".json")
        if os.path.isfile(mpath):
            return manifest_ext(load_manifest(mpath), None)
    return DEFAULT_EXT


def resolve_asset(name: str, search_roots: "list[str]", base_manifests: str,
                  base_content: str, ext: str) -> "tuple[str, str, str]":
    """Resolve a logical asset name -> (manifest_path, payload_path, layer_label).

    Per-asset-BUNDLE resolution: a manifest and its payload must describe each other, so
    they must come from the same place. An override layer holding only the payload would be
    checked against the stock manifest's sha256 (and fail, or worse, pass against the wrong
    length); one holding only the manifest would validate someone else's bytes. Both are
    refused.

    1. Walk `search_roots` in priority order. A root matches only if BOTH
       <root>/<name>.json and <root>/<name><ext> exist there. A root holding exactly one
       half is a misconfiguration and HALTS -- skipping it would quietly build something
       other than what the layer was created to change.
    2. Otherwise fall back to the base PAIR: manifest from `base_manifests`, payload from
       `base_content`. Those two directories are one logical bundle that the repo layout
       splits in two, which is why the same-layer rule is relaxed here and nowhere else.
       Missing either half HALTS.
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
                f"the manifest and its payload must come from the same layer. Add the "
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


def check_integrity(blob: bytes, man: dict, where: str) -> "tuple[str, bool]":
    """Check the blob against the manifest's declared source. Returns (sha256, matches).

    This is the BUILD-direction check (compile/encode). The three outcomes are deliberately
    not the same severity:

    * No `source` block at all -- nothing to check. `source` is provenance: it records that
      these bytes were extracted from a ROM. A hand-authored asset never came from one and
      omits the block entirely; absence of a claim is not violation of a claim, so the bytes
      pass and `matches` is reported True.
    * `source` present but `source_sha256` missing -- HALT. Here the claim is present and its
      proof is missing, which is a broken manifest rather than a design choice: a verbatim
      asset has no lossy view and no decode/encode round-trip, so the hash is the only thing
      that could ever check it. Re-extract, or drop the whole block if it is hand-authored.
    * `source_sha256` present but different -- NOT fatal, reported and returned False. On the
      build direction a mismatch is the normal state of an EDITED asset: `fork` copies the
      manifest along with the payload, so a forked .brr still carries the stock hash and the
      modder's first edit would otherwise be unbuildable -- while an equivalent edit to a
      .png or .yaml builds fine. Stock byte-identity is still earned, by the whole-ROM
      comparison, which never consults a manifest. The hash stays a HARD gate where it
      guards ROM ground truth: `extract` (read_rom_slice) and `verify`.

    The length check is hard in every case: a payload of the wrong size is not an edit of
    this asset at all, and would silently shift everything the assembler places after it."""
    src = man.get("source")
    if not src:
        return hashlib.sha256(blob).hexdigest(), True

    want_len = src.get("length")
    if want_len is not None and len(blob) != want_len:
        die(f"{where}: {len(blob)} bytes, but the manifest declares source.length={want_len}. "
            f"The payload does not match the data the manifest describes.")

    want_sha = src.get("source_sha256")
    if not want_sha:
        die(f"{where}: manifest declares a source block but no source.source_sha256 -- a "
            f"verbatim asset that claims ROM provenance cannot be integrity-checked without "
            f"it. Re-extract the asset, or drop the source block if it is hand-authored.")
    got_sha = hashlib.sha256(blob).hexdigest()
    if got_sha != want_sha:
        print(f"binpack: note: {where}: sha256 {got_sha} differs from the manifest's "
              f"source_sha256 {want_sha} -- building the edited bytes. Extraction and "
              f"`verify` still require an exact match.", file=sys.stderr)
        return got_sha, False
    return got_sha, True


# ======================================================================================
# Commands
# ======================================================================================
def read_rom_slice(rom_path: str, man: dict, manifest_path: str) -> bytes:
    """Read the ROM bytes a manifest describes, and prove they are the right ones.

    The `source` block is a claim about a specific cartridge. Checking its sha256 before
    writing anything is what turns "you pointed at some ROM" into a hard error instead of a
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


def read_member_buffer(in_path: str, man: dict, manifest_path: str) -> bytes:
    """Read a buffer file VERBATIM, and prove it is the data the manifest describes.

    The buffer-mode counterpart of read_rom_slice. Nothing is sliced here: the file IS the
    asset's bytes, cut out upstream (one member of a decompressed container buffer, say),
    so no ROM offset is involved. Such a manifest's `source` block records
    `{length, source_sha256, member_of, at}` and its hash is of those decompressed bytes.

    `source` is provenance and is optional -- a hand-authored asset has none, absence of a
    claim is not violation of a claim, and its bytes pass unchecked. When `source` IS
    present the checks are strict, exactly as on the ROM path: this is the extract-side
    direction, where the wrong input must never turn into a plausible-looking asset. (The
    build direction is the tolerant one -- see check_integrity.)
    """
    try:
        with open(in_path, "rb") as f:
            blob = f.read()
    except OSError as e:
        die(f"{in_path}: cannot read the input buffer: {e}")

    src = man.get("source") or {}
    if not src:
        return blob

    length = src.get("length")
    if length is not None and len(blob) != length:
        die(f"{in_path}: is {len(blob)} bytes, but {manifest_path} declares "
            f"source.length={length}. This is not the data the manifest describes -- the "
            f"buffer was cut at the wrong boundaries, or the manifest is stale.")

    want = src.get("source_sha256")
    if not want:
        die(f"{manifest_path}: source.source_sha256 is missing. Decoding is only safe "
            f"when the bytes can be proven to be the ones the manifest describes.")
    got = hashlib.sha256(blob).hexdigest()
    if got != want:
        die(f"{in_path}: bytes hash to {got}, but {manifest_path} declares source_sha256 "
            f"{want}. This is not the data the asset was exported from. Refusing to decode.")
    return blob


def write_payload(blob: bytes, out_path: str) -> None:
    """Write the payload file and read it straight back. Shared by extract and decode.

    A verbatim asset's whole correctness claim is "these are the same bytes", so the copy
    is proven rather than assumed; it is also what makes the output trivially
    byte-deterministic.
    """
    os.makedirs(os.path.dirname(os.path.abspath(out_path)), exist_ok=True)
    with open(out_path, "wb") as f:
        f.write(blob)
    with open(out_path, "rb") as f:
        back = f.read()
    if back != blob:
        die("SELF-CHECK FAILED: re-reading the extracted payload did not reproduce the "
            "source bytes. Do not trust this asset.")


def cmd_extract(a) -> int:
    """ROM -> the editable payload file, driven by ONE explicit manifest.

    Extraction is ROM ground truth, so it deliberately does not resolve the manifest
    through the layer search: a mod override must not be able to change what "the original
    data" means. There is no edited copy to protect here (edits live in a mod layer), so
    extract simply overwrites and is always safe to re-run. A verbatim copy of a verified
    slice is trivially byte-deterministic.
    """
    man = load_manifest(a.manifest)
    blob = read_rom_slice(a.rom, man, a.manifest)
    write_payload(blob, a.out)

    src = man["source"]
    print(f"extracted {man.get('name', a.manifest)} ({len(blob)} bytes, {man['type']}) "
          f"from {a.rom} @{src['rom_offset']}")
    print(f"  manifest : {a.manifest}")
    print(f"  payload  : {a.out}")
    print(f"  sha256   : {src['source_sha256']}  [matches ROM]")
    print("  self-check: payload reproduces source bytes exactly  [OK]")
    return 0


def cmd_decode(a) -> int:
    """A buffer file -> the editable payload file, driven by ONE explicit manifest.

    Buffer mode: `extract` with the ROM slice replaced by a verbatim file read. The input
    is produced upstream by the build -- one member cut out of a decompressed container
    buffer -- so this asset has no ROM offset of its own and the manifest is checked
    against the file's bytes instead. Like extract it is ground truth: the manifest is an
    explicit path, never a layer lookup, and re-running simply overwrites.

    For a verbatim asset this is a copy, exactly as extract is; the checks are the value.
    """
    man = load_manifest(a.manifest)
    blob = read_member_buffer(a.in_path, man, a.manifest)
    write_payload(blob, a.out)

    src = man.get("source") or {}
    origin = f" of {src['member_of']}" if src.get("member_of") else ""
    print(f"decoded {man.get('name', a.manifest)} ({len(blob)} bytes, {man['type']}) "
          f"from {a.in_path}{origin}")
    print(f"  manifest : {a.manifest}")
    print(f"  payload  : {a.out}")
    if src.get("source_sha256"):
        print(f"  sha256   : {src['source_sha256']}  [matches the input buffer]")
    else:
        print("  sha256   : no source block -- hand-authored, nothing to check against")
    print("  self-check: payload reproduces source bytes exactly  [OK]")
    return 0


def cmd_fork(a) -> int:
    """Copy the currently-effective bundle into a mod layer, so it can be edited there.

    Resolution is exactly compile's, so `fork` always branches from whatever the build is
    using right now -- including an already-forked lower-priority mod. Both halves are
    copied together: that is what keeps the complete-bundle rule satisfiable by hand.

    It never overwrites. A second fork onto an edited copy would destroy the edits, and
    there is no way to tell that apart from a legitimate re-fork.
    """
    ext = resolve_ext(a.name, a.search, a.base_manifests, a.ext)
    mpath, cpath, layer = resolve_asset(a.name, a.search, a.base_manifests,
                                        a.base_content, ext)
    dest_root = os.path.join(a.mods_dir, a.mod)
    dst_m = os.path.join(dest_root, a.name + ".json")
    dst_c = os.path.join(dest_root, a.name + ext)

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
    print(f"  payload  : {cpath}  ->  {dst_c}")
    print(f"  edit {dst_c}, then build with --search {dest_root} ahead of the other roots")
    return 0


def compile_asset(name: str, search_roots: "list[str]", base_manifests: str,
                  base_content: str,
                  ext_override: "str | None") -> "tuple[bytes, dict, str]":
    """Resolve and read the editable payload, asserting it still matches the manifest.
    Returns (blob, manifest, layer)."""
    ext = resolve_ext(name, search_roots, base_manifests, ext_override)
    mpath, payload_path, layer = resolve_asset(name, search_roots, base_manifests,
                                               base_content, ext)
    man = load_manifest(mpath)
    with open(payload_path, "rb") as f:
        blob = f.read()
    check_integrity(blob, man, payload_path)
    return blob, man, layer


def cmd_compile(a) -> int:
    blob, man, layer = compile_asset(a.name, a.search, a.base_manifests,
                                     a.base_content, a.ext)
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    with open(a.out, "wb") as f:
        f.write(blob)
    print(f"compiled {a.name} from layer '{layer}' -> {a.out} ({len(blob)} bytes)")
    return 0


def cmd_encode(a) -> int:
    """The editable payload -> the raw bytes this asset occupies inside a larger buffer.

    The build-direction counterpart of `decode`, and byte-for-byte the same copy as
    `compile`; the difference is what the output is FOR. `compile` emits a payload the
    assembler incbins, `encode` emits a fragment that a slicing tool concatenates back into
    a buffer (which something downstream may then recompress).

    It addresses the asset by LOGICAL NAME rather than by input path, so layer resolution
    is identical to compile's and a mod overrides a member exactly as it overrides any
    other asset. `--in-dir` only replaces the BASE content root, so mod layers still win.
    """
    base_content = a.in_dir or a.base_content
    blob, man, layer = compile_asset(a.name, a.search, a.base_manifests,
                                     base_content, a.ext)
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    with open(a.out, "wb") as f:
        f.write(blob)
    print(f"encoded {a.name} from layer '{layer}' -> {a.out} ({len(blob)} bytes)")
    return 0


def cmd_verify(a) -> int:
    ext = resolve_ext(a.name, a.search, a.base_manifests, a.ext)
    mpath, payload_path, layer = resolve_asset(a.name, a.search, a.base_manifests,
                                               a.base_content, ext)
    man = load_manifest(mpath)
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
        print("manifest   : no source_sha256 recorded -- skipped")

    # Strongest check: compare against the live ROM bytes at the recorded offset. An asset
    # that lives inside a container has no offset of its own -- its bytes only exist after
    # the container is decompressed -- so there is nothing to compare and we say so rather
    # than silently reading the ROM at 0.
    if a.rom:
        src = man.get("source") or {}
        if "rom_offset" not in src:
            print("rom        : no source.rom_offset recorded -- skipped "
                  f"(this asset is a member of {src.get('member_of', 'a container')})")
        else:
            off = int(str(src["rom_offset"]), 0)
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


# ======================================================================================
# selftest — integrity + layering assertions against throwaway temp trees. Needs no repo
# and no ROM: this is the public-CI gate.
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


def _manifest_json(sha: "str | None", length: int, off: str = "0x10") -> str:
    src = {"rom_offset": off, "length": length}
    if sha is not None:
        src["source_sha256"] = sha
    return json.dumps({
        "name": "audio/demo", "type": "audio.snes.brr", "ver": "v1",
        "source": src, "audio": {"ext": ".brr"},
    }, indent=2) + "\n"


def _selftest_integrity() -> None:
    """check_integrity: absent source is unverified, a missing hash halts, a DIFFERING
    hash is reported but builds (an edited asset is the normal reason for it)."""
    blob = bytes(range(32))
    sha = hashlib.sha256(blob).hexdigest()
    assert check_integrity(blob, {}, "selftest") == (sha, True), \
        "no source block => unverified"
    man = {"source": {"length": 32, "source_sha256": sha}}
    assert check_integrity(blob, man, "selftest") == (sha, True), "matching source block"
    # Edited bytes: not fatal on the build direction, but reported as not matching.
    edited = bytes(32)
    assert check_integrity(edited, man, "selftest") == \
        (hashlib.sha256(edited).hexdigest(), False), \
        "a sha mismatch must be reported, not fatal, on the build direction"
    # A wrong LENGTH is still fatal -- that is not an edit of this asset.
    _expect_die(lambda: check_integrity(blob + b"\x00", man, "selftest"),
                "length", "payload longer than source.length")
    # A source block whose proof was deleted is a broken manifest, not a design choice.
    _expect_die(lambda: check_integrity(blob, {"source": {"length": 32}}, "selftest"),
                "source_sha256", "source block without a hash")
    # A gfx type must be refused outright -- its payload is a PNG, not the bytes.
    import tempfile
    p = _mkfile(os.path.join(tempfile.mkdtemp(prefix="binpack-type-"), "g.json"),
                json.dumps({"type": "gfx.snes.2bpp"}))
    _expect_die(lambda: load_manifest(p), "gfxpack", "a gfx type handed to binpack")


def _selftest_layering() -> None:
    """Resolution: mod bundle wins, half a bundle halts, base pair is the fallback."""
    import tempfile
    tmp = tempfile.mkdtemp(prefix="binpack-layers-")
    R = lambda *p: os.path.join(tmp, *p)
    N = os.path.normpath
    name, ext = "audio/demo", ".brr"
    gen, content = R("generated", "assets"), R("extracted")
    assets, mod = R("assets"), R("mods", "m")
    roots = [mod, assets]
    man = _manifest_json(hashlib.sha256(b"").hexdigest(), 0)

    # 1. base pair: manifest and payload live in DIFFERENT directories and still resolve.
    _mkfile(R("generated", "assets", "audio", "demo.json"), man)
    _mkfile(R("extracted", "audio", "demo.brr"), b"")
    m, c, layer = resolve_asset(name, roots, gen, content, ext)
    assert (N(m), N(c)) == (N(R("generated", "assets", "audio", "demo.json")),
                            N(R("extracted", "audio", "demo.brr"))), \
        f"base-pair fallback: {(m, c)}"
    # the extension comes from the highest-priority manifest, with --ext overriding it
    assert resolve_ext(name, roots, gen, None) == ".brr", "ext from the manifest"
    assert resolve_ext(name, roots, gen, ".pcm") == ".pcm", "--ext overrides the manifest"
    _expect_die(lambda: resolve_ext(name, roots, gen, "brr"), "dot", "--ext without a dot")

    # 2. a complete bundle in a search root outranks the base pair...
    _mkfile(R("assets", "audio", "demo.json"), man)
    _mkfile(R("assets", "audio", "demo.brr"), b"")
    assert resolve_asset(name, roots, gen, content, ext)[2] == assets, \
        "assets bundle should win over the base pair"

    # 3. ...and a higher-priority mod bundle outranks that.
    _mkfile(R("mods", "m", "audio", "demo.json"), man)
    _mkfile(R("mods", "m", "audio", "demo.brr"), b"")
    assert resolve_asset(name, roots, gen, content, ext)[2] == mod, "mod bundle should win"

    # 4. half a bundle in a search root HALTS -- it must never be silently skipped.
    os.remove(R("mods", "m", "audio", "demo.brr"))
    _expect_die(lambda: resolve_asset(name, roots, gen, content, ext),
                "half", "manifest-only mod layer")
    os.remove(R("mods", "m", "audio", "demo.json"))
    _mkfile(R("mods", "m", "audio", "demo.brr"), b"")
    _expect_die(lambda: resolve_asset(name, roots, gen, content, ext),
                "half", "payload-only mod layer")
    os.remove(R("mods", "m", "audio", "demo.brr"))

    # 5. a base pair missing either half halts too.
    os.remove(R("assets", "audio", "demo.json"))
    os.remove(R("assets", "audio", "demo.brr"))
    os.remove(R("extracted", "audio", "demo.brr"))
    _expect_die(lambda: resolve_asset(name, roots, gen, content, ext),
                "not found", "base pair without content")

    # 6. shared files are NOT bundles: plain first-match over search roots, then the base
    #    content dir, then the base manifest dir.
    found = lambda: N(resolve_file("audio/notes.txt", roots, content, gen))
    _mkfile(R("generated", "assets", "audio", "notes.txt"), "")
    assert found() == N(R("generated", "assets", "audio", "notes.txt")), "from base manifests"
    _mkfile(R("extracted", "audio", "notes.txt"), "")
    assert found() == N(R("extracted", "audio", "notes.txt")), "base content outranks manifests"
    _mkfile(R("assets", "audio", "notes.txt"), "")
    assert found() == N(R("assets", "audio", "notes.txt")), "search root outranks the base"
    _mkfile(R("mods", "m", "audio", "notes.txt"), "")
    assert found() == N(R("mods", "m", "audio", "notes.txt")), "mod root outranks assets"
    _expect_die(lambda: resolve_file("audio/nope.txt", roots, content, gen),
                "not found", "unresolvable shared file")


def _selftest_extract_fork() -> None:
    """extract: ROM slice + sha gate + determinism. fork: copies both halves, once only."""
    import tempfile
    tmp = tempfile.mkdtemp(prefix="binpack-extract-")
    R = lambda *p: os.path.join(tmp, *p)
    gen, content, assets = R("generated", "assets"), R("extracted"), R("assets")

    data = bytes(range(64))
    rom = _mkfile(R("fake.sfc"), bytes(0x10) + data + bytes(0x10))
    good = hashlib.sha256(data).hexdigest()
    mpath = R("generated", "assets", "audio", "demo.json")
    _mkfile(mpath, _manifest_json(good, len(data)))

    layer_args = ["--search", assets, "--base-manifests", gen, "--base-content", content]
    out = R("extracted", "audio", "demo.brr")
    run = lambda: main(["extract", "--manifest", mpath, "--rom", rom, "--out", out] + layer_args)

    _quiet(run)
    first = open(out, "rb").read()
    assert first == data, "extract did not reproduce the ROM slice"
    _quiet(run)
    assert open(out, "rb").read() == first, "extract is not byte-deterministic"

    # The extracted payload compiles back to exactly the ROM bytes (base-pair resolution).
    blob, _, _ = compile_asset("audio/demo", [assets], gen, content, None)
    assert blob == data, "extract -> compile is not byte-identical"

    # Wrong ROM: the sha gate must halt rather than copy whatever it was pointed at.
    _mkfile(mpath, _manifest_json("0" * 64, len(data)))
    _expect_die(lambda: _quiet(run), "source_sha256", "extract against the wrong ROM")
    # A manifest with no hash at all cannot be extracted either.
    _mkfile(mpath, _manifest_json(None, len(data)))
    _expect_die(lambda: _quiet(run), "source_sha256", "extract without a recorded hash")
    # A manifest with no ROM provenance is not extractable.
    _mkfile(mpath, json.dumps({"name": "audio/demo", "type": "audio.snes.brr",
                               "audio": {"ext": ".brr"}}) + "\n")
    _expect_die(lambda: _quiet(run), "provenance", "extract of a hand-authored asset")
    _mkfile(mpath, _manifest_json(good, len(data)))

    # fork copies BOTH halves out of the effective layer, and refuses to do it twice.
    fork_args = (["fork", "--name", "audio/demo", "--mod", "m", "--mods-dir", R("mods")]
                 + layer_args)
    _quiet(lambda: main(fork_args))
    assert open(R("mods", "m", "audio", "demo.json"), "rb").read() == open(mpath, "rb").read()
    assert open(R("mods", "m", "audio", "demo.brr"), "rb").read() == data
    _expect_die(lambda: _quiet(lambda: main(fork_args)), "overwrite", "re-forking an asset")

    # The forked bundle now outranks the base pair for compile.
    assert resolve_asset("audio/demo", [R("mods", "m"), assets], gen, content, ".brr")[2] \
        == R("mods", "m"), "forked mod should win"


def _selftest_buffer_mode() -> None:
    """decode/encode: file in -> file out, no ROM anywhere.

    A container member's manifest carries a `source` block with NO rom_offset, so every
    check has to key off the bytes themselves. decode -> encode must be byte-identical, and
    decode must be as strict about wrong input as extract is.
    """
    import tempfile
    tmp = tempfile.mkdtemp(prefix="binpack-buffer-")
    R = lambda *p: os.path.join(tmp, *p)
    gen, content, assets = R("generated", "assets"), R("extracted"), R("assets")

    data = bytes(range(48))
    good = hashlib.sha256(data).hexdigest()
    mpath = R("generated", "assets", "audio", "member.json")

    def manifest(sha, length=len(data)):
        # No rom_offset: this asset only exists once its container is decompressed.
        return _mkfile(mpath, json.dumps({
            "name": "audio/member", "type": "audio.snes.brr", "ver": "v1",
            "source": {"length": length, "source_sha256": sha,
                       "member_of": "blob/demo_pack", "at": 256},
            "audio": {"ext": ".brr"},
        }, indent=2) + "\n")

    manifest(good)
    buf = _mkfile(R("build", "extract", "audio", "member.bin"), data)
    out = R("extracted", "audio", "member.brr")
    layer_args = ["--search", assets, "--base-manifests", gen, "--base-content", content]
    dec = lambda: main(["decode", "--manifest", mpath, "--in", buf, "--out", out] + layer_args)

    _quiet(dec)
    assert open(out, "rb").read() == data, "decode did not reproduce the buffer bytes"
    _quiet(dec)
    assert open(out, "rb").read() == data, "decode is not byte-deterministic"

    # encode resolves through the layers and reproduces the member's buffer bytes exactly.
    enc_out = R("build", "encode", "audio", "member.bin")
    _quiet(lambda: main(["encode", "--name", "audio/member", "--out", enc_out] + layer_args))
    assert open(enc_out, "rb").read() == data, "decode -> encode is not byte-identical"

    # --in-dir supplies the base content root; it must not disturb anything else.
    alt = R("alt")
    _mkfile(os.path.join(alt, "audio", "member.brr"), data)
    _quiet(lambda: main(["encode", "--name", "audio/member", "--in-dir", alt,
                         "--out", enc_out, "--search", assets,
                         "--base-manifests", gen, "--base-content", R("nowhere")]))
    assert open(enc_out, "rb").read() == data, "--in-dir did not supply the content root"

    # An EDITED member still encodes (the build direction tolerates a sha mismatch) while
    # `verify` -- which guards ROM ground truth -- fails on exactly the same bytes.
    edited = bytes([b ^ 0xFF for b in data])
    _mkfile(R("extracted", "audio", "member.brr"), edited)
    _quiet(lambda: main(["encode", "--name", "audio/member", "--out", enc_out] + layer_args))
    assert open(enc_out, "rb").read() == edited, "an edited member must still encode"
    assert _quiet(lambda: main(["verify", "--name", "audio/member"] + layer_args)) == 1, \
        "verify must still FAIL on a sha mismatch"
    _mkfile(R("extracted", "audio", "member.brr"), data)

    # Wrong bytes and wrong length must both halt -- decode is the extract-side direction.
    manifest("0" * 64)
    _expect_die(lambda: _quiet(dec), "source_sha256", "decode of the wrong buffer bytes")
    manifest(good, length=len(data) + 1)
    _expect_die(lambda: _quiet(dec), "source.length", "decode of a wrong-length buffer")

    # A hand-authored member has no source block at all: nothing to check, still decodes.
    _mkfile(mpath, json.dumps({
        "name": "audio/member", "type": "audio.snes.brr", "ver": "v1",
        "audio": {"ext": ".brr"},
    }, indent=2) + "\n")
    _quiet(dec)
    assert open(out, "rb").read() == data, "decode without a source block"


def cmd_selftest(a) -> int:
    _selftest_integrity()
    _selftest_layering()
    _selftest_extract_fork()
    _selftest_buffer_mode()
    print("selftest: all integrity and layering invariants hold  [OK]")
    return 0


def main(argv=None) -> int:
    p = argparse.ArgumentParser(
        prog="binpack",
        description="generic verbatim binary asset <-> ROM bytes (byte-identical round-trip)")
    p.add_argument("--version", action="version", version=TOOL_VERSION)
    sub = p.add_subparsers(dest="cmd", required=True)

    def add_layers(sp, with_ext=True):
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
        if with_ext:
            sp.add_argument("--ext", default=None,
                            help="payload file extension (e.g. .brr). Default: the ext "
                                 "recorded in the manifest, else .bin")

    e = sub.add_parser("extract", help="manifest + ROM -> the payload file")
    e.add_argument("--manifest", required=True,
                   help="the manifest to extract by, e.g. generated/assets/audio/x.json. "
                        "An explicit path, NOT a layer lookup: extraction is ROM ground "
                        "truth and must not be reachable by a mod override")
    e.add_argument("--rom", required=True, help="the original ROM to slice")
    e.add_argument("--out", required=True, help="the payload file to write")
    add_layers(e, with_ext=False)   # accepted for symmetry with the other codecs' rules
    e.set_defaults(fn=cmd_extract)

    d = sub.add_parser("decode", help="manifest + buffer file -> the payload file")
    d.add_argument("--manifest", required=True,
                   help="the manifest to decode by. An explicit path, NOT a layer lookup: "
                        "like extract, this is ground truth and must not be reachable by a "
                        "mod override")
    d.add_argument("--in", required=True, dest="in_path", metavar="FILE",
                   help="the buffer file holding exactly this asset's bytes, read verbatim "
                        "(e.g. one member cut out of a decompressed container)")
    d.add_argument("--out", required=True, help="the payload file to write")
    add_layers(d, with_ext=False)   # accepted for symmetry with the other codecs' rules
    d.set_defaults(fn=cmd_decode)

    fk = sub.add_parser("fork", help="copy the effective bundle into a mod layer")
    fk.add_argument("--name", required=True, help="logical name, e.g. audio/AudioBRR_00")
    fk.add_argument("--mod", required=True, help="mod layer name to fork into")
    fk.add_argument("--mods-dir", default=DEFAULT_MODS_DIR, dest="mods_dir", metavar="DIR",
                    help=f"directory holding mod layers (default {DEFAULT_MODS_DIR})")
    add_layers(fk)
    fk.set_defaults(fn=cmd_fork)

    c = sub.add_parser("compile", help="payload file -> raw .bin, check source_sha256")
    c.add_argument("--name", required=True)
    add_layers(c)
    c.add_argument("--out", required=True)
    c.set_defaults(fn=cmd_compile)

    en = sub.add_parser("encode", help="payload file -> raw bytes for a buffer slot")
    en.add_argument("--name", required=True,
                    help="logical name, resolved through the layers exactly as compile "
                         "does -- so a mod can override a container member")
    add_layers(en)
    en.add_argument("--in-dir", default=None, dest="in_dir", metavar="DIR",
                    help="directory holding the editable content, replacing the BASE "
                         "content root for this command. Mod layers still outrank it. "
                         "Default: --base-content")
    en.add_argument("--out", required=True, help="the buffer-slot bytes to write")
    en.set_defaults(fn=cmd_encode)

    v = sub.add_parser("verify", help="assert the payload still matches the manifest (and optionally the ROM)")
    v.add_argument("--name", required=True)
    add_layers(v)
    v.add_argument("--rom", default=None,
                   help="also compare against live ROM bytes at the manifest offset")
    v.set_defaults(fn=cmd_verify)

    st = sub.add_parser("selftest", help="integrity + layering assertions (needs no repo)")
    st.set_defaults(fn=cmd_selftest)

    a = p.parse_args(argv)
    if getattr(a, "search", None) is None:
        a.search = list(DEFAULT_SEARCH_ROOTS)
    return a.fn(a)


if __name__ == "__main__":
    sys.exit(main())

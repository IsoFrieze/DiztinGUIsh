#!/usr/bin/env python3
"""
nodepack — container nodes: cut a blob out of a ROM, split a buffer into members, join
members back into a buffer. Pure offset arithmetic, byte-identical round-trip.

Part of "dizpack", the stock codec toolset that DiztinGUIsh vendors into a game repo
on export. The game repo must NEVER need Diz at build time — only Python 3. Like binpack,
nodepack has NO third-party dependency.

What it is FOR
--------------
Some assets are not stored one-per-region: several of them share one blob, often a
compressed one. That blob is described by a CONTAINER manifest — a node with a `members`
array instead of a codec block — and building it is three generic steps:

    ROM --slice--> compressed blob --(decompress)--> buffer --split--> member files
    member files --join--> buffer --(compress)--> compressed blob --> incbin

nodepack owns the first and third arrows in each row. It is deliberately IGNORANT of both
the game and the codecs: it knows offsets, lengths and file names, and nothing else. The
compression steps are somebody else's tool (a game-specific one), and each member is
decoded/encoded by whichever leaf codec its own manifest names (gfxpack, textpack,
binpack). Keeping the split generic is what lets a member's build edge look exactly like a
top-level asset's, so mods, layering and determinism need no second implementation.

Coverage is the load-bearing rule
---------------------------------
**Members must tile the buffer exactly.** No hole, no overlap, nothing past the end, and
the declaration order must be the address order. Any violation HALTS, naming the container
and the byte range. A stretch nobody has reverse engineered yet is not a hole to ignore —
it is declared as an explicit member with a verbatim type, so the buffer is fully accounted
for from day one and understanding can accrete later without ever being wrong.

`at` is EXTRACT-TIME ONLY
------------------------
`split` cuts at the declared `at`, because that is where the data actually is in the
original buffer. `join` does NOT trust `at`: it concatenates the member files in
declaration order and recomputes every offset from their ACTUAL lengths. That is what makes
an edited member that grew safe — the growth shifts everything after it and is caught by
the region-bounds check downstream, instead of being silently mis-framed against a stale
`at`. Member declaration order is data.

Commands:
  slice     container manifest + ROM  -> the blob as stored (asserts source_sha256)
  split     container manifest + buffer -> one file per member (asserts coverage)
  join      container manifest + member files -> the buffer (recomputes offsets)
  selftest  -> coverage + round-trip assertions; needs no repo and no ROM (CI gate)

Member files are named `<dir>/<member name>.bin`, so a member called `gfx/main_font`
lands at `<dir>/gfx/main_font.bin` — the same logical-name-to-path rule the other codecs
use for their bundles.

Example (a container of three members plus a raw tail):
  nodepack.py slice --manifest generated/assets/blob/font_pack.json \
                    --rom rom/ct-us-orig.sfc --out build/extract/blob/font_pack.raw
  nodepack.py split --manifest generated/assets/blob/font_pack.json \
                    --in build/extract/blob/font_pack.plain --outdir build/extract
  nodepack.py join  --manifest generated/assets/blob/font_pack.json \
                    --outdir build/encode --out build/join/blob/font_pack.plain
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys

# --------------------------------------------------------------------------------------
# Versioning (same discipline as the other codecs).
#
# Manifests carry {"type": ..., "ver": ...}. `ver` omitted => LATEST. A version we don't
# implement is a HARD ERROR, never a silent best-effort -- silent drift is exactly what
# breaks byte-identity.
# --------------------------------------------------------------------------------------
LATEST_VER = "v1"
SUPPORTED_VERS = {"v1"}
TOOL_VERSION = "nodepack/1.0.0"

# The build intermediate holding one member's bytes. One extension, always: these files are
# raw buffer fragments, never an editable view of anything.
MEMBER_EXT = ".bin"


def die(msg: str) -> "None":
    print(f"nodepack: error: {msg}", file=sys.stderr)
    raise SystemExit(2)


# ======================================================================================
# Manifest: load and validate the container node
#
# nodepack does no layer resolution at all. Every command is driven by ONE explicit
# manifest path, exactly as the other codecs' `extract` is: a container's shape is
# structural ground truth, and letting a mod layer redefine where the member boundaries
# are would silently re-cut everyone else's data.
# ======================================================================================
def load_manifest(path: str) -> dict:
    try:
        with open(path, "r", encoding="utf-8") as f:
            man = json.load(f)
    except OSError as e:
        die(f"{path}: cannot read the manifest: {e}")
    except json.JSONDecodeError as e:
        die(f"{path}: invalid JSON: {e}")

    ver = man.get("ver") or LATEST_VER  # omitted => latest
    if ver not in SUPPORTED_VERS:
        die(f"{path}: manifest version '{ver}' is not implemented by {TOOL_VERSION} "
            f"(supported: {sorted(SUPPORTED_VERS)}). Refusing to guess.")
    man["ver"] = ver
    if not man.get("type"):
        die(f"{path}: manifest has no 'type'")
    return man


def container_name(man: dict, path: str) -> str:
    """What to call this container in messages. The logical name if it has one, else the
    manifest path -- an error must always be traceable to a file."""
    name = man.get("name")
    return name if isinstance(name, str) and name else path


def parse_members(man: dict, path: str) -> "list[dict]":
    """Validate the `members` array -> a list of {name, at, len}, in DECLARATION order.

    A node has either a typed codec block or members, never both; `members` is what makes
    this manifest a container, and its absence means the manifest belongs to a leaf codec
    rather than here.
    """
    members = man.get("members")
    if members is None:
        die(f"{path}: manifest has no 'members' array, so it does not describe a container. "
            f"A leaf asset is its own codec's job (gfxpack/textpack/binpack); nodepack only "
            f"cuts up and reassembles containers.")
    if not isinstance(members, list):
        die(f"{path}: 'members' must be a JSON array, got {type(members).__name__}")
    if not members:
        die(f"{path}: 'members' is empty. A container must account for every byte of its "
            f"buffer, so it has at least one member (a fully unidentified buffer is ONE "
            f"verbatim member, not zero).")

    out: "list[dict]" = []
    seen: "dict[str, int]" = {}
    for i, m in enumerate(members):
        where = f"{path}: members[{i}]"
        if not isinstance(m, dict):
            die(f"{where} must be an object with 'name', 'at' and 'len'")
        name = m.get("name")
        if not isinstance(name, str) or not name:
            die(f"{where}: 'name' must be a non-empty logical name, got {name!r}")
        if name in seen:
            die(f"{where}: member name '{name}' was already declared at members[{seen[name]}]. "
                f"Names are file paths -- two members with one name would overwrite each "
                f"other on split and be read twice on join.")
        seen[name] = i
        vals = {}
        for key, low in (("at", 0), ("len", 1)):
            v = m.get(key)
            # bool is an int subclass in Python; `true` is not an offset.
            if not isinstance(v, int) or isinstance(v, bool) or v < low:
                die(f"{where} ('{name}'): '{key}' must be an integer >= {low}, got {v!r}"
                    + ("" if low == 0 else
                       ". A zero-length member claims no bytes and cannot be told apart "
                       "from a typo; drop it instead."))
            vals[key] = v
        out.append({"name": name, "at": vals["at"], "len": vals["len"]})
    return out


def check_coverage(members: "list[dict]", total: int, container: str, where: str) -> None:
    """Prove the members TILE the buffer exactly, or HALT naming the offending byte range.

    Four distinct failures, reported distinctly because they have different fixes:
      * a HOLE -- bytes nobody claims. Declare them as an explicit verbatim member.
      * an OVERLAP -- two members claiming the same byte. One of the two is wrong.
      * a SHORTFALL/OVERRUN against the real buffer length -- the manifest describes a
        different buffer than the one in hand.
      * members DECLARED OUT OF ADDRESS ORDER -- legal-looking and quietly fatal, because
        `join` concatenates in declaration order: the rebuilt buffer would be a permutation
        of the original one, and every downstream check would still pass.

    Silence on any of these is the expensive failure mode: the ROM still assembles, still
    boots, and is wrong.
    """
    ordered = sorted(members, key=lambda m: m["at"])
    cursor = 0
    for m in ordered:
        at, ln = m["at"], m["len"]
        if at > cursor:
            die(f"{where}: container '{container}' leaves a HOLE at bytes "
                f"0x{cursor:X}..0x{at:X} ({at - cursor} unclaimed), before member "
                f"'{m['name']}'. Members must tile the buffer exactly -- declare the gap "
                f"as an explicit verbatim member rather than losing those bytes.")
        if at < cursor:
            die(f"{where}: container '{container}' has an OVERLAP at bytes "
                f"0x{at:X}..0x{cursor:X} ({cursor - at} bytes claimed twice): member "
                f"'{m['name']}' starts inside the member before it. Members must tile the "
                f"buffer exactly.")
        cursor = at + ln
    if cursor < total:
        die(f"{where}: container '{container}' leaves a HOLE at bytes "
            f"0x{cursor:X}..0x{total:X} ({total - cursor} unclaimed) at the END of the "
            f"buffer. Members must tile the buffer exactly -- declare the tail as an "
            f"explicit verbatim member.")
    if cursor > total:
        die(f"{where}: container '{container}' claims {cursor} bytes but the buffer is "
            f"{total}; the last {cursor - total} byte(s) are past its end. The manifest "
            f"describes different data than the one in hand.")

    if [m["name"] for m in members] != [m["name"] for m in ordered]:
        first = next(i for i, (a, b) in enumerate(zip(members, ordered))
                     if a["name"] != b["name"])
        die(f"{where}: container '{container}' declares its members out of address order "
            f"-- members[{first}] is '{members[first]['name']}' at "
            f"0x{members[first]['at']:X} where '{ordered[first]['name']}' at "
            f"0x{ordered[first]['at']:X} comes next. Declaration order is data: join "
            f"concatenates in it, so out-of-order members would silently rebuild the "
            f"buffer permuted. List them in ascending 'at'.")


def member_path(root: str, name: str) -> str:
    """Where one member's bytes live: <root>/<logical name>.bin. The logical name is a
    '/'-separated path, the same rule the leaf codecs use for their bundles."""
    return os.path.join(root, *name.split("/")) + MEMBER_EXT


# ======================================================================================
# Commands
# ======================================================================================
def read_rom_slice(rom_path: str, man: dict, manifest_path: str) -> bytes:
    """Read the ROM bytes a manifest describes, and prove they are the right ones.

    The `source` block is a claim about a specific cartridge. Checking its sha256 before
    writing anything is what turns "you pointed at some ROM" into a hard error instead of a
    plausible-looking blob built from the wrong bytes -- a wrong region, a different
    revision, or a headered dump all land here. For a container, `source.length` is the
    length of the blob AS STORED (compressed, if it is compressed); the decompressed
    buffer's length is not recorded and is never needed -- split measures the buffer.
    """
    src = man.get("source") or {}
    if "rom_offset" not in src or "length" not in src:
        die(f"{manifest_path}: slice needs source.rom_offset and source.length, and the "
            f"manifest has no ROM provenance. A hand-authored container is not sliceable: "
            f"its members are the source, there is nothing to cut it out of.")
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
        die(f"{manifest_path}: source.source_sha256 is missing. Slicing is only safe "
            f"when the bytes can be proven to be the ones the manifest describes.")
    got = hashlib.sha256(blob).hexdigest()
    if got != want:
        die(f"{rom_path}: bytes at 0x{off:X}+{length} hash to {got}, but "
            f"{manifest_path} declares source_sha256 {want}. This is not the ROM the "
            f"container was exported from (wrong version, wrong region, or a headered "
            f"dump). Refusing to slice.")
    return blob


def write_file(blob: bytes, out_path: str) -> None:
    """Write a build intermediate and read it straight back, so a truncated or unwritable
    output is caught here rather than as a mysterious mismatch two edges later."""
    os.makedirs(os.path.dirname(os.path.abspath(out_path)), exist_ok=True)
    with open(out_path, "wb") as f:
        f.write(blob)
    with open(out_path, "rb") as f:
        if f.read() != blob:
            die(f"SELF-CHECK FAILED: re-reading {out_path} did not reproduce the bytes "
                f"just written. Do not trust this build.")


def cmd_slice(a) -> int:
    """ROM -> the container's blob exactly as stored, driven by ONE explicit manifest.

    This is the container's counterpart of a leaf codec's `extract`, and the same rules
    apply: ROM ground truth, an explicit manifest path rather than a layer lookup, safe to
    re-run, byte-deterministic by construction (it is a verified copy).
    """
    man = load_manifest(a.manifest)
    parse_members(man, a.manifest)      # a container it must be, before anything is written
    blob = read_rom_slice(a.rom, man, a.manifest)
    write_file(blob, a.out)

    src = man["source"]
    print(f"sliced {container_name(man, a.manifest)} ({len(blob)} bytes as stored, "
          f"{man['type']}) from {a.rom} @{src['rom_offset']}")
    print(f"  manifest : {a.manifest}")
    print(f"  blob     : {a.out}")
    print(f"  sha256   : {src['source_sha256']}  [matches ROM]")
    return 0


def cmd_split(a) -> int:
    """A buffer -> one file per member, cut at the declared `at`/`len`.

    The buffer is whatever the pipeline produced upstream (the blob itself if the container
    is uncompressed, otherwise its decompressed form), so its length is measured, never
    taken from the manifest: `source.length` describes the STORED blob and would be the
    wrong number here.
    """
    man = load_manifest(a.manifest)
    members = parse_members(man, a.manifest)
    name = container_name(man, a.manifest)
    try:
        with open(a.in_path, "rb") as f:
            buf = f.read()
    except OSError as e:
        die(f"{a.in_path}: cannot read the buffer to split: {e}")

    check_coverage(members, len(buf), name, a.in_path)

    for m in members:
        write_file(buf[m["at"]:m["at"] + m["len"]], member_path(a.outdir, m["name"]))

    print(f"split {name} ({len(buf)} bytes) into {len(members)} member(s) -> {a.outdir}")
    print(f"  manifest : {a.manifest}")
    print(f"  buffer   : {a.in_path}")
    for m in members:
        print(f"  @0x{m['at']:06X} +{m['len']:<7} {m['name']}")
    print("  coverage : members tile the buffer exactly  [OK]")
    return 0


def cmd_join(a) -> int:
    """Member files -> the buffer, concatenated in DECLARATION order.

    Offsets are recomputed from the members' actual lengths; the manifest's `at` is only
    checked for self-consistency (it still has to tile), never used to place anything. A
    member that grew is therefore not an error here -- it shifts what follows it, and the
    resulting buffer being too big for its slot is caught downstream, where the size limit
    actually lives.
    """
    man = load_manifest(a.manifest)
    members = parse_members(man, a.manifest)
    name = container_name(man, a.manifest)

    # The manifest must still describe a coherent tiling of its own declared size, even
    # though the actual sizes below are what get used. A container that cannot tile is
    # broken whichever direction it is being built in.
    check_coverage(members, sum(m["len"] for m in members), name, a.manifest)

    out = bytearray()
    placed = []
    for m in members:
        path = member_path(a.outdir, m["name"])
        try:
            with open(path, "rb") as f:
                data = f.read()
        except OSError as e:
            die(f"{path}: cannot read member '{m['name']}' of container '{name}': {e}. "
                f"Every member must have been encoded before the container can be joined.")
        placed.append((m, len(out), len(data)))
        out += data

    write_file(bytes(out), a.out)

    drifted = [(m, at, ln) for m, at, ln in placed if ln != m["len"] or at != m["at"]]
    print(f"joined {len(members)} member(s) of {name} -> {a.out} ({len(out)} bytes)")
    print(f"  manifest : {a.manifest}")
    print(f"  members  : {a.outdir}")
    for m, at, ln in placed:
        print(f"  @0x{at:06X} +{ln:<7} {m['name']}")
    if drifted:
        # Not an error: offsets are recomputed on purpose. Say it out loud anyway, because
        # a changed length is the interesting thing that happened in this build.
        print(f"  note     : {len(drifted)} member(s) differ from the declared extract-time "
              f"layout; offsets were recomputed from the actual lengths")
    return 0


# ======================================================================================
# selftest — coverage and round-trip assertions against throwaway temp trees. Needs no
# repo and no ROM: this is the public-CI gate.
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


def _container_json(members: "list[dict]", source: "dict | None" = None) -> str:
    man = {"name": "blob/demo_pack", "type": "blob.container", "ver": "v1"}
    if source is not None:
        man["source"] = source
    man["members"] = members
    man["generated_by"] = "selftest"
    return json.dumps(man, indent=2) + "\n"


def _demo_members() -> "list[dict]":
    return [{"name": "gfx/a", "at": 0, "len": 16},
            {"name": "text/b", "at": 16, "len": 8},
            {"name": "blob/demo_pack.pad", "at": 24, "len": 4}]


def _selftest_coverage() -> None:
    """The tiling rule: exact passes, every way of breaking it halts."""
    ok = _demo_members()
    check_coverage(ok, 28, "blob/demo_pack", "selftest")     # exact: no exception

    hole = [dict(m) for m in ok]
    hole[1]["at"] = 20                                        # 16..20 unclaimed
    hole[1]["len"] = 4
    _expect_die(lambda: check_coverage(hole, 28, "blob/demo_pack", "selftest"),
                "HOLE", "a container with a hole between members")

    overlap = [dict(m) for m in ok]
    overlap[0]["len"] = 20                                    # runs into member 1
    _expect_die(lambda: check_coverage(overlap, 28, "blob/demo_pack", "selftest"),
                "OVERLAP", "a container whose members overlap")

    _expect_die(lambda: check_coverage(ok, 32, "blob/demo_pack", "selftest"),
                "HOLE", "members that stop short of the buffer end")
    _expect_die(lambda: check_coverage(ok, 24, "blob/demo_pack", "selftest"),
                "past its end", "members that run past the buffer end")

    swapped = [ok[1], ok[0], ok[2]]                           # tiles, but declared reversed
    _expect_die(lambda: check_coverage(swapped, 28, "blob/demo_pack", "selftest"),
                "out of address order", "members declared out of address order")


def _selftest_members() -> None:
    """The member array is validated, not trusted."""
    import tempfile
    R = lambda body: _mkfile(os.path.join(tempfile.mkdtemp(prefix="nodepack-m-"), "c.json"),
                             body)
    ok = _demo_members()
    p = R(_container_json(ok))
    assert parse_members(load_manifest(p), p) == ok, "a valid member list round-trips"

    leaf = R(json.dumps({"name": "gfx/x", "type": "gfx.snes.2bpp",
                         "gfx": {"bpp": 2, "tiles": 1}}) + "\n")
    _expect_die(lambda: parse_members(load_manifest(leaf), leaf),
                "does not describe a container", "a leaf manifest handed to nodepack")
    empty = R(_container_json([]))
    _expect_die(lambda: parse_members(load_manifest(empty), empty),
                "empty", "a container with no members")
    dup = R(_container_json([{"name": "gfx/a", "at": 0, "len": 4},
                             {"name": "gfx/a", "at": 4, "len": 4}]))
    _expect_die(lambda: parse_members(load_manifest(dup), dup),
                "already declared", "two members sharing one name")
    zero = R(_container_json([{"name": "gfx/a", "at": 0, "len": 0}]))
    _expect_die(lambda: parse_members(load_manifest(zero), zero),
                "len", "a zero-length member")
    bad = R(_container_json([{"name": "gfx/a", "at": "0x10", "len": 4}]))
    _expect_die(lambda: parse_members(load_manifest(bad), bad),
                "at", "a non-integer offset")


def _selftest_round_trip() -> None:
    """split -> join is byte-identical, and join recomputes offsets rather than trusting
    the manifest's extract-time `at`."""
    import tempfile
    tmp = tempfile.mkdtemp(prefix="nodepack-rt-")
    R = lambda *p: os.path.join(tmp, *p)

    buf = bytes((i * 37 + 11) & 0xFF for i in range(28))
    mpath = _mkfile(R("generated", "assets", "blob", "demo_pack.json"),
                    _container_json(_demo_members()))
    bpath = _mkfile(R("build", "extract", "blob", "demo_pack.plain"), buf)
    ex, en = R("build", "extract"), R("build", "encode")

    _quiet(lambda: main(["split", "--manifest", mpath, "--in", bpath, "--outdir", ex]))
    parts = [open(member_path(ex, m["name"]), "rb").read() for m in _demo_members()]
    assert [len(p) for p in parts] == [16, 8, 4], f"member sizes: {[len(p) for p in parts]}"
    assert b"".join(parts) == buf, "split did not cut the buffer where it said it would"
    assert parts[1] == buf[16:24], "member bytes came from the wrong offset"

    # Splitting twice must produce the same files -- these are tracked-adjacent artifacts.
    _quiet(lambda: main(["split", "--manifest", mpath, "--in", bpath, "--outdir", ex]))
    assert [open(member_path(ex, m["name"]), "rb").read()
            for m in _demo_members()] == parts, "split is not byte-deterministic"

    # join over the untouched members reproduces the buffer exactly.
    joined = R("build", "join", "blob", "demo_pack.plain")
    for m, part in zip(_demo_members(), parts):
        _mkfile(member_path(en, m["name"]), part)
    _quiet(lambda: main(["join", "--manifest", mpath, "--outdir", en, "--out", joined]))
    assert open(joined, "rb").read() == buf, "split -> join is not byte-identical"

    # A member that GREW: join must place what follows it at the recomputed offset, not at
    # the stale `at` -- and must not complain, because growing is legal here.
    grown = parts[0] + b"\xAA\xBB"
    _mkfile(member_path(en, "gfx/a"), grown)
    _quiet(lambda: main(["join", "--manifest", mpath, "--outdir", en, "--out", joined]))
    assert open(joined, "rb").read() == grown + parts[1] + parts[2], \
        "join did not recompute offsets from the actual member lengths"
    _mkfile(member_path(en, "gfx/a"), parts[0])

    # A missing member file halts: half a container is never silently built.
    os.remove(member_path(en, "text/b"))
    _expect_die(lambda: _quiet(lambda: main(
        ["join", "--manifest", mpath, "--outdir", en, "--out", joined])),
        "cannot read member", "join with a member file missing")
    _mkfile(member_path(en, "text/b"), parts[1])

    # And the coverage rule is enforced through the COMMANDS, not just the helper.
    holed = _mkfile(R("generated", "assets", "blob", "holed.json"),
                    _container_json([{"name": "gfx/a", "at": 0, "len": 16},
                                     {"name": "text/b", "at": 20, "len": 8}]))
    _expect_die(lambda: _quiet(lambda: main(
        ["split", "--manifest", holed, "--in", bpath, "--outdir", ex])),
        "HOLE", "splitting a container with a hole")
    _expect_die(lambda: _quiet(lambda: main(
        ["join", "--manifest", holed, "--outdir", en, "--out", joined])),
        "HOLE", "joining a container with a hole")
    lapped = _mkfile(R("generated", "assets", "blob", "lapped.json"),
                     _container_json([{"name": "gfx/a", "at": 0, "len": 20},
                                      {"name": "text/b", "at": 16, "len": 12}]))
    _expect_die(lambda: _quiet(lambda: main(
        ["split", "--manifest", lapped, "--in", bpath, "--outdir", ex])),
        "OVERLAP", "splitting a container whose members overlap")
    _expect_die(lambda: _quiet(lambda: main(
        ["join", "--manifest", lapped, "--outdir", en, "--out", joined])),
        "OVERLAP", "joining a container whose members overlap")

    # Buffer of the wrong size for the declared members: the manifest describes other data.
    short = _mkfile(R("build", "extract", "blob", "short.plain"), buf[:20])
    _expect_die(lambda: _quiet(lambda: main(
        ["split", "--manifest", mpath, "--in", short, "--outdir", ex])),
        "past its end", "splitting a buffer smaller than the members")


def _selftest_slice() -> None:
    """slice: the sha gate, and a verified copy out of a fake ROM."""
    import tempfile
    tmp = tempfile.mkdtemp(prefix="nodepack-slice-")
    R = lambda *p: os.path.join(tmp, *p)

    blob = bytes((i * 91 + 7) & 0xFF for i in range(28))
    rom = _mkfile(R("fake.sfc"), bytes(0x10) + blob + bytes(0x10))
    good = hashlib.sha256(blob).hexdigest()
    mpath = R("generated", "assets", "blob", "demo_pack.json")
    src = lambda sha: {"rom_offset": "0x10", "length": len(blob), "source_sha256": sha,
                       "snes_addr": "0xC00010"}
    out = R("build", "extract", "blob", "demo_pack.raw")
    run = lambda: main(["slice", "--manifest", mpath, "--rom", rom, "--out", out])

    _mkfile(mpath, _container_json(_demo_members(), src(good)))
    _quiet(run)
    assert open(out, "rb").read() == blob, "slice did not reproduce the ROM bytes"
    _quiet(run)
    assert open(out, "rb").read() == blob, "slice is not byte-deterministic"

    _mkfile(mpath, _container_json(_demo_members(), src("0" * 64)))
    _expect_die(lambda: _quiet(run), "source_sha256", "slice against the wrong ROM")
    _mkfile(mpath, _container_json(_demo_members(), None))
    _expect_die(lambda: _quiet(run), "provenance", "slice of a hand-authored container")


def cmd_selftest(a) -> int:
    _selftest_coverage()
    _selftest_members()
    _selftest_round_trip()
    _selftest_slice()
    print("selftest: all coverage and round-trip invariants hold  [OK]")
    return 0


def main(argv=None) -> int:
    p = argparse.ArgumentParser(
        prog="nodepack",
        description="container nodes: slice a blob, split a buffer into members, join them back")
    p.add_argument("--version", action="version", version=TOOL_VERSION)
    sub = p.add_subparsers(dest="cmd", required=True)

    manifest_help = ("the container manifest. An explicit path, NOT a layer lookup: where "
                     "the member boundaries are is structural ground truth and must not be "
                     "reachable by a mod override")

    s = sub.add_parser("slice", help="manifest + ROM -> the container's blob as stored")
    s.add_argument("--manifest", required=True, help=manifest_help)
    s.add_argument("--rom", required=True, help="the original ROM to slice")
    s.add_argument("--out", required=True, help="the blob file to write")
    s.set_defaults(fn=cmd_slice)

    sp = sub.add_parser("split", help="manifest + buffer -> one file per member")
    sp.add_argument("--manifest", required=True, help=manifest_help)
    sp.add_argument("--in", required=True, dest="in_path", metavar="FILE",
                    help="the buffer to cut up (the blob itself, or its decompressed form)")
    sp.add_argument("--outdir", required=True, metavar="DIR",
                    help=f"directory to write <member name>{MEMBER_EXT} into")
    sp.set_defaults(fn=cmd_split)

    j = sub.add_parser("join", help="manifest + member files -> the buffer")
    j.add_argument("--manifest", required=True, help=manifest_help)
    j.add_argument("--outdir", required=True, metavar="DIR",
                   help=f"directory holding <member name>{MEMBER_EXT} to read back")
    j.add_argument("--out", required=True, help="the reassembled buffer to write")
    j.set_defaults(fn=cmd_join)

    st = sub.add_parser("selftest", help="coverage + round-trip assertions (needs no repo)")
    st.set_defaults(fn=cmd_selftest)

    a = p.parse_args(argv)
    return a.fn(a)


if __name__ == "__main__":
    sys.exit(main())

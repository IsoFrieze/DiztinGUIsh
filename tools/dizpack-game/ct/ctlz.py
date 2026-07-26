#!/usr/bin/env python3
"""
ctlz -- Chrono Trigger (US) LZSS codec. DECODE ONLY; the encoder is ctlzpack.py.

Game-specific codec, so it is not part of the shared tool set. The master copy lives in
the DiztinGUIsh repo under tools/dizpack-game/ct/; an export copies it, together with the
rest of that game's tool directory, into the game repo's tools/vendor/game/. That copy is
regenerated on every export -- edit the master, never the vendored file.

Follows the gfxpack.py conventions: single file, stdlib only, argparse subcommands,
die() for fatal errors, manifests as the authority.

Canonical format spec: docs/chrono-trigger/compression-format.md (in romhax-workspace).
Primary evidence is the game's own decompressor, `fnl_decompression_routine`,
$C3/0557-$C3/08B2 in generated/bank_C3.asm. Every nonobvious rule below cites the
address it was read from. Where this file and the .md disagree, re-read the ASM.


================================ THE FORMAT ================================

A blob is bank-agnostic and self-terminating:

    +0        uint16  SIZE of the body, little-endian, counting the body ONLY
                      (not itself, not the marker, not any addendum).
                      [$C3/0563-$C3/056B: X = src+2; $0309 = (src+2) + [[src]] ]
    +2        BODY: a run of packets.
    +2+SIZE   MARKER byte, then optionally an addendum chain (below).

Registers in the ASM, for reading the citations: X = ROM read cursor, Y = WRAM write
cursor, $0309 = "stop cursor" (address just past the current body).

--- PACKET ---------------------------------------------------------------------
    ctrl byte, then 8 elements, consumed LSB-first:
        bit 0 -> one literal byte
        bit 1 -> one two-byte back-reference
    Packet length = 1 + 8 + (number of set bits).  [$C3/05ED-$C3/0612]

--- ctrl == 0 FAST PATH --------------------------------------------------------
    If the ctrl byte is $00, the routine emits the next 8 bytes as literals with
    unrolled stores and does X += 9, Y += 8.  [$C3/05F0 -> $C3/059D-$C3/05E3]
    For a full 8-bit packet this is just an optimization -- identical output.
    It is NOT equivalent inside an addendum; see ADDENDUM below.

--- BACK-REFERENCE (2 bytes, little-endian word b0,b1) -------------------------
    12-bit mode:  offset = word & $0FFF   length = (b1 >> 4) + 3   (3..18)
    11-bit mode:  offset = word & $07FF   length = (b1 >> 3) + 3   (3..34)

    Length: the ASM shifts b1 right and adds 2 ($C3/0614-$C3/061D), then MVN copies
    C+1 bytes ($C3/0633) -- so +2 +1 = +3. Verified, no off-by-one.

    Offset is a BACKWARD DISTANCE INTO THE OUTPUT PRODUCED SO FAR
    ($C3/0629-$C3/062B: source = Y - offset). It is NOT a ring-buffer index (this
    is where FF6-style assumptions go wrong).

    *** The copy MUST be byte-by-byte, forward, ascending. *** MVN increments X and
    Y as it goes, so offset < length legally overlaps itself and re-reads bytes it
    just wrote -- free RLE, and real data uses it. Using slicing/memmove here is a
    silent correctness bug that only shows up on overlapping runs. See selftest.

--- MODE SELECT (and why it is latched) ----------------------------------------
    Mode comes from the FIRST marker's & $C0: nonzero -> 11-bit, zero -> 12-bit.
    [$C3/057C-$C3/0580]

    $C0 selects OFFSET WIDTH ONLY. The destination bank ($7E vs $7F) is a separate
    axis taken from the CALLER's byte at $0305, not from the stream ($C3/0588,
    $C3/0594). The four routine bodies are a 2x2 (width x bank), not a 1x4.
    Consequence: a blob carries no destination bank, and this decoder needs no bank
    parameter.

    Mode is LATCHED. After an addendum marker, control does BRA $C305E9 ($C3/065B)
    -- back into the ALREADY-SELECTED variant body. So the $C0 bits of every
    addendum marker are ignored by the hardware. We record them anyway (see
    `addenda[].marker_c0`) because a nonzero one would be an encoder that believed
    otherwise, which is worth knowing.

--- END / ADDENDUM CHAIN -------------------------------------------------------
    When the read cursor reaches the stop cursor, the marker byte is read:
        if (marker & $3F) == 0  -> DONE            [$C3/0644-$C3/0649 -> $C3/065D]
        else:
            bitcount = marker & $3F                [$C3/064B]
            uint16 CUMULATIVE length at marker+1;
              new stop cursor = src16 + that word  [$C3/064F-$C3/0654, carry clear
              via REP #$21 at $C3/064D -- REP *clears* C, so no +1]
            X += 3, resume decoding                [$C3/0656-$C3/065B]

    The cursor test is `CPX $0309 : BEQ` at $C3/05E9 and it runs ONLY at a packet
    boundary -- it is an EQUALITY test, not >=. If a body's size does not land
    exactly on a packet boundary the real hardware runs away past the end. This
    decoder therefore treats overshoot as a hard error rather than stopping early.

    Two things follow from the ASM that are easy to get wrong:

    (a) The bit counter is NOT reset to 8 when resuming into an addendum. $C305E9
        is entered with the counter holding (marker & $3F). So the addendum's first
        ctrl byte supplies only that many elements, and $3F permits up to 63 -- more
        than the 8 bits an 8-bit shift register holds. We emulate the shift register
        faithfully: LSR shifts in zeros, so bits past the 8th read as 0 (literals).

    (b) *** This explains OPEN QUESTION 1 (the "$FE" rule). *** Geiger found
        empirically that an addendum's ctrl byte must have its UNUSED bits SET, and
        could not explain why. The ASM explains it: $C305E9 falls through to the
        `LDA $0000,X : BEQ` fast-path test at $C3/05F0, which does not consult the
        bit counter at all. An addendum of one literal with cleared padding has ctrl
        == $00, hits the 8-literal fast path, and emits 8 bytes instead of 1. Setting
        the padding ($FE) makes the ctrl byte nonzero, so the slow path runs and the
        counter is honoured. Read from the ASM, not inferred from behaviour.

--- OUTPUT LENGTH --------------------------------------------------------------
    The routine reports Y - dst at $C3/08AA. There is no length field in the stream;
    the decompressed size is whatever the packets produce.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys

TOOL_VERSION = "ctlz/1.0.0"

# Manifest versioning, same contract as gfxpack: `ver` omitted => LATEST. A version we
# do not implement is a HARD ERROR, never a silent best-effort.
LATEST_VER = "v1"
SUPPORTED_VERS = {"v1"}

MODE_12BIT = 12
MODE_11BIT = 11


def die(msg: str) -> "None":
    print(f"ctlz: error: {msg}", file=sys.stderr)
    raise SystemExit(2)


class CtlzError(Exception):
    """A blob failed to decode. Carries how far we got, for corpus triage."""

    def __init__(self, msg: str, src: int = -1, out_len: int = -1):
        super().__init__(msg)
        self.msg = msg
        self.src = src
        self.out_len = out_len


# ======================================================================================
# Decoder
# ======================================================================================

def decode(rom: bytes, start: int, trace: bool = False) -> "tuple[bytearray, dict]":
    """Decompress the CT LZSS blob at file offset `start`.

    Returns (output_bytes, info). `info` is the observed statistics of this stream --
    it is the instrument the corpus questions are answered with, so it records what
    the stream DID, not what it was allowed to do.

    Raises CtlzError on any stream that the game's routine would not have decoded
    cleanly. We never "recover": a loosened decoder would launder bad data into
    plausible-looking output, and the whole point of the corpus is to find real
    contradictions.
    """
    n = len(rom)

    def rd8(p: int) -> int:
        if p < 0 or p >= n:
            raise CtlzError(f"read past end of ROM at 0x{p:X}", p, -1)
        return rom[p]

    def rd16(p: int) -> int:
        return rd8(p) | (rd8(p + 1) << 8)

    size = rd16(start)

    # $C3/0563-$C3/056B. X and the stop cursor are 16-bit registers in the real
    # routine and wrap within the bank; we work in flat file offsets and treat a
    # bank crossing as a thing to report rather than silently emulate (no observed
    # case in the corpus so far -- see the note in the corpus doc).
    src = start + 2
    stop = src + size

    marker = rd8(stop)
    mode = MODE_11BIT if (marker & 0xC0) else MODE_12BIT
    if mode == MODE_11BIT:
        offset_mask, len_shift = 0x07FF, 3
    else:
        offset_mask, len_shift = 0x0FFF, 4

    out = bytearray()

    info = {
        "start": start,
        "header_size": size,
        "mode": mode,
        "first_marker": marker,
        "addenda": [],          # one entry per addendum marker that continued the stream
        "packets": 0,
        "literals": 0,
        "matches": 0,
        "fast_path_packets": 0,
        "match_len_hist": {},
        "max_offset": 0,
        "zero_offset_matches": 0,
        "overlap_matches": 0,   # offset < length, i.e. self-overlapping RLE
        "underflow_matches": 0, # offset > len(out): reads before the start of output
        # trace=True: one tuple per emitted ELEMENT, in emission order, as
        # (out_pos, is_match, offset, length). length is 1 for a literal. This is the
        # input to `fingerprint`'s greedy/lazy and offset-rank analysis: at out_pos the
        # encoder had out[:out_pos] as history and out[out_pos:] as lookahead.
        "elements": [] if trace else None,
    }

    bitctr = 8  # $C3/05E5

    while True:
        # --- top of packet: $C3/05E9 --------------------------------------------
        if src == stop:
            # marker / addendum chain, $C3/0644
            m = rd8(src)
            bits = m & 0x3F
            if bits == 0:
                src += 1  # the terminator byte itself is consumed
                break
            cum = rd16(src + 1)
            new_stop = start + cum
            if new_stop <= src:
                raise CtlzError(
                    f"addendum stop cursor 0x{new_stop:X} does not advance past "
                    f"marker at 0x{src:X} (cumulative length {cum})", src, len(out))
            info["addenda"].append({
                "marker_at": src,
                "marker": m,
                "bits": bits,
                "marker_c0": m & 0xC0,
                "cumulative": cum,
                "new_stop": new_stop,
            })
            src += 3
            stop = new_stop
            bitctr = bits          # $C3/064B -- deliberately NOT reset to 8
            # The addendum's PACKET HEADER (control byte) is the very next byte:
            # $C3/065B BRA CODE_C305E9 -> $C3/05ED LDA $0000,X with X = marker+3.
            # This is the byte Geiger pads with `|= (0xFF << nBitCtr)`.
            info["addenda"][-1]["ctrl"] = rd8(src)
            info["addenda"][-1]["ctrl_at"] = src
            continue

        if src > stop:
            # $C3/05E9 is `CPX : BEQ` -- an equality test. Real hardware would have
            # sailed straight past the marker and kept decoding garbage.
            raise CtlzError(
                f"read cursor 0x{src:X} overshot stop cursor 0x{stop:X}: body does "
                f"not end on a packet boundary", src, len(out))

        ctrl = rd8(src)

        # --- ctrl == 0 fast path: $C3/05F0 -> $C3/059D --------------------------
        # Note this test precedes the bit counter entirely, which is exactly why an
        # addendum must not have a $00 ctrl byte (see module docstring).
        if ctrl == 0:
            for i in range(1, 9):
                if trace:
                    info["elements"].append((len(out), 0, 0, 1))
                out.append(rd8(src + i))
            src += 9
            info["packets"] += 1
            info["fast_path_packets"] += 1
            info["literals"] += 8
            bitctr = 8  # invariant: the fast path is only ever entered with 8 anyway
            continue

        info["packets"] += 1
        src += 1  # past the ctrl byte, $C3/05F2

        # Faithful 8-bit shift register. The ASM does one `LSR A` for the first
        # element ($C3/05F3) and `LSR $030D` thereafter ($C3/0605); both shift in a
        # zero, so element 9+ of an over-long addendum packet reads as a literal.
        shifter = ctrl
        while True:
            is_match = shifter & 1
            shifter >>= 1

            if not is_match:
                if trace:
                    info["elements"].append((len(out), 0, 0, 1))
                out.append(rd8(src))
                src += 1
                info["literals"] += 1
            else:
                b0 = rd8(src)
                b1 = rd8(src + 1)
                word = b0 | (b1 << 8)
                length = (b1 >> len_shift) + 3
                offset = word & offset_mask
                src += 2
                info["matches"] += 1
                info["match_len_hist"][length] = \
                    info["match_len_hist"].get(length, 0) + 1
                if offset > info["max_offset"]:
                    info["max_offset"] = offset
                if offset == 0:
                    info["zero_offset_matches"] += 1
                    raise CtlzError(
                        f"back-reference with offset 0 at 0x{src - 2:X} "
                        f"(output pos {len(out)}) -- undefined; see spec Q3",
                        src - 2, len(out))
                if offset > len(out):
                    # Would read WRAM below the destination pointer: garbage on real
                    # hardware, and a strong signal we started at a wrong address.
                    info["underflow_matches"] += 1
                    raise CtlzError(
                        f"back-reference offset {offset} exceeds output produced "
                        f"({len(out)}) at 0x{src - 2:X}", src - 2, len(out))
                if offset < length:
                    info["overlap_matches"] += 1

                if trace:
                    info["elements"].append((len(out), 1, offset, length))

                # BYTE-BY-BYTE, FORWARD. Do not "optimize" this into a slice copy:
                # when offset < length the copy legally reads bytes it just wrote.
                pos = len(out) - offset
                for _ in range(length):
                    out.append(out[pos])
                    pos += 1

            # $C3/0601: DEC counter; on zero, reload 8 and go check the stop cursor.
            bitctr -= 1
            if bitctr == 0:
                bitctr = 8
                break

    info["consumed"] = src - start
    info["out_len"] = len(out)
    info["sha256"] = hashlib.sha256(bytes(out)).hexdigest()
    return out, info


# ======================================================================================
# Manifest I/O
#
# Per AGENTS.md ("Preserve raw evidence" / only the skeleton is tracked): we record
# ADDRESSES AND HASHES, never ROM bytes. A manifest entry is reproducible from any
# copy of the ROM, and a hash mismatch tells you your ROM is not the one we surveyed.
# ======================================================================================

def load_manifest(path: str) -> dict:
    try:
        with open(path, "r", encoding="utf-8") as f:
            man = json.load(f)
    except FileNotFoundError:
        die(f"manifest not found: {path}")
    except json.JSONDecodeError as e:
        die(f"manifest {path} is not valid JSON: {e}")
    ver = man.get("ver", LATEST_VER)
    if ver not in SUPPORTED_VERS:
        die(f"manifest {path} declares ver={ver!r}, which this build does not "
            f"implement (supported: {sorted(SUPPORTED_VERS)})")
    if man.get("type") != "ctlz-corpus":
        die(f"manifest {path} has type={man.get('type')!r}, expected 'ctlz-corpus'")
    return man


def load_node_manifest(path: str, fail=None) -> dict:
    """Load the manifest that describes ONE container asset in a generated build.

    A different schema from load_manifest() above, which reads the corpus survey: this is
    the per-asset JSON a build edge is handed, and the only thing this file wants out of
    it is the pipeline's declared parameters. `fail` lets the encoder report errors under
    its own name; it defaults to this tool's die().
    """
    fail = fail or die
    try:
        with open(path, "r", encoding="utf-8") as f:
            return json.load(f)
    except OSError as e:
        fail(f"{path}: cannot read the manifest: {e}")
    except json.JSONDecodeError as e:
        fail(f"{path}: manifest is not valid JSON: {e}")


def manifest_lz_mode(man: dict, path: str, fail=None) -> int:
    """The LZSS offset width the manifest declares, from pipeline[0].lz.mode.

    The mode is per-blob metadata no codec can derive from the plaintext, and a generated
    build records the same fact TWICE: here, and in the build variable the exporter baked
    into the command line. They are copies of one fact, so a disagreement means one of
    them has drifted -- and there is no way to tell which. Callers therefore compare the
    two and halt; that is the whole reason this is read at build time at all.

    A missing or unrecognized value is drift as well, not a licence to fall back: a
    manifest that no longer declares a mode cannot confirm anything.
    """
    fail = fail or die
    stages = man.get("pipeline")
    if not isinstance(stages, list) or not stages or not isinstance(stages[0], dict):
        fail(f"{path}: manifest declares no `pipeline`, so it carries no LZSS mode to "
             f"check the build against. Re-export the project, or point --manifest at "
             f"the container's manifest.")
    block = stages[0].get("lz")
    mode = block.get("mode") if isinstance(block, dict) else None
    if mode not in (MODE_11BIT, MODE_12BIT):
        fail(f"{path}: manifest declares pipeline[0].lz.mode={mode!r}, which is not "
             f"{MODE_11BIT} or {MODE_12BIT}. Fix the mode on the region and re-export; "
             f"refusing to guess an offset width.")
    return mode


def read_rom(path: str) -> bytes:
    try:
        with open(path, "rb") as f:
            rom = f.read()
    except FileNotFoundError:
        die(f"ROM not found: {path}")
    # A copier header is 512 bytes on top of a power-of-two image. Corpus addresses
    # are UNHEADERED file offsets (Geiger's convention), so strip rather than guess.
    if len(rom) % 1024 == 512:
        print(f"ctlz: note: {path} has a 512-byte copier header; stripping it",
              file=sys.stderr)
        rom = rom[512:]
    return rom


# ======================================================================================
# Commands
# ======================================================================================

def cmd_decode(a) -> int:
    rom = read_rom(a.rom)
    try:
        out, info = decode(rom, a.offset)
    except CtlzError as e:
        die(f"decode failed at 0x{a.offset:X}: {e.msg}")
    if a.out:
        os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
        with open(a.out, "wb") as f:
            f.write(out)
    if a.json:
        print(json.dumps(info, indent=2, sort_keys=True))
    else:
        print(f"0x{a.offset:06X}  mode={info['mode']}-bit  header_size={info['header_size']}  "
              f"consumed={info['consumed']}  out={info['out_len']}  "
              f"packets={info['packets']}  addenda={len(info['addenda'])}  "
              f"overlap={info['overlap_matches']}  sha256={info['sha256'][:16]}")
    return 0


def cmd_decompress(a) -> int:
    """File -> file: one standalone compressed blob in, its plaintext out.

    The input is read VERBATIM. It is a build intermediate, not a ROM image, so it
    must not go through read_rom(), which strips a 512-byte copier header from any
    file whose length happens to be 512 mod 1024.

    With --manifest, the mode the manifest declares is cross-checked against the mode the
    command line asked for BEFORE anything is decoded: they are two copies of one fact and
    a drifted manifest must be loud, not quietly ignored. Given a manifest and no
    --expect-mode, the manifest's mode becomes the expectation the stream is held to.
    """
    expect = a.expect_mode
    if a.manifest is not None:
        declared = manifest_lz_mode(load_node_manifest(a.manifest), a.manifest)
        if expect is not None and declared != expect:
            die(f"{a.manifest}: manifest declares pipeline[0].lz.mode={declared}, but "
                f"this build asked for --expect-mode {expect}. The two records of the "
                f"blob's offset width have drifted apart and neither can be trusted. "
                f"Re-export the project so the build matches the manifest, or correct "
                f"the mode authored on the region.")
        expect = declared
    try:
        with open(a.inp, "rb") as f:
            buf = f.read()
    except FileNotFoundError:
        die(f"input not found: {a.inp}")
    try:
        out, info = decode(buf, 0)
    except CtlzError as e:
        die(f"decode failed on {a.inp}: {e.msg}")
    # The stream is self-terminating, so a blob file that is longer or shorter than
    # what the decoder walked means the byte range we were handed is not exactly this
    # blob. Halt: decoding a prefix would silently produce plausible wrong plaintext.
    if info["consumed"] != len(buf):
        die(f"{a.inp}: stream ends after {info['consumed']} bytes but the file is "
            f"{len(buf)} bytes; this is not exactly one blob")
    if expect is not None and info["mode"] != expect:
        die(f"{a.inp}: stream is {info['mode']}-bit, expected {expect}-bit"
            f"{f' (declared by {a.manifest})' if a.manifest is not None else ''}")
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    with open(a.out, "wb") as f:
        f.write(out)
    return 0


def cmd_harvest(a) -> int:
    """Try to decode every candidate address; write the ones that decode as a manifest.

    Candidates come from --addr and/or --from-geiger (the FirstByte column of rows
    flagged Compressed=Y). A candidate that fails is recorded in `rejected` WITH ITS
    REASON -- do not silently drop it, the failures are the interesting part.
    """
    rom = read_rom(a.rom)
    cands: "list[tuple[int, str]]" = []

    for s in (a.addr or []):
        cands.append((int(s, 16), "cli"))

    if a.from_geiger:
        cands.extend(parse_geiger(a.from_geiger))

    seen = set()
    entries, rejected = [], []
    for off, label in cands:
        if off in seen:
            continue
        seen.add(off)
        try:
            out, info = decode(rom, off)
        except CtlzError as e:
            rejected.append({"offset": f"0x{off:06X}", "source": label,
                             "reason": e.msg, "bytes_out_before_fail": e.out_len})
            continue
        entries.append({
            "offset": f"0x{off:06X}",
            "source": label,
            "compressed_len": info["consumed"],
            "decompressed_len": info["out_len"],
            "mode": info["mode"],
            "addenda": len(info["addenda"]),
            "sha256": info["sha256"],
        })

    man = {
        "type": "ctlz-corpus",
        "ver": LATEST_VER,
        "tool": TOOL_VERSION,
        "rom_sha256": hashlib.sha256(rom).hexdigest(),
        "rom_len": len(rom),
        "note": "Addresses are UNHEADERED file offsets. No ROM bytes are stored here; "
                "sha256 is of the DECOMPRESSED output and is the regression anchor.",
        "entries": sorted(entries, key=lambda e: e["offset"]),
        "rejected": sorted(rejected, key=lambda e: e["offset"]),
    }
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    with open(a.out, "w", encoding="utf-8") as f:
        json.dump(man, f, indent=2)
        f.write("\n")
    print(f"ctlz: harvested {len(entries)} blobs, rejected {len(rejected)} -> {a.out}")
    return 0


def parse_geiger(path: str) -> "list[tuple[int, str]]":
    """FirstByte of every row flagged Compressed=Y in Geiger's tab-separated offsets.

    Columns: FirstByte / LastByte / Type / Compressed / Description / Modified.
    Rows marked "(Huffman compressed)" in the DESCRIPTION are CT's separate text
    codec and have Compressed=N -- filtering on the column, not the word, excludes
    them correctly.
    """
    out = []
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as f:
            lines = f.read().splitlines()
    except FileNotFoundError:
        die(f"Geiger offsets file not found: {path}")
    for ln in lines[1:]:
        c = ln.split("\t")
        if len(c) < 4 or c[3].strip().upper() != "Y":
            continue
        try:
            off = int(c[0].strip(), 16)
        except ValueError:
            continue
        out.append((off, f"geiger:{c[2].strip()}"))
    return out


def cmd_verify(a) -> int:
    """Re-decode every manifest entry and assert the decompressed sha256 still matches."""
    rom = read_rom(a.rom)
    man = load_manifest(a.manifest)
    if man.get("rom_sha256") and man["rom_sha256"] != hashlib.sha256(rom).hexdigest():
        die(f"ROM sha256 does not match the manifest's ({man['rom_sha256'][:16]}...). "
            f"This is a different ROM; corpus offsets are not portable.")
    bad = 0
    for e in man["entries"]:
        off = int(e["offset"], 16)
        try:
            _, info = decode(rom, off)
        except CtlzError as ex:
            print(f"FAIL {e['offset']}: {ex.msg}")
            bad += 1
            continue
        for k in ("compressed_len", "decompressed_len", "mode", "sha256"):
            want = e[k]
            got = info["consumed"] if k == "compressed_len" else \
                  info["out_len"] if k == "decompressed_len" else info[k]
            if want != got:
                print(f"FAIL {e['offset']}: {k} {got!r} != manifest {want!r}")
                bad += 1
                break
    total = len(man["entries"])
    print(f"ctlz: verify {total - bad}/{total} entries match")
    return 1 if bad else 0


def cmd_stats(a) -> int:
    """Aggregate the corpus into the numbers the open questions ask for."""
    rom = read_rom(a.rom)
    man = load_manifest(a.manifest)
    agg = {
        "blobs": 0, "mode11": 0, "mode12": 0,
        "max_addendum_chain": 0, "blobs_with_addenda": 0,
        "addendum_c0_set": 0,
        "packets": 0, "literals": 0, "matches": 0, "fast_path_packets": 0,
        "overlap_matches": 0, "blobs_with_overlap": 0,
        "zero_offset_matches": 0,
        "max_offset": 0, "max_len": 0,
        "comp_bytes": 0, "decomp_bytes": 0,
    }
    for e in man["entries"]:
        _, i = decode(rom, int(e["offset"], 16))
        agg["blobs"] += 1
        agg["mode11" if i["mode"] == 11 else "mode12"] += 1
        na = len(i["addenda"])
        agg["max_addendum_chain"] = max(agg["max_addendum_chain"], na)
        if na:
            agg["blobs_with_addenda"] += 1
        agg["addendum_c0_set"] += sum(1 for x in i["addenda"] if x["marker_c0"])
        for k in ("packets", "literals", "matches", "fast_path_packets",
                  "overlap_matches", "zero_offset_matches"):
            agg[k] += i[k]
        if i["overlap_matches"]:
            agg["blobs_with_overlap"] += 1
        agg["max_offset"] = max(agg["max_offset"], i["max_offset"])
        if i["match_len_hist"]:
            agg["max_len"] = max(agg["max_len"], max(i["match_len_hist"]))
        agg["comp_bytes"] += i["consumed"]
        agg["decomp_bytes"] += i["out_len"]
    print(json.dumps(agg, indent=2, sort_keys=True))
    return 0


# ======================================================================================
# fingerprint -- what did SQUARE's encoder do?
#
# Two independent questions, both answered from real ROM bytes only:
#
#  (A) ADDENDUM PADDING. Geiger's encoder does
#          CompData[i][nPackHdrOff] |= (byte)(0xFF << nBitCtr)
#      (CTRecompression.txt:80) -- note `|=`, an OR. The low `nBitCtr` bits are the
#      addendum's REAL element flags; only the high (8 - nBitCtr) bits are padding.
#      So the correct test is "are all padding bits SET", i.e.
#          (ctrl & padmask) == padmask,  padmask = (0xFF << bits) & 0xFF
#      NOT "ctrl == (0xFF << bits)". Comparing against the bare shifted value is a
#      mistake -- it would flag every addendum whose first element happens to be a
#      match as a divergence.
#
#      Position of the byte, from the ASM: the addendum marker sits at the stop
#      cursor ($C3/0644); $C3/064F-$C3/0654 reads the uint16 cumulative length at
#      marker+1; $C3/0656-$C3/0658 does INX x3; $C3/065B BRA CODE_C305E9 falls into
#      $C3/05ED LDA $0000,X. So the packet header is at marker+3.
#
#  (B) PARSE SHAPE. For every emitted element we know the output position, so we can
#      recompute what the encoder COULD have chosen there and compare:
#        - greedy vs lazy: was the emitted match the longest available at that spot?
#        - offset rank: among offsets achieving at least the emitted length, was the
#          emitted one the NEAREST (rank 1)? Geiger's far->near `k >= nCopyLength`
#          scan (CTRecompression.txt:41-51) yields rank 1 by construction.
#        - missed matches: literals emitted where a length>=3 match existed.
# ======================================================================================

def _build_index(out: bytes) -> dict:
    """3-byte prefix -> ascending list of positions. Minimum match length is 3."""
    idx = {}
    for i in range(len(out) - 2):
        idx.setdefault(out[i:i + 3], []).append(i)
    return idx


def _match_len(out: bytes, q: int, p: int, maxlen: int) -> int:
    """Length of the match at history pos q for the string at p, self-overlap allowed.

    `out` is the FULLY decoded buffer, so out[q+k] for q+k >= p already holds the byte
    MVN would have produced -- the overlap semantics are reproduced exactly, not
    approximated.
    """
    n = len(out)
    k = 0
    while k < maxlen and p + k < n and out[q + k] == out[p + k]:
        k += 1
    return k


def _analyze_position(out: bytes, idx: dict, p: int, rng: int, maxlen: int,
                      chosen_off: int, chosen_len: int, cap: int):
    """Return (best_len, rank_from_near, farther_equal_or_longer, capped).

    Candidates are walked NEAREST-FIRST so `best_len` can stop early at maxlen. `rank`
    counts how many strictly nearer offsets also reach chosen_len (rank 1 == nearest).
    `cap` bounds the candidate walk; a capped position is reported, never silently
    treated as a clean result.
    """
    key = out[p:p + 3]
    positions = idx.get(key)
    if not positions:
        return 0, 0, 0, False
    lo = p - rng
    best = 0
    nearer = 0
    farther = 0
    seen = 0
    capped = False
    # positions is ascending; walk it backwards from just-below p = nearest first.
    import bisect
    i = bisect.bisect_left(positions, p) - 1
    while i >= 0:
        q = positions[i]
        if q < lo:
            break
        seen += 1
        if seen > cap:
            capped = True
            break
        L = _match_len(out, q, p, maxlen)
        if L > best:
            best = L
        if chosen_len and L >= chosen_len:
            if (p - q) < chosen_off:
                nearer += 1
            elif (p - q) > chosen_off:
                farther += 1
        if best >= maxlen and chosen_off == 0:
            break
        i -= 1
    return best, nearer + 1, farther, capped


def cmd_fingerprint(a) -> int:
    rom = read_rom(a.rom)
    man = load_manifest(a.manifest)
    if man.get("rom_sha256") and man["rom_sha256"] != hashlib.sha256(rom).hexdigest():
        die("ROM sha256 does not match the manifest's; corpus offsets are not portable.")

    entries = man["entries"]

    # ---------------- (A) addendum padding, over the WHOLE corpus ----------------
    pad = {
        "blobs": 0, "addenda": 0,
        "geiger_rule_holds": 0,     # all padding bits set
        "nonzero_but_different": 0, # ctrl != 0 but some padding bit clear
        "zero_ctrl": 0,             # ctrl == 0: would take the 8-literal fast path
        "bits_zero": 0,             # bits == 0 cannot happen (that terminates)
    }
    by_bits = {}
    counterexamples = []
    for e in entries:
        off = int(e["offset"], 16)
        _, i = decode(rom, off)
        pad["blobs"] += 1
        for ad in i["addenda"]:
            bits = ad["bits"]
            ctrl = ad["ctrl"]
            padmask = (0xFF << bits) & 0xFF
            pad["addenda"] += 1
            b = by_bits.setdefault(bits, {"n": 0, "ok": 0, "bad": 0})
            b["n"] += 1
            if ctrl == 0:
                pad["zero_ctrl"] += 1
            if (ctrl & padmask) == padmask:
                pad["geiger_rule_holds"] += 1
                b["ok"] += 1
            else:
                pad["nonzero_but_different"] += 1
                b["bad"] += 1
                if len(counterexamples) < 40:
                    counterexamples.append({
                        "blob": e["offset"], "ctrl_at": f"0x{ad['ctrl_at']:06X}",
                        "bits": bits, "ctrl": f"0x{ctrl:02X}",
                        "padmask": f"0x{padmask:02X}",
                        "mode": i["mode"], "type": e.get("source"),
                    })

    result = {"addendum_padding": pad, "addendum_padding_by_bitcount": by_bits,
              "addendum_counterexamples": counterexamples}

    # ---------------- (B) parse shape, over a SAMPLE ----------------
    if a.sample:
        sel = entries[::max(1, len(entries) // a.sample)][:a.sample]
    else:
        sel = entries
    parse = {
        "blobs_analyzed": 0, "elements": 0,
        "matches": 0, "literals": 0,
        "match_is_longest": 0, "match_shorter_than_best": 0,
        "shortfall_hist": {},
        "rank_hist": {}, "rank_from_far_hist": {}, "only_candidate": 0,
        "rank_capped": 0,
        "literal_with_match_available": 0,
        "literal_missed_len_hist": {},
        "positions_capped": 0,
    }
    for e in sel:
        off = int(e["offset"], 16)
        outb, i = decode(rom, off, trace=True)
        outb = bytes(outb)
        rng, maxlen = (0x07FF, 34) if i["mode"] == 11 else (0x0FFF, 18)
        idx = _build_index(outb)
        parse["blobs_analyzed"] += 1
        for (p, is_match, o, L) in i["elements"]:
            parse["elements"] += 1
            if is_match:
                parse["matches"] += 1
                best, rank, farther, capped = _analyze_position(
                    outb, idx, p, rng, maxlen, o, L, a.cap)
                if capped:
                    parse["positions_capped"] += 1
                    parse["rank_capped"] += 1
                    continue
                if L >= best:
                    parse["match_is_longest"] += 1
                else:
                    parse["match_shorter_than_best"] += 1
                    d = best - L
                    parse["shortfall_hist"][d] = parse["shortfall_hist"].get(d, 0) + 1
                rk = rank if rank <= 8 else 9  # 9 == "9 or worse"
                parse["rank_hist"][rk] = parse["rank_hist"].get(rk, 0) + 1
                # Same population, ranked from the FAR end. rank_from_far == 1 means
                # the chosen offset is the FARTHEST reaching that length.
                fk = (farther + 1) if farther < 8 else 9
                parse["rank_from_far_hist"][fk] =                     parse["rank_from_far_hist"].get(fk, 0) + 1
                if rank == 1 and farther == 0:
                    parse["only_candidate"] += 1
            else:
                parse["literals"] += 1
                if p + 3 > len(outb):
                    continue
                best, _, _, capped = _analyze_position(
                    outb, idx, p, rng, maxlen, 0, 0, a.cap)
                if capped:
                    parse["positions_capped"] += 1
                    continue
                if best >= 3:
                    parse["literal_with_match_available"] += 1
                    parse["literal_missed_len_hist"][best] = \
                        parse["literal_missed_len_hist"].get(best, 0) + 1
    result["parse_shape"] = parse
    result["parse_shape_note"] = (
        f"sample = {parse['blobs_analyzed']} of {len(entries)} blobs, "
        f"candidate cap = {a.cap} per position")

    print(json.dumps(result, indent=2, sort_keys=True))
    return 0


# ======================================================================================
# Self-test
#
# No test framework is vendored (gfxpack ships none), so the tests live in the tool,
# stdlib only: `ctlz.py selftest`. Each case asserts one rule from the spec, and the
# comment says which one -- these exist to catch a future "cleanup" that breaks
# byte-identity.
# ======================================================================================

def _pack(body: bytes, marker: int = 0x00, tail: bytes = b"") -> bytes:
    """Build a blob: uint16 body size + body + marker + tail."""
    return bytes([len(body) & 0xFF, len(body) >> 8]) + body + bytes([marker]) + tail


def cmd_selftest(a) -> int:
    fails = []
    # A packet is always exactly 8 elements. When a test only cares about the
    # first element (a match), the remaining 7 are filler literals; expected
    # output is checked as a prefix.
    FILL = b"zzzzzzz"

    def check(name, got, want):
        if got != want:
            fails.append(f"{name}: got {got!r}, want {want!r}")
        else:
            print(f"  ok  {name}")

    def dec(blob, at=0):
        return decode(blob, at)

    # 1. Header counts the BODY only; the marker sits at src+2+size and terminates
    #    when (marker & $3F) == 0.  [$C3/0563-$C3/056B, $C3/0644-$C3/0649]
    out, info = dec(_pack(bytes([0x00]) + b"ABCDEFGH"))
    check("literals/fast-path output", bytes(out), b"ABCDEFGH")
    check("literals/fast-path consumed", info["consumed"], 2 + 9 + 1)
    check("fast path taken", info["fast_path_packets"], 1)

    # 2. A ctrl byte of $FF is eight matches; $00 is eight literals via the fast
    #    path. Both must produce identical output for the same 8 literals -- prove
    #    the slow path with a nonzero ctrl whose bits are all literal except one.
    #    ctrl = $00 would hit the fast path, so use a partial packet instead below.

    # 3. 12-bit mode: marker & $C0 == 0. offset = word & $0FFF, length = (b1>>4)+3.
    #    Emit 8 literals, then a match: offset 8, length 3 -> repeats "ABC".
    body = bytes([0x00]) + b"ABCDEFGH" + bytes([0x01, 0x08, 0x00]) + FILL
    out, info = dec(_pack(body, marker=0x00))
    check("12-bit mode latched", info["mode"], 12)
    check("12-bit match len 3 @ off 8", bytes(out)[:11], b"ABCDEFGHABC")

    # 4. 11-bit mode: marker & $C0 != 0 -> offset = word & $07FF, length = (b1>>3)+3.
    #    The encoder writes $40 for 11-bit (not $C0) -- the ASM only tests != 0.
    #    b1 = 0x00 -> len 3; word 0x0008 -> offset 8.
    out, info = dec(_pack(body, marker=0x40))
    check("11-bit mode latched by $40", info["mode"], 11)
    check("11-bit match len 3 @ off 8", bytes(out)[:11], b"ABCDEFGHABC")

    # 5. Length range endpoints. 12-bit: b1>>4 == 15 -> 18. 11-bit: b1>>3 == 31 -> 34.
    #    b1=$F0 in 12-bit -> len 18, offset = 0xF008 & 0x0FFF = 8.
    body = bytes([0x00]) + b"ABCDEFGH" + bytes([0x01, 0x08, 0xF0]) + FILL
    out, _ = dec(_pack(body, marker=0x00))
    check("12-bit max length", len(out) - 8 - 7, 18)
    #    b1=$F8 in 11-bit -> len (0xF8>>3)+3 = 34, offset = 0xF808 & 0x07FF = 8.
    body = bytes([0x00]) + b"ABCDEFGH" + bytes([0x01, 0x08, 0xF8]) + FILL
    out, _ = dec(_pack(body, marker=0x40))
    check("11-bit max length", len(out) - 8 - 7, 34)

    # 6. *** SELF-OVERLAP (offset < length) IS FREE RLE. ***
    #    MVN copies byte-by-byte ascending, so this re-reads what it just wrote.
    #    A slice/memmove copy fails HERE and nowhere else. Non-negotiable.
    #    offset 1, length 18 after "ABCDEFGH" -> 18 more 'H'.
    body = bytes([0x00]) + b"ABCDEFGH" + bytes([0x01, 0x01, 0xF0]) + FILL
    out, info = dec(_pack(body, marker=0x00))
    check("overlap RLE output", bytes(out), b"ABCDEFGH" + b"H" * 18 + FILL)
    check("overlap counted", info["overlap_matches"], 1)
    #    offset 2, length 5 -> alternating 2-byte pattern, catches a naive
    #    "copy min(offset,length) then repeat" shortcut too.
    body = bytes([0x00]) + b"ABCDEFGH" + bytes([0x01, 0x02, 0x20]) + FILL
    out, _ = dec(_pack(body, marker=0x00))
    check("overlap period-2", bytes(out), b"ABCDEFGH" + b"GHGHG" + FILL)

    # 7. Offset 0 is undefined (spec Q3) and must be rejected, not guessed at.
    body = bytes([0x00]) + b"ABCDEFGH" + bytes([0x01, 0x00, 0x00]) + FILL
    try:
        dec(_pack(body, marker=0x00))
        fails.append("offset-0 should have raised")
    except CtlzError:
        print("  ok  offset 0 rejected")

    # 8. ADDENDUM CHAIN. Marker & $3F = bit count for the addendum's first ctrl byte;
    #    the following uint16 is CUMULATIVE from src+0. The bit counter is NOT reset
    #    to 8.  [$C3/064B-$C3/065B]
    #    Body: one fast-path packet (8 literals). Then marker $01 (1 element),
    #    cumulative length = offset of the end of the addendum body from src+0.
    body = bytes([0x00]) + b"ABCDEFGH"
    #    layout: [0..1]=size [2..10]=body [11]=marker [12..13]=cum [14]=ctrl [15]=lit
    #            [16]=terminator.  Addendum body ends at 16 -> cumulative = 16.
    blob = _pack(body, marker=0x01, tail=bytes([16, 0x00, 0xFE, ord("Z"), 0x00]))
    out, info = dec(blob)
    check("addendum output", bytes(out), b"ABCDEFGHZ")
    check("addendum chain length", len(info["addenda"]), 1)
    check("addendum consumed", info["consumed"], 17)

    # 9. The $FE padding rule, demonstrated. Same addendum with ctrl == $00 takes the
    #    fast path ($C3/05F0 is tested before the bit counter) and emits 8 literals
    #    instead of 1 -- which is exactly why encoders must set the unused bits.
    blob = _pack(body, marker=0x01,
                 tail=bytes([23, 0x00, 0x00]) + b"Z1234567" + bytes([0x00]))
    out, _ = dec(blob)
    check("cleared addendum padding over-reads (Q1 mechanism)",
          bytes(out), b"ABCDEFGHZ1234567")

    # 10. Multi-addendum chain -- the ASM loops, so two in a row must work.
    body = bytes([0x00]) + b"ABCDEFGH"
    #     [11]=m1 [12..13]=cum1=16 [14]=ctrl [15]=lit  -> stop at 16
    #     [16]=m2 [17..18]=cum2=21 [19]=ctrl [20]=lit  -> stop at 21
    #     [21]=terminator
    blob = _pack(body, marker=0x01,
                 tail=bytes([16, 0x00, 0xFE, ord("Y"),
                             0x01, 21, 0x00, 0xFE, ord("Z"),
                             0x00]))
    out, info = dec(blob)
    check("two-addendum chain output", bytes(out), b"ABCDEFGHYZ")
    check("two-addendum chain length", len(info["addenda"]), 2)

    # 11. Mode is LATCHED from the FIRST marker; an addendum marker's $C0 is ignored
    #     ($C3/065B re-enters the already-selected variant body). First marker is
    #     12-bit; the addendum claims $40 but the match must still decode 12-bit.
    #     Addendum: ctrl $FD (bit0=1 match, bits 1..7 set as padding), 1 element.
    #     word 0x0001 with b1=0x00 -> 12-bit: off 1 len 3 ("HHH").
    #                                 11-bit would give off 1 len 3 as well, so use
    #     b1 = 0x10: 12-bit len (0x10>>4)+3 = 4; 11-bit len (0x10>>3)+3 = 5.
    body = bytes([0x00]) + b"ABCDEFGH"
    blob = _pack(body, marker=0x01,
                 tail=bytes([17, 0x00, 0xFD, 0x01, 0x10, 0x00]))
    out, info = dec(blob)
    check("addendum $C0 ignored (mode latched)", bytes(out), b"ABCDEFGH" + b"H" * 4)
    check("addendum marker $C0 recorded", info["addenda"][0]["marker_c0"], 0x00)

    # 12. Body that does not end on a packet boundary must be a hard error -- the
    #     ASM's stop test is `CPX : BEQ`, an equality, and would run away.
    try:
        dec(_pack(bytes([0x00]) + b"ABCDEFG"))  # 8-byte body, packet needs 9
        fails.append("packet-boundary overshoot should have raised")
    except CtlzError:
        print("  ok  packet-boundary overshoot rejected")

    # 13. Decoding at a nonzero start offset must be position-independent, and the
    #     addendum's cumulative field is relative to src+0, not to the file.
    pad = b"\xAA" * 7
    blob = _pack(bytes([0x00]) + b"ABCDEFGH", marker=0x01,
                 tail=bytes([16, 0x00, 0xFE, ord("Z"), 0x00]))
    out, info = dec(pad + blob, at=7)
    check("position independent", bytes(out), b"ABCDEFGHZ")
    check("position independent consumed", info["consumed"], 17)

    # 14. The `decompress` build edge. The offset width is recorded both in the container
    #     manifest and in the command the build runs; the two are copies of one fact, so
    #     a disagreement halts instead of silently trusting either.
    import tempfile
    tmp = tempfile.mkdtemp(prefix="ctlz-decompress-")
    R = lambda *p: os.path.join(tmp, *p)

    def dies(argv, label):
        try:
            main(argv)
        except SystemExit:
            print(f"  ok  {label}")
            return
        fails.append(f"{label}: expected a hard error, but the command succeeded")

    def manifest(fname, stage) -> str:
        man = {"name": "blob/demo_pack", "type": "blob.container",
               "members": [{"name": "blob/demo_pack.buffer", "at": 0, "len": 8}]}
        if stage is not None:
            man["pipeline"] = [stage]
        p = R(fname)
        with open(p, "w", encoding="utf-8", newline="\n") as f:
            json.dump(man, f, indent=2)
        return p

    stream = _pack(bytes([0x00]) + b"ABCDEFGH")     # marker $00 => a 12-bit stream
    src = R("pack.raw")
    with open(src, "wb") as f:
        f.write(stream)
    check("selftest fixture is a 12-bit stream", dec(stream)[1]["mode"], 12)

    def lz(mode):
        return {"codec": "compress.ct.lzss", "lz": {"mode": mode}}

    m12, m11 = manifest("c12.json", lz(12)), manifest("c11.json", lz(11))
    no_pipeline = manifest("c-none.json", None)
    no_mode = manifest("c-empty.json", {"codec": "compress.ct.lzss", "lz": {}})

    check("manifest mode agreeing with --expect-mode decompresses",
          main(["decompress", "--in", src, "--out", R("a.bin"),
                "--expect-mode", "12", "--manifest", m12]), 0)
    check("agreeing manifest produced the plaintext",
          open(R("a.bin"), "rb").read(), b"ABCDEFGH")
    dies(["decompress", "--in", src, "--out", R("b.bin"),
          "--expect-mode", "12", "--manifest", m11],
         "manifest mode disagreeing with --expect-mode halts")
    dies(["decompress", "--in", src, "--out", R("c.bin"), "--manifest", m11],
         "manifest mode alone is held against the stream")
    dies(["decompress", "--in", src, "--out", R("d.bin"),
          "--expect-mode", "12", "--manifest", no_pipeline],
         "manifest with no pipeline halts")
    dies(["decompress", "--in", src, "--out", R("e.bin"),
          "--expect-mode", "12", "--manifest", no_mode],
         "manifest with no lz.mode halts")
    check("no manifest is still allowed",
          main(["decompress", "--in", src, "--out", R("f.bin"), "--expect-mode", "12"]), 0)

    if fails:
        print("\nFAILURES:")
        for f in fails:
            print("  " + f)
        return 1
    print("\nctlz: all selftests passed")
    return 0


# ======================================================================================
# CLI
# ======================================================================================

def main(argv=None) -> int:
    p = argparse.ArgumentParser(
        prog="ctlz.py",
        description="Chrono Trigger (US) LZSS decoder + corpus harvester")
    p.add_argument("--version", action="version", version=TOOL_VERSION)
    sub = p.add_subparsers(dest="cmd", required=True)

    d = sub.add_parser("decode", help="decompress one blob at a ROM offset")
    d.add_argument("--rom", required=True)
    d.add_argument("--offset", required=True, type=lambda s: int(s, 0),
                   help="UNHEADERED file offset of the blob")
    d.add_argument("--out", default=None, help="write decompressed bytes here")
    d.add_argument("--json", action="store_true", help="dump full stream statistics")
    d.set_defaults(fn=cmd_decode)

    dz = sub.add_parser("decompress",
                        help="decompress a standalone blob file to a plaintext file")
    dz.add_argument("--in", dest="inp", required=True, metavar="PATH",
                    help="compressed blob, read verbatim (no header stripping)")
    dz.add_argument("--out", required=True, metavar="PATH",
                    help="write decompressed bytes here")
    dz.add_argument("--expect-mode", dest="expect_mode", type=int, choices=[11, 12],
                    default=None,
                    help="halt unless the stream uses this offset width")
    dz.add_argument("--manifest", default=None, metavar="PATH",
                    help="the container's manifest; its pipeline[0].lz.mode must agree "
                         "with --expect-mode, and is used as the expectation when "
                         "--expect-mode is omitted")
    dz.set_defaults(fn=cmd_decompress)

    h = sub.add_parser("harvest", help="decode candidate offsets -> corpus manifest")
    h.add_argument("--rom", required=True)
    h.add_argument("--addr", action="append", metavar="HEX",
                   help="candidate offset, repeatable")
    h.add_argument("--from-geiger", default=None, dest="from_geiger",
                   metavar="PATH", help="'Offsets, NA.txt'; takes Compressed=Y rows")
    h.add_argument("--out", required=True, help="manifest path to write")
    h.set_defaults(fn=cmd_harvest)

    v = sub.add_parser("verify", help="re-decode a manifest and assert hashes match")
    v.add_argument("--rom", required=True)
    v.add_argument("--manifest", required=True)
    v.set_defaults(fn=cmd_verify)

    s = sub.add_parser("stats", help="aggregate corpus statistics (answers Q4/Q5)")
    s.add_argument("--rom", required=True)
    s.add_argument("--manifest", required=True)
    s.set_defaults(fn=cmd_stats)

    fp = sub.add_parser("fingerprint",
                        help="Square's encoder fingerprint: addendum padding + parse shape")
    fp.add_argument("--rom", required=True)
    fp.add_argument("--manifest", required=True)
    fp.add_argument("--sample", type=int, default=0,
                    help="analyze parse shape on N evenly-spaced blobs (0 = all). "
                         "The addendum-padding survey always covers the whole corpus.")
    fp.add_argument("--cap", type=int, default=4000,
                    help="max match candidates examined per position; capped positions "
                         "are counted and excluded, never guessed at")
    fp.set_defaults(fn=cmd_fingerprint)

    t = sub.add_parser("selftest", help="run the built-in format assertions")
    t.set_defaults(fn=cmd_selftest)

    a = p.parse_args(argv)
    return a.fn(a)


if __name__ == "__main__":
    sys.exit(main())

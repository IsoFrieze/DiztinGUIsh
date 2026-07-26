#!/usr/bin/env python3
"""
ctlzpack -- Chrono Trigger (US) LZSS *encoder* + byte-identity harness.

Companion to ctlz.py (the verified decoder). This file NEVER modifies ctlz.py; it
imports its decoder. Same idiom: Python 3, stdlib only, argparse subcommands, die().

Canonical format spec: docs/chrono-trigger/compression-format.md.
Primary evidence for the DECODER side is $C3/0557-$C3/08B2 in generated/bank_C3.asm.
Primary evidence for the ENCODER side is Geiger's 2003 C#,
docs/chrono-trigger/prior-art/evilpeer-2003/CTRecompression.txt -- which is NOT the
game and NOT Square's encoder. Everything the encoder does is therefore a HYPOTHESIS
about Square's encoder, and the harness is what tests it.


=========================== WHY A HARNESS AT ALL ============================

We have a perfect oracle. For all 1302 corpus blobs the ROM holds Square's original
compressed bytes AND ctlz.py yields the plaintext they decode to. So:

    plaintext -> our encoder -> must equal Square's bytes, exactly.

Three outcomes, tracked separately and never conflated:

    IDENTICAL   our bytes == Square's bytes.                     <- the goal
    DIVERGENT   different bytes, but they decode back to the
                correct plaintext.                               <- useful signal
    BROKEN      re-decode fails, or yields the wrong plaintext.  <- OUR BUG

BROKEN is always an encoder defect, never "just a different strategy". It is reported
loudly and separately.


========================= THE STREAM WE MUST EMIT ===========================

From compression-format.md (all [ASM]-verified):

    +0   uint16 SIZE of the body only
    +2   BODY: packets of 1 ctrl byte + 8 elements (LSB-first; 0=literal, 1=match)
    +2+SIZE  MARKER byte; (marker & $3F)==0 ends the stream, else it is the bit
             count of an addendum, followed by uint16 CUMULATIVE length from +0.

Hard constraints on any encoder:

  * The stop test at $C3/05E9 is `CPX $0309 : BEQ` -- EQUALITY. The body must end
    EXACTLY on a packet boundary or real hardware sails past the marker. This is why
    the leftover 1..7 elements go in the addendum rather than in a short final packet.
  * An addendum's ctrl byte must be NONZERO ($C3/05F0 `BEQ` is tested before the bit
    counter -- see ctlz.py's docstring). Geiger pads with `0xFF << nBitCtr`.
  * Length 3..18 (12-bit mode) / 3..34 (11-bit mode); MVN copies C+1 bytes.
  * Back-references are byte-by-byte forward, so offset < length (self-overlap) is
    legal and pervasive -- 13.5% of Square's matches. The match finder here CAN and
    DOES emit it (the k-loop compares src[j+k] against src[pos+k] with j+k allowed to
    run into the not-yet-emitted region, which is exactly how Geiger gets overlap).
  * 11-bit mode's marker byte is $40, not $C0 -- `(byte)(0xC0*(i-1))`. [OBSERVED in
    Square's data too: only $00-$07 and $40-$47 appear across 1302 first markers.]


==================== GEIGER'S QUIRKS, PORTED VERBATIM =======================

Reproduced exactly (see CTRecompression.txt line numbers):

  Q1 :27-29  two passes. i=0 -> range $07FF, maxcopy 34, shift 3, marker $40.
             i=1 -> range $0FFF, maxcopy 18, shift 4, marker $00.
             Pass 1 commits only if STRICTLY smaller; ties keep i=0 (11-bit).
             Note the size comparison is apples-to-oranges in his code -- the loop
             guard `nWorkPos < nCompSize` compares a work POSITION against pass 0's
             BODY SIZE. Ported as written.
  Q2 :41-51  match search scans j from (pos - range) ASCENDING toward pos; accept
             test is `k >= nCopyLength`, so ties resolve to the NEAREST offset.
             `k == nMaxCopy` breaks out early -> that one takes the FARTHEST.
  Q3 :59-69  nCopyLength is zeroed only on the MATCH path (:65). The literal path
             (:68) leaves it, so a stale length -- and a stale ABSOLUTE nOffset --
             leak into the next position's search. Almost certainly a bug in his
             encoder. Ported faithfully; `leak=off` is the fixed variant.
  Q4 :80     addendum ctrl padding `0xFF << nBitCtr`.
  Q5 :86     final byte (the terminator) has $3F clear -- he writes $00.
  Q6 :30-37  the first element is ALWAYS a literal: nSrcPos=1, nBitCtr=1, and
             CompData[3] = src[0]. Position 0 is never even searched.

Deliberate deviations, each flagged where it happens:
  * His k-loop reads SrcBuffer past nDecompressedSize (no bound). We clamp k to the
    bytes actually remaining. Reading past the end is undefined in C# too (it would
    throw or read the next asset); a match longer than the remaining data cannot be
    emitted correctly by ANY encoder. Marked DEVIATION-1 in the code.
  * His buffers are fixed 0x10000 and his positions are ushort. We use Python ints
    and report an error if the body would exceed 16 bits, rather than wrapping.


=========================== THE SWEEP AXES =================================

    --tiebreak nearest|farthest   `k >= cur` vs `k > cur` (ties to near vs far)
    --leak on|off                 Geiger's nCopyLength/nOffset state leak
    --lazy N                      0 = pure greedy; N = look ahead N positions and
                                  take a literal if a strictly longer match starts
                                  there
    --mode both|11|12             which pass(es) to run
    --mode-tie 11|12              who wins a size tie in `both` (Geiger: 11)

NOTE ON A NON-AXIS: the task list asked about emitting the `ctrl == 0` eight-literal
fast path "opportunistically vs only when forced". There is no such choice. The fast
path is not a distinct encoding -- a packet whose eight elements are all literals HAS
a ctrl byte of $00 by construction, and the decoder's $C3/05F0 test then handles it.
An encoder cannot emit eight literals any other way, and cannot avoid emitting $00
when it does. The axis does not exist; it is not swept. (The one place $00 is a real
choice is the ADDENDUM, where it is forbidden -- hence the padding rule.)
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ctlz  # noqa: E402  -- the verified decoder; we import, never edit

TOOL_VERSION = "ctlzpack/1.0.0"

die = ctlz.die


# ======================================================================================
# Strategy
# ======================================================================================

class Strategy:
    """One point in the sweep space. Hashable name so results can be tabulated."""

    def __init__(self, tiebreak="nearest", leak=True, lazy=0,
                 mode="both", mode_tie="11", tailpad=True):
        self.tiebreak = tiebreak      # "nearest" (k>=cur) | "farthest" (k>cur)
        self.leak = leak              # Geiger's nCopyLength/nOffset state leak
        self.lazy = lazy              # 0,1,2 -- lookahead depth
        self.mode = mode              # "both" | "11" | "12"
        self.mode_tie = mode_tie      # which pass wins an exact size tie
        self.tailpad = tailpad        # quirk Q7, no-addendum trailing $00

    @property
    def name(self) -> str:
        return (f"tie={self.tiebreak},leak={'on' if self.leak else 'off'},"
                f"lazy={self.lazy},mode={self.mode},mtie={self.mode_tie},"
                f"tailpad={'on' if self.tailpad else 'off'}")

    def __repr__(self):
        return f"<Strategy {self.name}>"


GEIGER = Strategy()  # the verbatim 2003 baseline


# ======================================================================================
# Match finder
#
# Geiger's inner loop is O(range * maxcopy) per source position, which over 3.8 MB of
# corpus plaintext is not tractable in Python. This finder produces the IDENTICAL
# answer while skipping candidates that provably cannot be accepted.
#
# The equivalence argument, stated so it can be checked:
#   The accepted value is monotone non-decreasing across the ascending j scan (each
#   accept requires k >= the current value). Therefore the final state is
#       (M, last j attaining M)      where M = max_j k(j)
#   for `nearest`, and (M, first j attaining M) for `farthest` -- UNLESS some j hits
#   k == nMaxCopy first, which breaks the loop and takes that j regardless.
#   If M < the stale incoming value, NOTHING is accepted and both nCopyLength and
#   nOffset keep their stale values. That is quirk Q3 and it is preserved.
#   So we only need M and the right extremal j. Any j with k < 3 can only matter when
#   nothing in the window reaches 3, which we detect and handle with a small exact
#   scan. `selftest` cross-checks this finder against a literal transcription of
#   Geiger's loop on random data.
# ======================================================================================

class MatchFinder:
    """Index of 3-, 2- and 1-byte prefixes over the plaintext, built incrementally."""

    def __init__(self, src: bytes):
        self.src = src
        self.n = len(src)
        self.h3: "dict[int, list[int]]" = {}
        self.h2: "dict[int, list[int]]" = {}
        self.h1: "dict[int, list[int]]" = {}
        self.built = 0   # every position < self.built has been indexed

    def _index_to(self, upto: int) -> None:
        src, n = self.src, self.n
        for j in range(self.built, upto):
            b0 = src[j]
            self.h1.setdefault(b0, []).append(j)
            if j + 1 < n:
                self.h2.setdefault(b0 | (src[j + 1] << 8), []).append(j)
                if j + 2 < n:
                    self.h3.setdefault(b0 | (src[j + 1] << 8) | (src[j + 2] << 16),
                                       []).append(j)
        self.built = max(self.built, upto)

    def _k(self, j: int, pos: int, maxcopy: int, limit: int) -> int:
        """Length of the common run src[j..] vs src[pos..].

        DEVIATION-1: `limit` clamps to the bytes remaining (Geiger's loop has no such
        bound and reads past the end of the source array). j + k is deliberately NOT
        clamped -- it is allowed to run into [pos, ...), which is precisely how a
        self-overlapping (offset < length) match arises.
        """
        src = self.src
        cap = maxcopy if maxcopy < limit else limit
        k = 0
        while k < cap and src[j + k] == src[pos + k]:
            k += 1
        return k

    def find(self, pos: int, rng: int, maxcopy: int, cur: int, tiebreak: str):
        """Return (length, abs_offset_j) or None if nothing was accepted.

        `cur` is the incoming nCopyLength (stale under quirk Q3). None means the
        search accepted nothing, so the caller must keep its stale state.
        """
        src, n = self.src, self.n
        lo = pos - rng if pos > rng else 0
        limit = n - pos
        if limit <= 0:
            return None
        self._index_to(pos)

        strict = (tiebreak == "farthest")

        # --- fast path: is there any 3-byte match in the window? ------------------
        cands = None
        if limit >= 3:
            key3 = src[pos] | (src[pos + 1] << 8) | (src[pos + 2] << 16)
            cands = self.h3.get(key3)

        if cands:
            # ascending j, exactly Geiger's order; only j >= lo are in range.
            i = _lower_bound(cands, lo)
            best_k, best_j = -1, -1
            found3 = False
            for idx in range(i, len(cands)):
                j = cands[idx]
                if j >= pos:
                    break
                k = self._k(j, pos, maxcopy, limit)
                found3 = True
                if k > best_k or (k == best_k and not strict):
                    best_k, best_j = k, j
                if k == maxcopy:
                    best_k, best_j = k, j   # early break takes THIS j
                    break
            if found3 and best_k >= 3:
                # accept test against the stale value
                if (best_k > cur) if strict else (best_k >= cur):
                    return best_k, best_j
                return None

        # --- nothing reaches 3: exact small scan over k in {0,1,2} ---------------
        # Only reachable when cur <= 2 can still accept something, but we must also
        # return None correctly when cur >= 3, so compute M honestly.
        best_k, best_j = -1, -1
        for klen, table in ((2, self.h2), (1, self.h1)):
            if klen > limit:
                continue
            if klen == 2:
                key = src[pos] | (src[pos + 1] << 8)
            else:
                key = src[pos]
            lst = table.get(key)
            if not lst:
                continue
            i = _lower_bound(lst, lo)
            for idx in range(i, len(lst)):
                j = lst[idx]
                if j >= pos:
                    break
                k = self._k(j, pos, maxcopy, limit)
                if k > best_k or (k == best_k and not strict):
                    best_k, best_j = k, j
            if best_k >= klen:
                break
        if best_k < 0:
            # No j at all in the window (pos == 0 only, in practice). Geiger's loop
            # body then never runs and nothing is accepted.
            if lo >= pos:
                return None
            # There IS a window but no byte matched: k == 0 for every j, and the last
            # (nearest) j is pos-1 under `nearest`, the first is lo under `farthest`.
            best_k, best_j = 0, (pos - 1 if not strict else lo)
        if (best_k > cur) if strict else (best_k >= cur):
            return best_k, best_j
        return None


    # ---------------------------------------------------------------------------
    # RICH candidate enumeration (added for the per-blob solver).
    #
    # `find()` above returns ONE j, chosen by the nearest/farthest extremal rule,
    # and is the load-bearing Geiger-equivalent path (selftest proves it). The
    # solver needs the FULL tie set instead, so that policies which are not a
    # directional scan (repeat-offset preference, middle, ...) can be expressed.
    #
    # DELIBERATE DEVIATION from Geiger, stated plainly: this path does NOT take
    # his `k == nMaxCopy` early break, because that break truncates the candidate
    # set. For `nearest`/`farthest` the early break provably cannot change the
    # answer -- a j attaining k == maxcopy attains the maximum M, and the break
    # takes the FIRST such j (== farthest); `_pick` reproduces that by taking
    # min(js) for farthest. For `nearest` the break CAN differ from a full scan,
    # so `nearest`/`farthest` in the encoder keep using find(), not this. This
    # path is used only by the new policies, which have no Geiger analogue.
    # ---------------------------------------------------------------------------
    def find_ties(self, pos: int, rng: int, maxcopy: int):
        """Return (M, sorted list of every j in-window attaining length M), or None.

        M is the true maximum common-run length over the whole window. Only
        M >= 3 is interesting to the caller; shorter results are returned as-is
        so the caller can decide.
        """
        src = self.src
        lo = pos - rng if pos > rng else 0
        limit = self.n - pos
        if limit <= 0 or lo >= pos:
            return None
        self._index_to(pos)
        if limit < 3:
            return None
        key3 = src[pos] | (src[pos + 1] << 8) | (src[pos + 2] << 16)
        cands = self.h3.get(key3)
        if not cands:
            return None
        best_k = -1
        js = []
        for idx in range(_lower_bound(cands, lo), len(cands)):
            j = cands[idx]
            if j >= pos:
                break
            k = self._k(j, pos, maxcopy, limit)
            if k > best_k:
                best_k, js = k, [j]
            elif k == best_k:
                js.append(j)
        if best_k < 3:
            return None
        return best_k, js


def _pick(js, policy, prev_j, pos):
    """Choose one j from the tie set under a non-directional policy.

    js is ascending, so js[0] is the FARTHEST offset and js[-1] the NEAREST.
    """
    if policy == "farthest":
        return js[0]
    if policy == "nearest":
        return js[-1]
    if policy == "middle":
        return js[len(js) // 2]
    if policy == "repeat":
        # prefer reusing the immediately-previous match's OFFSET (distance),
        # falling back to farthest. This is the disambiguation of the 48.7%
        # lead in compression-recompression.md 5.2.
        if prev_j is not None:
            want = pos - prev_j
            for j in js:
                if pos - j == want:
                    return j
        return js[0]
    if policy == "nearest-full":
        # NOT the same as `nearest`. Geiger's scan breaks out early on
        # k == nMaxCopy and takes THAT j (the farthest attaining maxcopy), so his
        # `nearest` is nearest-except-at-maxcopy. This policy is the true nearest
        # over the full tie set. [OBSERVED] it solves 0x062000, which `nearest`
        # misses at offset 24 -- the early break was masking a real axis.
        return js[-1]
    if policy == "repeat-near":
        if prev_j is not None:
            want = pos - prev_j
            for j in js:
                if pos - j == want:
                    return j
        return js[-1]
    raise EncodeError(f"unknown tiebreak policy {policy}")


RICH_POLICIES = ("middle", "repeat", "repeat-near", "nearest-full")


# ======================================================================================
# OKUMURA TREE MATCH FINDER
#
# Hypothesis under test: Square did not use a tie-break POLICY at all. They used
# Haruhiko Okumura's LZSS.C (4/6/1989) -- the canonical binary-search-tree LZSS that
# was ubiquitous in Japanese development -- or a close derivative. In LZSS.C the
# returned match position is an ARTIFACT of the tree's insertion/deletion history,
# not a farthest/nearest choice: InsertNode() walks down one root-to-leaf path and
# keeps the FIRST node on that path attaining each new maximum length (the test is
# `if (i > match_length)`, strictly greater -- equal-length candidates encountered
# LATER on the descent are ignored, and candidates not on the descent path are never
# examined at all).
#
# SOURCE, verbatim, cross-checked against two independent copies:
#   https://raw.githubusercontent.com/e-n-f/lzss/master/LZSS.C
#   https://raw.githubusercontent.com/luikore/lzss/master/src/lzss.c
# Both carry the header "4/6/1989 Haruhiko Okumura / Use, distribute, and modify this
# program freely." and are byte-compatible in InitTree/InsertNode/DeleteNode/Encode.
#
# NOTE ON A NEAR MISS: LZHUF.C (Yoshizaki 1988, Okumura's comments 4/7/1989,
#   https://raw.githubusercontent.com/msmiley/lzh/master/src/lzh.c :79-83) adds an
#   `if (i == match_length)` block that explicitly prefers the NEARER of two equal
#   -length matches. Plain LZSS.C has no such block. They are different algorithms at
#   exactly the decision we care about; this class implements PLAIN LZSS.C, and
#   `--tiebreak okumura-lzhuf` implements the LZHUF variant for comparison.
#
# ---------------------------------------------------------------------------------
# ADAPTATIONS TO CT'S FORMAT. Each is flagged FORCED (the format leaves no choice) or
# CHOICE (a judgement call that could reasonably have gone otherwise).
#
#  A1 FORCED  Ring buffer -> plaintext array. CT offsets are OUTPUT-RELATIVE backward
#             distances (compression-format.md 2), not indices into a mod-N ring. So
#             node ids here are ABSOLUTE plaintext positions and the emitted distance
#             is `pos - p`. Okumura's `((r-p) & (N-1)) - 1` encoding is dropped; it is
#             a ring artifact with no counterpart in CT.
#  A2 FORCED  N and F are set per mode from the format's own limits:
#               12-bit mode: N = 4096, F = 18   <- EXACTLY Okumura's stock parameters
#               11-bit mode: N = 2048, F = 34
#             THRESHOLD = 2 is unchanged from LZSS.C and already matches CT exactly
#             (CT's minimum encodable match is 3). No adaptation needed there.
#  A3 FORCED  No space-filled / zero-filled pre-history. Okumura seeds the ring with
#             N-F filler bytes and calls InsertNode(r-i) for i=1..F over them. A CT
#             back-reference cannot point before the start of the output, so those
#             nodes are inexpressible. This DISPOSES OF the "spaces vs zeros buffer
#             init" variant: in CT it is not a free parameter, it is unrepresentable.
#  A4 CHOICE  Comparison clamped at the end of the plaintext (r+i >= n stops the
#             compare with cmp = 0, i.e. descend right). Okumura reads stale ring
#             bytes there and lets Encode() clamp afterwards with
#             `if (match_length > len) match_length = len`. Our clamp is equivalent
#             for match_length but MAY differ in the `cmp` it feeds the descent within
#             the last F bytes of a blob. Same class as the existing DEVIATION-1.
#             (p+i can never exceed n: p < r, so p+i < r+i.)
#  A5 FORCED  Live-window size is Okumura's, not the format's. His Encode() keeps
#             exactly N-F nodes live (r trails s by N-F), so the largest distance he
#             can ever emit is N-F, not N-1. That is 4078 (12-bit) and 2014 (11-bit).
#             [OBSERVED] Square's corpus max offset is EXACTLY 4078 and EXACTLY 2014,
#             with offsets 4079..4095 and 2015..2047 never appearing in 307,715
#             back-references, though the format encodes them fine. Reproducing this
#             is not a choice -- it falls out of transcribing Encode() faithfully.
# ======================================================================================

class OkumuraTree:
    """Okumura LZSS.C InsertNode/DeleteNode, over an absolute plaintext array.

    Node ids are plaintext positions 0..n-1. Following the original, the 256 tree
    roots are pseudo-nodes n+1+c (his N+1+key[0]) and NIL is a real, writable array
    slot (his NIL == N) so that the original's harmless `dad[NIL] = ...` stores have
    somewhere to land rather than needing a guard the original does not have.

    After `advance(pos)`, `match_length` / `match_position` hold exactly what
    InsertNode(pos) set -- the same globals, with the same meaning.

    IMPORTANT PROPERTY: Encode() inserts EVERY position in order regardless of what
    the parse decided, so the tree's state at a given position does not depend on
    match/literal choices. That is what makes the per-tie measurement below valid:
    we can drive this tree along Square's own parse and still see the tree the
    original encoder would have had.
    """

    def __init__(self, src: bytes, N: int, F: int, lzhuf: bool = False,
                 tail: str = "ring", fill: int = 0x00):
        self.src = src
        self.n = n = len(src)
        self.N = N
        self.F = F
        self.lzhuf = lzhuf
        # `tail` governs adaptation A4 -- what the comparison reads past the end of
        # the plaintext.  "clamp" stops the compare (our first, simpler cut).
        # "ring" models Okumura's actual ring: index (x mod N) still holds the byte
        # written N positions ago, so a compare running off the end reads src[x-N],
        # or the initial filler if that is also before the start.  [OBSERVED] every
        # single one of our remaining sub-maxcopy disagreements under "clamp" lay in
        # the last F bytes of a blob, which is what motivated modelling this.
        #
        # THE WRITTEN-YET TEST IS `x >= N + F`, NOT `x - N >= 0`.  [OBSERVED]
        # The ring slot holding coordinate x is (re)written at TIME x - F, by the
        # advance loop's `text_buf[s] = c`.  Past the end of the plaintext that
        # write never happens, so the slot still holds what the PREVIOUS lap put
        # there -- and that previous write happened at time x - N - F, which only
        # exists if x >= N + F.  Below that threshold the slot has never been
        # written at all and still holds the zero-initialised buffer.
        #
        # Okumura's own buffer is NOT zero there: his Encode() pre-loads the first
        # F bytes at ring indices [N-F, N-1] before the loop starts.  Square has no
        # such pre-load -- that is adaptation A3 (no pre-history) applied to the
        # BUFFER as well as to the node set, which is forced by CT's format.
        #
        # This is exactly the `n == N` case: the blob fills the ring precisely once,
        # so the last F positions read slots that Okumura pre-loaded but Square left
        # zero.  Getting this wrong cost 53 blobs and nothing else; fixing it takes
        # the corpus from 1246/1299 to 1299/1299.  See compression-recompression.md 11.
        self.tail = tail
        self.fill = fill
        self.NIL = n + 257                     # a real slot, as in the original
        size = n + 258
        self.lson = [self.NIL] * size
        self.rson = [self.NIL] * size
        self.dad = [self.NIL] * size
        self.match_position = 0
        self.match_length = 0
        self.r = 0                             # next position to insert
        self.window = N - F                    # A5: Okumura's live-node span

    # -- InsertNode(r), LZSS.C:  inserts AND returns the match via the globals ------
    def insert(self, r: int) -> None:
        src, n, F, NIL = self.src, self.n, self.F, self.NIL
        lson, rson, dad = self.lson, self.rson, self.dad
        cmp_ = 1
        p = n + 1 + src[r]                     # his `p = N + 1 + key[0]`
        rson[r] = lson[r] = NIL
        self.match_length = 0
        while True:
            if cmp_ >= 0:
                if rson[p] != NIL:
                    p = rson[p]
                else:
                    rson[p] = r
                    dad[r] = p
                    return
            else:
                if lson[p] != NIL:
                    p = lson[p]
                else:
                    lson[p] = r
                    dad[r] = p
                    return
            # `for (i = 1; i < F; i++) if ((cmp = key[i] - text_buf[p+i]) != 0) break;`
            i = 1
            cmp_ = 0
            ring = (self.tail == "ring")
            N = self.N
            fill = self.fill
            while i < F:
                x = r + i
                if x >= n:                     # A4: past the end of the plaintext
                    if not ring:
                        cmp_ = 0
                        break
                    a = src[x - N] if x >= N + F else fill
                else:
                    a = src[x]
                y = p + i
                b = src[y] if y < n else (src[y - N] if y >= N + F else fill)
                cmp_ = a - b
                if cmp_ != 0:
                    break
                i += 1
            if i > self.match_length:
                self.match_position = p
                self.match_length = i
                if i >= F:
                    break
            elif self.lzhuf and i == self.match_length:
                # LZHUF.C:79-83 only -- prefer the NEARER equal-length match.
                if (r - p) < (r - self.match_position):
                    self.match_position = p
        # node replacement: r takes p's place in the tree
        dad[r] = dad[p]
        lson[r] = lson[p]
        rson[r] = rson[p]
        dad[lson[p]] = r
        dad[rson[p]] = r
        if rson[dad[p]] == p:
            rson[dad[p]] = r
        else:
            lson[dad[p]] = r
        dad[p] = NIL

    # -- DeleteNode(p), LZSS.C -----------------------------------------------------
    def delete(self, p: int) -> None:
        NIL = self.NIL
        lson, rson, dad = self.lson, self.rson, self.dad
        if dad[p] == NIL:
            return
        if rson[p] == NIL:
            q = lson[p]
        elif lson[p] == NIL:
            q = rson[p]
        else:
            q = lson[p]
            if rson[q] != NIL:
                while rson[q] != NIL:
                    q = rson[q]
                rson[dad[q]] = lson[q]
                dad[lson[q]] = dad[q]
                lson[q] = lson[p]
                dad[lson[p]] = q
            rson[q] = rson[p]
            dad[rson[p]] = q
        dad[q] = dad[p]
        if rson[dad[p]] == p:
            rson[dad[p]] = q
        else:
            lson[dad[p]] = q
        dad[p] = NIL

    # -- Encode()'s node bookkeeping, LZSS.C ---------------------------------------
    def advance(self, pos: int) -> None:
        """Insert every position up to and including `pos`, retiring stale nodes.

        Mirrors Encode()'s inner loop `DeleteNode(s); s++; r++; InsertNode(r);`.
        Solving that recurrence: when InsertNode(R) runs, the node just retired is
        R - window - 1, leaving exactly [R-window, R] live -- hence a maximum
        emittable distance of `window` == N - F (adaptation A5).
        """
        w = self.window
        while self.r <= pos:
            stale = self.r - w - 1
            if stale >= 0:
                self.delete(stale)
            self.insert(self.r)
            self.r += 1

    def match_at(self, pos: int):
        """(length, absolute j) for `pos`, clamped to CT's limits, or None."""
        if pos >= self.n:
            return None
        self.advance(pos)
        L = self.match_length
        rem = self.n - pos
        if L > rem:                    # Encode(): `if (match_length > len) ...`
            L = rem
        if L <= 2:                     # THRESHOLD
            return None
        return L, self.match_position


OKUMURA_POLICIES = ("okumura", "okumura-lzhuf")


def _lower_bound(lst, v):
    lo, hi = 0, len(lst)
    while lo < hi:
        mid = (lo + hi) // 2
        if lst[mid] < v:
            lo = mid + 1
        else:
            hi = mid
    return lo


# ======================================================================================
# Encoder
# ======================================================================================

class EncodeError(Exception):
    pass


def _encode_pass(src: bytes, i: int, st: Strategy, size_guard: int):
    """One of Geiger's two passes. `i` is his loop variable.

    i = 0 -> range $07FF, maxcopy 34, shift 3, marker bits $40   (11-bit mode)
    i = 1 -> range $0FFF, maxcopy 18, shift 4, marker bits $00   (12-bit mode)
    [CTRecompression.txt:28-29, :82/:90]

    Returns (blob_bytes, body_size) or None if the pass was abandoned by the
    `nWorkPos < nCompSize` guard (:39).
    """
    n = len(src)
    if n == 0:
        raise EncodeError("empty plaintext")

    rng = 0x07FF | (i << 11)              # :28
    maxcopy = 18 + ((1 - i) << 4)         # :29
    shift = 3 + i                         # :63
    off_hi_mask = 0x07 | (i << 3)         # :63
    marker_mode = 0x40 if i == 0 else 0x00  # :82 -- (byte)(0xC0*(i-1)) == $40

    # :30-37. The first byte is unconditionally a literal and position 0 is never
    # searched. buf[0..1] = size (filled in at the end), buf[2] = first packet
    # header, buf[3] = src[0].
    buf = bytearray(b"\x00\x00\x00")
    buf.append(src[0])
    src_pos = 1
    bit_ctr = 1
    pack_hdr_off = 2
    copy_len = 0        # nCopyLength -- leaks across positions under quirk Q3
    offset_j = 0        # nOffset (ABSOLUTE j) -- leaks too

    mf = MatchFinder(src)

    rich = st.tiebreak in RICH_POLICIES
    oku = st.tiebreak in OKUMURA_POLICIES
    prev_j = None   # offset_j of the previous emitted MATCH (repeat-offset policies)

    # Okumura's tree is stateful and must see every position in order; it is built
    # once per pass. N/F come from the mode (adaptation A2). rng/maxcopy above are
    # the FORMAT's limits; the tree's own window (N-F) is tighter and is what
    # actually bounds the emitted distance (A5).
    otree = OkumuraTree(src, rng + 1, maxcopy,
                        lzhuf=(st.tiebreak == "okumura-lzhuf")) if oku else None

    def search(pos, cur):
        if oku:
            # No stale-state accept test: LZSS.C has no nCopyLength analogue, so the
            # `leak` axis is not applicable to this finder and is ignored here.
            return otree.match_at(pos)
        if rich:
            # Rich policies are not a directional scan; they need the tie set.
            # The stale-`cur` accept test (quirk Q3) is applied afterwards so the
            # leak axis still means the same thing.
            r = mf.find_ties(pos, rng, maxcopy)
            if r is None:
                return None
            m, js = r
            if m < cur:
                return None
            return m, _pick(js, st.tiebreak, prev_j, pos)
        return mf.find(pos, rng, maxcopy, cur, st.tiebreak)

    while src_pos < n and len(buf) < size_guard:
        while bit_ctr < 8 and src_pos < n:
            cur = copy_len if st.leak else 0
            r = search(src_pos, cur)
            if r is not None:
                copy_len, offset_j = r
            elif oku:
                copy_len = 0    # LZSS.C has no state leak; nothing to preserve
            # else: quirk Q3 -- keep the stale copy_len and offset_j untouched.

            take_match = copy_len > 2                      # :59

            # --- lazy parsing -------------------------------------------------
            # Not Geiger. If a strictly longer match starts within the next
            # `lazy` positions, emit a literal now instead.
            if take_match and st.lazy:
                for d in range(1, st.lazy + 1):
                    if src_pos + d >= n:
                        break
                    r2 = mf.find(src_pos + d, rng, maxcopy, 0, st.tiebreak)
                    if r2 is not None and r2[0] > copy_len + d - 1 and r2[0] > 2:
                        take_match = False
                        break

            if take_match:
                buf[pack_hdr_off] |= 1 << bit_ctr          # :60
                dist = src_pos - offset_j                  # :61
                if dist <= 0 or dist > rng:
                    raise EncodeError(
                        f"encoder produced out-of-range offset {dist} at src {src_pos} "
                        f"(quirk Q3 stale-offset leak); range={rng}")
                buf.append(dist & 0xFF)                                        # :62
                buf.append(((copy_len - 3) << shift) | ((dist >> 8) & off_hi_mask))
                src_pos += copy_len                        # :64
                prev_j = offset_j
                copy_len = 0                               # :65 -- reset ONLY here
            else:
                buf.append(src[src_pos])                   # :68
                src_pos += 1
            bit_ctr += 1

        if bit_ctr == 8:                                   # :71-75
            bit_ctr = 0
            pack_hdr_off = len(buf)
            buf.append(0)

    if len(buf) >= size_guard:
        return None                                        # :78 guard failed

    work_pos = len(buf)
    if bit_ctr > 0:
        # --- partial trailing packet becomes the ADDENDUM ---------------------
        buf[pack_hdr_off] |= (0xFF << bit_ctr) & 0xFF      # :80  padding rule
        tail = bytes(buf[pack_hdr_off:work_pos])           # :81  shift right by 3
        del buf[pack_hdr_off:work_pos]
        arr_length = work_pos + 3                          # :83
        buf.append(bit_ctr | marker_mode)                  # :82  marker
        buf.append(arr_length & 0xFF)                      # :84  cumulative length
        buf.append((arr_length >> 8) & 0xFF)               # :85
        buf.extend(tail)
        # QUIRK Q8 [OBSERVED, new]. Geiger writes a bare $00 here (:86). Square writes
        # the MODE bits: the terminator is `marker_mode`, exactly as in the
        # no-addendum branch below. Both satisfy the ASM, which only requires
        # `AND #$3F == 0` ($C3/0644-$C3/0649), so $00 decodes fine -- it is simply not
        # the byte Square emitted. Corpus-wide check, 1302/1302 with no exceptions:
        #   11-bit + addendum -> $40 x710      11-bit, none -> $40 x95
        #   12-bit + addendum -> $00 x436      12-bit, none -> $00 x61
        # This was masked until now because it only bites 11-bit blobs WITH an
        # addendum, i.e. almost exactly the EVENT/MAP/TASM classes -- which is why
        # 12-bit GFX scored 90.9% while 11-bit EVENT scored 13.2% on the same parse.
        buf.append(marker_mode)                            # :86  terminator, $3F clear
        total = arr_length + 1                             # :98
    else:
        # Body divided evenly; pack_hdr_off is an unused header slot == end of body.
        #
        # QUIRK Q7 (found by this port, not previously documented). :89 sets
        # nArrLength = nPackHdrOff + 1, but the last byte actually written is the
        # terminator at index nPackHdrOff (:90). The final Array.Copy at :97 copies
        # nArrLength + 1 == nPackHdrOff + 2 bytes -- one byte PAST the terminator.
        # So Geiger's no-addendum output carries a spurious trailing $00. Compare
        # the addendum branch, where nArrLength = nWorkPos + 3 IS the index of the
        # terminator (:83/:86) and the same +1 is correct. Ported as an axis
        # (--tailpad) because it is pure output length: it cannot change any earlier
        # byte, only make us 1 longer than Square.
        arr_length = pack_hdr_off + 1                      # :89
        del buf[pack_hdr_off:]
        buf.append(marker_mode)                            # :90  terminator
        if st.tailpad:
            buf.append(0)                                  # :97 over-copy
            total = arr_length + 1
        else:
            total = arr_length

    body_size = pack_hdr_off - 2                           # :93
    if body_size > 0xFFFF:
        raise EncodeError(f"body size {body_size} exceeds the 16-bit header field")
    buf[0] = body_size & 0xFF                              # :94
    buf[1] = (body_size >> 8) & 0xFF                       # :95
    if len(buf) != total:
        raise EncodeError(f"internal: built {len(buf)} bytes, expected {total}")
    return bytes(buf), body_size


def encode(src: bytes, st: Strategy = GEIGER, orig_mode: int = 0) -> bytes:
    """Full encode. Mirrors CTRecompression.txt's two-pass commit logic (:27, :39, :78).

    mode="orig" is a DIAGNOSTIC, not a strategy: it forces the mode Square actually
    used for this blob, which isolates parse quality from mode-selection policy. It
    is not a reproducible encoder (it needs the answer as input).
    """
    best = None
    comp_size = 0xFFFF                       # :14 -- doubles as the loop guard
    if st.mode == "orig":
        passes = (0,) if orig_mode == 11 else (1,)
    else:
        passes = {"both": (0, 1), "11": (0,), "12": (1,)}[st.mode]
    for i in passes:
        r = _encode_pass(src, i, st, comp_size)
        if r is None:
            continue
        blob, body_size = r
        # :78 `if(nWorkPos < nCompSize)` already gated inside the pass; committing
        # here updates nCompSize the same way :93 does.
        if st.mode_tie == "12" and best is not None and body_size == comp_size:
            best = blob
        comp_size = body_size
        best = blob
    if best is None:
        raise EncodeError("no pass produced output")
    return best


# ======================================================================================
# Identity harness
# ======================================================================================

def _hexwin(b: bytes, at: int, w: int = 12) -> str:
    lo = max(0, at - 4)
    hi = min(len(b), at + w)
    return " ".join(f"{x:02X}" for x in b[lo:hi])


def compare_one(rom: bytes, off: int, st: Strategy) -> dict:
    """Encode one corpus blob and classify the result. Never raises."""
    plain, info = ctlz.decode(rom, off)
    orig = rom[off:off + info["consumed"]]
    plain = bytes(plain)

    res = {"offset": f"0x{off:06X}", "orig_len": len(orig),
           "mode_orig": info["mode"], "plain_len": len(plain)}

    # A blob that decodes to ZERO bytes. Three exist in the corpus (0x1B7FF2,
    # 0x3E4B7A, 0x3EFCBE). Geiger's encoder cannot express one at all -- :37 does
    # CompData[3] = SrcBuffer[0] unconditionally, so an empty source is out of its
    # domain. This is a property of the input, not a defect in our encoder, so it
    # gets its own verdict rather than being laundered into BROKEN.
    if len(plain) == 0:
        res.update(verdict="DEGENERATE", reason="blob decodes to zero bytes")
        return res

    try:
        mine = encode(plain, st, orig_mode=info["mode"])
    except (EncodeError, ctlz.CtlzError) as e:
        res.update(verdict="BROKEN", reason=f"encode: {e}")
        return res
    res["mine_len"] = len(mine)
    res["delta"] = len(mine) - len(orig)

    if mine == orig:
        res["verdict"] = "IDENTICAL"
        res["first_div"] = -1
        return res

    # first divergence
    fd = 0
    while fd < min(len(mine), len(orig)) and mine[fd] == orig[fd]:
        fd += 1
    res["first_div"] = fd
    res["ctx_ours"] = _hexwin(mine, fd)
    res["ctx_theirs"] = _hexwin(orig, fd)

    # Does it at least decode back correctly? DIVERGENT is signal; BROKEN is our bug.
    try:
        back, _ = ctlz.decode(mine, 0)
    except ctlz.CtlzError as e:
        res.update(verdict="BROKEN", reason=f"re-decode: {e.msg}")
        return res
    if bytes(back) != plain:
        res.update(verdict="BROKEN", reason="re-decode produced different plaintext")
        return res
    res["verdict"] = "DIVERGENT"
    return res


def run_harness(rom, offsets, st, progress=False):
    rows = []
    t0 = time.time()
    for idx, off in enumerate(offsets):
        rows.append(compare_one(rom, off, st))
        if progress and (idx + 1) % 25 == 0:
            el = time.time() - t0
            print(f"  ... {idx+1}/{len(offsets)}  {el:.0f}s", file=sys.stderr)
    return rows, time.time() - t0


def summarize(rows, st, elapsed):
    n = len(rows)
    ident = sum(1 for r in rows if r["verdict"] == "IDENTICAL")
    div = sum(1 for r in rows if r["verdict"] == "DIVERGENT")
    brk = sum(1 for r in rows if r["verdict"] == "BROKEN")
    deg = sum(1 for r in rows if r["verdict"] == "DEGENERATE")
    deltas = [r["delta"] for r in rows if "delta" in r]
    fds = [r["first_div"] for r in rows if r.get("first_div", -1) >= 0]
    s = {
        "strategy": st.name, "blobs": n,
        "identical": ident, "divergent": div, "broken": brk, "degenerate": deg,
        # rate is over ENCODABLE blobs; zero-length blobs are outside the encoder's
        # domain and are excluded rather than counted as failures.
        "identity_rate": (ident / (n - deg) if (n - deg) else 0.0),
        "seconds": round(elapsed, 1),
    }
    if deltas:
        deltas.sort()
        s["delta_total"] = sum(deltas)
        s["delta_min"] = deltas[0]
        s["delta_median"] = deltas[len(deltas) // 2]
        s["delta_max"] = deltas[-1]
        s["blobs_smaller"] = sum(1 for d in deltas if d < 0)
        s["blobs_larger"] = sum(1 for d in deltas if d > 0)
    if fds:
        buckets = {"0-3": 0, "4-15": 0, "16-63": 0, "64-255": 0,
                   "256-1023": 0, "1024+": 0}
        for f in fds:
            k = ("0-3" if f <= 3 else "4-15" if f <= 15 else "16-63" if f <= 63
                 else "64-255" if f <= 255 else "256-1023" if f <= 1023 else "1024+")
            buckets[k] += 1
        s["first_div_hist"] = buckets
        s["first_div_median"] = sorted(fds)[len(fds) // 2]
    return s


# ======================================================================================
# Commands
# ======================================================================================

def _offsets(a, man):
    offs = [int(e["offset"], 16) for e in man["entries"]]
    if getattr(a, "sample", 0):
        # deterministic evenly-spaced subset -- NOT random, so runs are comparable
        step = max(1, len(offs) // a.sample)
        offs = offs[::step][:a.sample]
    return offs


def _strategy_from_args(a) -> Strategy:
    return Strategy(tiebreak=a.tiebreak, leak=(a.leak == "on"), lazy=a.lazy,
                    mode=a.mode, mode_tie=a.mode_tie, tailpad=(a.tailpad == "on"))


def identity_strategy() -> Strategy:
    """The one configuration that reproduces the original encoder byte-for-byte.

    Okumura binary-search-tree tie-break, the encoder's copy-length/offset state leak
    left on, greedy (no lazy lookahead) and no trailing pad byte: over the whole
    corpus of shipped blobs this re-encodes every one of them to the exact original
    bytes. The argparse defaults of the research subcommands are deliberately NOT
    this configuration, so anything that must produce insertable bytes pins it here
    rather than passing flags.

    Offset width (11- or 12-bit) is not part of it: that is per-blob metadata carried
    alongside the asset, supplied as `orig_mode`.
    """
    return Strategy(tiebreak="okumura", leak=True, lazy=0, mode="orig",
                    tailpad=False)


def cmd_compress(a) -> int:
    """File -> file: plaintext in, one compressed blob out, at the pinned strategy."""
    try:
        with open(a.inp, "rb") as f:
            src = f.read()
    except FileNotFoundError:
        die(f"input not found: {a.inp}")
    try:
        blob = encode(src, identity_strategy(), orig_mode=a.mode)
    except EncodeError as e:
        die(f"encode failed on {a.inp}: {e}")
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    with open(a.out, "wb") as f:
        f.write(blob)
    return 0


def cmd_pack(a) -> int:
    rom = ctlz.read_rom(a.rom)
    plain, _ = ctlz.decode(rom, a.offset)
    blob = encode(bytes(plain), _strategy_from_args(a))
    if a.out:
        with open(a.out, "wb") as f:
            f.write(blob)
    print(f"0x{a.offset:06X}  plain={len(plain)}  packed={len(blob)}")
    return 0


def cmd_identity(a) -> int:
    rom = ctlz.read_rom(a.rom)
    man = ctlz.load_manifest(a.manifest)
    st = _strategy_from_args(a)
    offs = _offsets(a, man)
    rows, el = run_harness(rom, offs, st, progress=a.progress)
    s = summarize(rows, st, el)
    print(json.dumps(s, indent=2, sort_keys=True))
    if a.report:
        with open(a.report, "w", encoding="utf-8") as f:
            json.dump({"summary": s, "rows": rows}, f, indent=2)
        print(f"ctlzpack: per-blob report -> {a.report}", file=sys.stderr)
    if a.show:
        for r in rows:
            if r["verdict"] != "IDENTICAL":
                print(f"  {r['offset']} {r['verdict']:9} fd={r.get('first_div')} "
                      f"delta={r.get('delta')}")
                if "ctx_ours" in r:
                    print(f"      ours   {r['ctx_ours']}")
                    print(f"      square {r['ctx_theirs']}")
    return 0


def cmd_sweep(a) -> int:
    rom = ctlz.read_rom(a.rom)
    man = ctlz.load_manifest(a.manifest)
    offs = _offsets(a, man)
    grid = []
    for tb in a.tiebreaks.split(","):
        for lk in a.leaks.split(","):
            for lz in [int(x) for x in a.lazies.split(",")]:
                for md in a.modes.split(","):
                    for mt in a.mode_ties.split(","):
                        for tp in a.tailpads.split(","):
                            grid.append(Strategy(tb, lk == "on", lz, md, mt,
                                                 tp == "on"))
    out = []
    for st in grid:
        rows, el = run_harness(rom, offs, st)
        s = summarize(rows, st, el)
        out.append(s)
        print(f"{s['identity_rate']*100:6.2f}%  id={s['identical']:5} "
              f"div={s['divergent']:5} brk={s['broken']:5}  "
              f"dtot={s.get('delta_total','?'):>9}  {st.name}", flush=True)
    out.sort(key=lambda s: (-s["identity_rate"], s.get("delta_total", 1 << 60)))
    if a.report:
        with open(a.report, "w", encoding="utf-8") as f:
            json.dump(out, f, indent=2)
    print("\nRANKED:")
    for s in out:
        print(f"  {s['identity_rate']*100:6.2f}%  {s['strategy']}")
    return 0


# ======================================================================================
# Per-blob solver
#
# Reframes the question from "which SINGLE strategy wins overall" to "for each blob,
# does ANY strategy reproduce Square's bytes exactly". Emits a per-blob table with the
# SET of winning strategies (possibly empty), so the results can be clustered.
#
# Pruned axes, and why (do not re-add without new evidence):
#   * greedy vs lazy       -- settled greedy (99.939% longest-match, corpus 0b).
#   * leak on/off          -- proven ZERO effect on output under greedy (sweep 4.1).
#   * tailpad on/off       -- pure trailing $00; `on` scores 0.00% by construction.
#                             Handled analytically, not swept.
#   * first-found / last-found -- these are NOT new axes. Geiger's window scan is
#     ascending from (pos-range), so the FIRST candidate found is the FARTHEST offset
#     and the LAST is the NEAREST. They are aliases of farthest/nearest respectively.
# ======================================================================================

SOLVER_TIEBREAKS = ("farthest", "nearest", "nearest-full", "middle",
                    "repeat", "repeat-near")


def _solver_strategies(both_modes: bool):
    out = []
    modes = ("orig", "flip") if both_modes else ("orig",)
    for md in modes:
        for tb in SOLVER_TIEBREAKS:
            out.append((f"{tb}/{md}", tb, md))
    return out


def _encode_for(plain, tb, md, orig_mode):
    st = Strategy(tiebreak=tb, leak=True, lazy=0,
                  mode="orig" if md == "orig" else ("11" if orig_mode == 12 else "12"),
                  tailpad=False)
    return encode(plain, st, orig_mode=orig_mode)


def cmd_solve(a) -> int:
    rom = ctlz.read_rom(a.rom)
    man = ctlz.load_manifest(a.manifest)
    entries = man["entries"]
    if a.sample:
        step = max(1, len(entries) // a.sample)
        entries = entries[::step][:a.sample]
    strats = _solver_strategies(not a.no_flip)
    rows = []
    t0 = time.time()
    for idx, e in enumerate(entries):
        off = int(e["offset"], 16)
        plain, info = ctlz.decode(rom, off)
        plain = bytes(plain)
        orig = rom[off:off + info["consumed"]]
        src = e.get("source") or "?"
        atype = src.split(":")[-1] if ":" in src else src
        row = {"offset": e["offset"], "type": atype, "mode": info["mode"],
               "plain_len": len(plain), "orig_len": len(orig),
               "bank": (off >> 16), "winners": [], "broken": [],
               "best_first_div": -1}
        if len(plain) == 0:
            row["verdict"] = "DEGENERATE"
            rows.append(row)
            continue
        best_fd = -1
        for (name, tb, md) in strats:
            try:
                mine = _encode_for(plain, tb, md, info["mode"])
            except (EncodeError, ctlz.CtlzError) as ex:
                row["broken"].append(f"{name}:{ex}")
                continue
            if mine == orig:
                row["winners"].append(name)
                continue
            fd = 0
            while fd < min(len(mine), len(orig)) and mine[fd] == orig[fd]:
                fd += 1
            if fd > best_fd:
                best_fd = fd
        row["best_first_div"] = best_fd
        row["verdict"] = "SOLVED" if row["winners"] else "UNSOLVED"
        # tie density: how many positions in Square's own parse were ambiguous
        if a.ties:
            row["ties"] = _tie_stats(plain, info["mode"])
        rows.append(row)
        if a.progress and (idx + 1) % 50 == 0:
            print(f"  ... {idx+1}/{len(entries)}  {time.time()-t0:.0f}s",
                  file=sys.stderr)
    el = time.time() - t0
    out = {"tool": TOOL_VERSION, "strategies": [s[0] for s in strats],
           "seconds": round(el, 1), "rows": rows}
    with open(a.out, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1)
    enc = [r for r in rows if r["verdict"] != "DEGENERATE"]
    solved = [r for r in enc if r["winners"]]
    print(f"blobs={len(rows)} encodable={len(enc)} degenerate={len(rows)-len(enc)}")
    print(f"UNION RATE: {len(solved)}/{len(enc)} = {100*len(solved)/len(enc):.2f}%")
    for (name, _, _) in strats:
        c = sum(1 for r in enc if name in r["winners"])
        print(f"  {name:22} {c:5}  {100*c/len(enc):6.2f}%")
    print(f"elapsed {el:.0f}s -> {a.out}")
    return 0


def _tie_stats(plain: bytes, mode: int) -> dict:
    """Replay a greedy parse and count ambiguous (multi-candidate) match positions."""
    rng, mc = (0x07FF, 34) if mode == 11 else (0x0FFF, 18)
    mf = MatchFinder(plain)
    pos, n = 1, len(plain)
    matches = amb = 0
    while pos < n:
        r = mf.find_ties(pos, rng, mc)
        if r is not None and r[0] > 2:
            matches += 1
            if len(r[1]) > 1:
                amb += 1
            pos += r[0]
        else:
            pos += 1
    return {"matches": matches, "ambiguous": amb}


def cmd_reach(a) -> int:
    """Step 4: for UNSOLVED blobs, is Square's choice even IN the candidate set?

    Replays Square's OWN parse element by element (ctlz trace) and, at every match,
    asks two questions:
      (a) is Square's length the maximum available at that position?  (greedy check)
      (b) is Square's offset among the offsets attaining that maximum? (tie-set check)
    A NO on (a) means the parse itself differs -- no greedy-longest rule can produce
    it. A NO on (b) with YES on (a) would mean the offset is unreachable, which would
    be a contradiction (Square decoded it, so it must be a real match) and is asserted.
    """
    rom = ctlz.read_rom(a.rom)
    tab = json.load(open(a.table, encoding="utf-8"))
    targets = [r for r in tab["rows"] if r["verdict"] == "UNSOLVED"]
    if a.sample:
        step = max(1, len(targets) // a.sample)
        targets = targets[::step][:a.sample]
    agg = {"blobs": 0, "matches": 0, "non_longest": 0, "off_not_in_tieset": 0,
           "blobs_with_non_longest": 0, "rank_hist": {}}
    firstdiv = {"square_longer": 0, "square_shorter": 0, "same_len_diff_off": 0}
    for r in targets:
        off = int(r["offset"], 16)
        plain, info = ctlz.decode(rom, off, trace=True)
        plain = bytes(plain)
        rng, mc = (0x07FF, 34) if info["mode"] == 11 else (0x0FFF, 18)
        mf = MatchFinder(plain)
        agg["blobs"] += 1
        blob_bad = False
        first_reported = False
        for (p, is_match, o, L) in info["elements"]:
            if not is_match:
                continue
            agg["matches"] += 1
            res = mf.find_ties(p, rng, mc)
            m, js = (res if res else (0, []))
            if L != m:
                agg["non_longest"] += 1
                blob_bad = True
                if not first_reported:
                    first_reported = True
                    firstdiv["square_longer" if L > m else "square_shorter"] += 1
            else:
                sq_j = p - o
                if sq_j not in js:
                    agg["off_not_in_tieset"] += 1
                else:
                    rank = js.index(sq_j)   # 0 == farthest
                    k = str(rank if rank < 8 else "8+")
                    agg["rank_hist"][k] = agg["rank_hist"].get(k, 0) + 1
                    if not first_reported and len(js) > 1:
                        first_reported = True
                        firstdiv["same_len_diff_off"] += 1
        if blob_bad:
            agg["blobs_with_non_longest"] += 1
    agg["first_anomaly_kind"] = firstdiv
    print(json.dumps(agg, indent=2, sort_keys=True))
    if a.out:
        with open(a.out, "w", encoding="utf-8") as f:
            json.dump(agg, f, indent=2)
    return 0


def cmd_okucmp(a) -> int:
    """PER-TIE agreement of the Okumura tree vs Square, broken out by asset class.

    This is the primary metric, and it is size-independent by construction: it is a
    rate over ambiguous POSITIONS, not over blobs, so it is directly comparable to the
    farthest/nearest table in compression-recompression.md 7.4.

    Method: replay Square's OWN parse (ctlz trace). At each match where the tie set has
    more than one member, ask what farthest / nearest / the Okumura tree would each
    have returned, and compare to the offset Square actually emitted. Driving the tree
    along Square's parse is valid because Encode() inserts every position regardless of
    parse decisions (see OkumuraTree's docstring).

    Also reports, separately, the tree's rate over ALL max-length matches including
    unambiguous ones, and how often the tree fails to find the maximum length at all
    (its descent is one root-to-leaf path, so it is NOT guaranteed to find the longest
    match -- that is a real and important failure mode for this hypothesis).
    """
    rom = ctlz.read_rom(a.rom)
    man = ctlz.load_manifest(a.manifest)
    entries = man["entries"]
    if a.sample:
        step = max(1, len(entries) // a.sample)
        entries = entries[::step][:a.sample]

    # STRATIFIED (see compression-recompression.md 8): ambiguous positions split on
    # whether the match length equals maxcopy. Square's behaviour differs between the
    # two strata, so a blended rate is not interpretable. `desc` is the rival
    # hypothesis -- a DESCENDING (near->far) scan with a `k == maxcopy` early break,
    # which reduces exactly to "nearest at maxcopy, farthest below it".
    def blank():
        return {"all": 0, "oku_all": 0, "oku_short": 0,
                "amb": 0, "oku": 0, "far": 0, "near": 0, "desc": 0,
                "mx_amb": 0, "mx_oku": 0, "mx_far": 0, "mx_near": 0,
                "sb_amb": 0, "sb_oku": 0, "sb_far": 0, "sb_near": 0}

    by = {}
    tot = blank()
    t0 = time.time()
    for idx, e in enumerate(entries):
        off = int(e["offset"], 16)
        try:
            plain, info = ctlz.decode(rom, off, trace=True)
        except ctlz.CtlzError:
            continue
        plain = bytes(plain)
        if len(plain) == 0:
            continue
        src = e.get("source") or "?"
        atype = src.split(":")[-1] if ":" in src else src
        st_ = by.setdefault(atype, blank())
        rng, mc = (0x07FF, 34) if info["mode"] == 11 else (0x0FFF, 18)
        mf = MatchFinder(plain)
        tree = OkumuraTree(plain, rng + 1, mc, lzhuf=a.lzhuf)
        for (p, is_match, o, L) in info["elements"]:
            if not is_match:
                continue
            res = mf.find_ties(p, rng, mc)
            if res is None:
                continue
            m, js = res
            if L != m:
                continue                      # non-longest: 0.094%, out of scope here
            sq_j = p - o
            tm = tree.match_at(p)
            for d in (st_, tot):
                d["all"] += 1
            if tm is None or tm[0] != m:
                for d in (st_, tot):
                    d["oku_short"] += 1       # tree missed the longest match entirely
            elif tm[1] == sq_j:
                for d in (st_, tot):
                    d["oku_all"] += 1
            if len(js) < 2:
                continue                      # unambiguous: no choice was made
            is_far = (js[0] == sq_j)
            is_near = (js[-1] == sq_j)
            is_oku = (tm is not None and tm[0] == m and tm[1] == sq_j)
            pre = "mx_" if m >= mc else "sb_"  # maxcopy stratum vs sub-max stratum
            for d in (st_, tot):
                d["amb"] += 1
                d[pre + "amb"] += 1
                if is_far:
                    d["far"] += 1
                    d[pre + "far"] += 1
                if is_near:
                    d["near"] += 1
                    d[pre + "near"] += 1
                if is_oku:
                    d["oku"] += 1
                    d[pre + "oku"] += 1
                # descending-scan rival: nearest at maxcopy, farthest below it
                if (is_near if m >= mc else is_far):
                    d["desc"] += 1
        if a.progress and (idx + 1) % 25 == 0:
            print(f"  ... {idx+1}/{len(entries)}  {time.time()-t0:.0f}s",
                  file=sys.stderr)

    def pct(x, d):
        return f"{100.0*x/d:6.2f}%" if d else "     -"

    label = "okumura-lzhuf" if a.lzhuf else "okumura"
    order = sorted(by, key=lambda k: -by[k]["amb"])

    def table(title, pre, rivals):
        print(f"\n{title}   finder={label}")
        head = f"{'class':10} {'ambiguous':>10} {'OKUMURA':>9}"
        for rn, _ in rivals:
            head += f" {rn:>9}"
        print(head)
        for k in order + ["TOTAL"]:
            d = by[k] if k != "TOTAL" else tot
            nn = d[pre + "amb"]
            if not nn:
                continue
            line = f"{k:10} {nn:10} {pct(d[pre+'oku'], nn):>9}"
            for _, rk in rivals:
                line += f" {pct(d[pre+rk], nn):>9}"
            print(line)

    table("PER-TIE, SUB-MAXCOPY STRATUM (length < maxcopy)", "sb_",
          [("farthest", "far"), ("nearest", "near")])
    table("PER-TIE, MAXCOPY STRATUM (length == maxcopy)", "mx_",
          [("farthest", "far"), ("nearest", "near")])
    table("PER-TIE, BLENDED (both strata -- NOT interpretable alone)", "",
          [("farthest", "far"), ("nearest", "near"), ("desc-scan", "desc")])
    print(f"\nover ALL max-length matches ({d['all']}): "
          f"okumura exact = {pct(d['oku_all'], d['all'])}; "
          f"tree failed to reach the longest match = {pct(d['oku_short'], d['all'])}")
    print(f"elapsed {time.time()-t0:.0f}s")
    if a.out:
        with open(a.out, "w", encoding="utf-8") as f:
            json.dump({"finder": label, "total": tot, "by_class": by}, f, indent=2)
    return 0


# ======================================================================================
# Self-test
# ======================================================================================

def cmd_selftest(a) -> int:
    import random
    fails = []

    def check(name, got, want):
        if got != want:
            fails.append(f"{name}: got {got!r}, want {want!r}")
        else:
            print(f"  ok  {name}")

    # 1. Round-trip through the verified decoder, for every strategy axis.
    random.seed(1234)
    corpora = [
        b"A" * 64,
        b"ABCD" * 40,
        bytes(random.randrange(256) for _ in range(500)),
        b"the quick brown fox " * 13 + b"!",
        bytes([0]) * 3 + b"hello world hello world hello",
        bytes(range(256)),
        b"x",
        b"xy",
    ]
    for st in (GEIGER,
               Strategy(tiebreak="farthest"),
               Strategy(leak=False),
               Strategy(lazy=1, leak=False),
               Strategy(mode="11"), Strategy(mode="12")):
        for c in corpora:
            try:
                blob = encode(c, st)
                back, _ = ctlz.decode(blob, 0)
            except Exception as e:  # noqa: BLE001
                fails.append(f"roundtrip {st.name} len={len(c)}: {e}")
                continue
            if bytes(back) != c:
                fails.append(f"roundtrip MISMATCH {st.name} len={len(c)}")
        print(f"  ok  round-trip {st.name}")

    # 2. The match finder must agree EXACTLY with a literal transcription of
    #    Geiger's loop (:41-57). This is the load-bearing optimization.
    def geiger_loop(src, pos, rng, maxcopy, cur, strict):
        n = len(src)
        lo = pos - rng if pos > rng else 0
        best = (cur, None)
        j = lo
        while j < pos:
            k = 0
            cap = min(maxcopy, n - pos)
            while k < cap and src[j + k] == src[pos + k]:
                k += 1
            ok = (k > best[0]) if strict else (k >= best[0])
            if ok:
                best = (k, j)
                if k == maxcopy:
                    break
            j += 1
        return None if best[1] is None else best

    random.seed(99)
    bad = 0
    for trial in range(60):
        n = random.randrange(4, 200)
        alpha = random.choice([2, 3, 8, 256])
        src = bytes(random.randrange(alpha) for _ in range(n))
        for strict in (False, True):
            mf = MatchFinder(src)
            for pos in range(1, n):
                for cur in (0, 1, 3, 7):
                    rng, mc = 0x07FF, 34
                    a1 = mf.find(pos, rng, mc, cur, "farthest" if strict else "nearest")
                    a2 = geiger_loop(src, pos, rng, mc, cur, strict)
                    if a1 != a2:
                        bad += 1
                        if bad < 4:
                            fails.append(f"finder != geiger: trial{trial} pos{pos} "
                                         f"cur{cur} strict{strict}: {a1} vs {a2}")
    check("match finder == verbatim Geiger loop", bad, 0)

    # 3. Structural rules the ASM demands.
    blob = encode(b"the quick brown fox jumps over the quick brown fox", GEIGER)
    size = blob[0] | (blob[1] << 8)
    marker = blob[2 + size]
    check("body ends exactly on the marker (CPX:BEQ equality)",
          2 + size < len(blob), True)
    check("11-bit mode marker is $40 not $C0", marker & 0xC0, 0x40)
    check("terminator has $3F clear", blob[-1] & 0x3F, 0)
    if marker & 0x3F:
        ctrl = blob[2 + size + 3]
        check("addendum ctrl byte is nonzero (Q1 padding rule)", ctrl != 0, True)
        pad = (0xFF << (marker & 0x3F)) & 0xFF
        check("addendum padding is 0xFF<<bitctr", ctrl & pad, pad)

    # 4. Self-overlap must be emittable -- a pure run is the canonical case.
    blob = encode(b"Q" + b"Z" * 60, GEIGER)
    back, inf = ctlz.decode(blob, 0)
    check("run round-trips", bytes(back), b"Q" + b"Z" * 60)
    check("run uses self-overlapping back-refs", inf["overlap_matches"] > 0, True)

    # 5. Mode selection: the first marker's $C0 must reflect the forced mode.
    def first_marker(b):
        return b[2 + (b[0] | (b[1] << 8))]

    sample = b"abcabcabcabc" * 9
    check("mode=11 emits $40", first_marker(encode(sample, Strategy(mode="11"))) & 0xC0,
          0x40)
    check("mode=12 emits $00", first_marker(encode(sample, Strategy(mode="12"))) & 0xC0,
          0x00)

    # 6. Quirk Q7: --tailpad only ever changes the length by one trailing $00, and
    #    only for blobs with no addendum.
    for c in corpora:
        on = encode(c, Strategy(tailpad=True))
        off = encode(c, Strategy(tailpad=False))
        if len(on) != len(off):
            if on[:-1] != off or on[-1] != 0:
                fails.append(f"tailpad differs by more than a trailing $00 (len {len(c)})")
    print("  ok  tailpad is a pure trailing-$00 axis")

    if fails:
        print("\nFAILURES:")
        for f in fails[:25]:
            print("  " + f)
        return 1
    print("\nctlzpack: all selftests passed")
    return 0


# ======================================================================================
# CLI
# ======================================================================================

def _add_strategy_args(p):
    p.add_argument("--tiebreak",
                   choices=["nearest", "farthest", "nearest-full", "middle",
                            "repeat", "repeat-near", "okumura", "okumura-lzhuf"],
                   default="nearest")
    p.add_argument("--leak", choices=["on", "off"], default="on")
    p.add_argument("--lazy", type=int, default=0)
    p.add_argument("--mode", choices=["both", "11", "12", "orig"], default="both")
    p.add_argument("--mode-tie", dest="mode_tie", choices=["11", "12"], default="11")
    p.add_argument("--tailpad", choices=["on", "off"], default="on")


def main(argv=None) -> int:
    p = argparse.ArgumentParser(
        prog="ctlzpack.py",
        description="Chrono Trigger (US) LZSS encoder + byte-identity harness")
    p.add_argument("--version", action="version", version=TOOL_VERSION)
    sub = p.add_subparsers(dest="cmd", required=True)

    k = sub.add_parser("pack", help="re-encode one corpus blob")
    k.add_argument("--rom", required=True)
    k.add_argument("--offset", required=True, type=lambda s: int(s, 0))
    k.add_argument("--out", default=None)
    _add_strategy_args(k)
    k.set_defaults(fn=cmd_pack)

    c = sub.add_parser("compress",
                       help="compress a plaintext file to a standalone blob file")
    c.add_argument("--in", dest="inp", required=True, metavar="PATH",
                   help="plaintext to encode")
    c.add_argument("--out", required=True, metavar="PATH",
                   help="write the compressed blob here")
    c.add_argument("--mode", required=True, type=int, choices=[11, 12],
                   help="offset width to encode with; per-blob metadata, not a policy")
    c.set_defaults(fn=cmd_compress)

    i = sub.add_parser("identity", help="run the identity harness over the corpus")
    i.add_argument("--rom", required=True)
    i.add_argument("--manifest", required=True)
    i.add_argument("--sample", type=int, default=0,
                   help="evenly-spaced subset size (0 = full corpus)")
    i.add_argument("--report", default=None, help="write per-blob JSON here")
    i.add_argument("--show", action="store_true", help="print non-identical blobs")
    i.add_argument("--progress", action="store_true")
    _add_strategy_args(i)
    i.set_defaults(fn=cmd_identity)

    s = sub.add_parser("sweep", help="grid-sweep strategies against the harness")
    s.add_argument("--rom", required=True)
    s.add_argument("--manifest", required=True)
    s.add_argument("--sample", type=int, default=0)
    s.add_argument("--report", default=None)
    s.add_argument("--tiebreaks", default="nearest,farthest")
    s.add_argument("--leaks", default="on,off")
    s.add_argument("--lazies", default="0")
    s.add_argument("--modes", default="both")
    s.add_argument("--mode-ties", dest="mode_ties", default="11")
    s.add_argument("--tailpads", default="on")
    s.set_defaults(fn=cmd_sweep)

    v = sub.add_parser("solve", help="per-blob strategy solver (union rate)")
    v.add_argument("--rom", required=True)
    v.add_argument("--manifest", required=True)
    v.add_argument("--out", required=True)
    v.add_argument("--sample", type=int, default=0)
    v.add_argument("--no-flip", action="store_true",
                   help="skip the opposite-mode control arm")
    v.add_argument("--ties", action="store_true", help="record tie density per blob")
    v.add_argument("--progress", action="store_true")
    v.set_defaults(fn=cmd_solve)

    rc = sub.add_parser("reach", help="is Square's choice reachable by any greedy rule?")
    rc.add_argument("--rom", required=True)
    rc.add_argument("--table", required=True, help="solve output JSON")
    rc.add_argument("--sample", type=int, default=0)
    rc.add_argument("--out", default=None)
    rc.set_defaults(fn=cmd_reach)

    oc = sub.add_parser("okucmp", help="per-tie agreement: Okumura tree vs Square")
    oc.add_argument("--rom", required=True)
    oc.add_argument("--manifest", required=True)
    oc.add_argument("--sample", type=int, default=0)
    oc.add_argument("--lzhuf", action="store_true", help="LZHUF nearest-tie variant")
    oc.add_argument("--out", default=None)
    oc.add_argument("--progress", action="store_true")
    oc.set_defaults(fn=cmd_okucmp)

    t = sub.add_parser("selftest", help="round-trip + finder-equivalence assertions")
    t.set_defaults(fn=cmd_selftest)

    a = p.parse_args(argv)
    return a.fn(a)


if __name__ == "__main__":
    sys.exit(main())

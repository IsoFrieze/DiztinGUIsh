using System;
using System.Collections.Generic;
using System.Linq;
using Diz.Core.Interfaces;
using Diz.Core.util;

namespace Diz.Core.model.snes;

/// <summary>
/// Shared bank-region synthesis logic (docs/diz/regions-as-partition-plan.md §A.5).
///
/// This is the ONE place that decides "does bank N need a synthesized whole-bank
/// file-producing region, or is it already covered?" It is used from two call sites that
/// must never disagree with each other:
///
///   - Diz.LogWriter.AsmCreationInstructions.GenerateSyntheticBankRegions -- in-memory,
///     every export, over the flattened region list (persisted + comment-derived).
///   - the save-format-107 migration / on-import synthesis (Diz.Cpu.65816) -- persists real
///     Region objects into project.Data.Regions.
///
/// Because both call sites run the identical skip logic (a persisted region that EXACTLY
/// matches a bank's extent is treated as already covering it), a project that has gone
/// through migration/import will already have exact-match persisted bank regions, and the
/// export-time synthesis will see them and skip -- no duplicate regions, no laminar-family
/// violation. This is deliberate: it is what keeps the two mechanisms (in-memory synthesis
/// from step 4, and persistence from step 5) from both firing for the same bank. See the "As
/// built -- two deviations to reconcile" note at the end of §A.4.
/// </summary>
public static class BankRegionSynthesis
{
    /// <summary>
    /// Compute the whole-bank, file-producing regions that need to be added for a ROM of the
    /// given size, given the regions that already exist. Skips any bank whose bytes are already
    /// exactly covered by an existing file-producing region (exact extent match), or partially
    /// crossed by one (in which case the user/migration is expected to have already tiled it by
    /// hand -- see plan §B.5). Purely additive: never mutates or returns anything for existing
    /// regions, just the NEW ones to add.
    ///
    /// EndSnesAddress is inclusive throughout (§A.2.2) -- bank C0 is $C00000-$C0FFFF.
    /// </summary>
    /// <param name="existingRegions">every region that already exists (persisted + any transient/comment-derived)</param>
    /// <param name="romSize">ROM size in bytes (PC address space)</param>
    /// <param name="bankSize">bytes per bank in PC address space (0x8000 for LoRom, 0x10000 otherwise)</param>
    /// <param name="convertPcToSnes">PC offset -> SNES address, mapping-mode-aware; return -1 for "doesn't map"</param>
    public static List<Region> SynthesizeMissingBankRegions(
        IEnumerable<IRegion> existingRegions,
        int romSize,
        int bankSize,
        Func<int, int> convertPcToSnes)
    {
        var result = new List<Region>();

        if (bankSize <= 0)
            return result;

        var existingFileProducing = existingRegions.Where(r => r.IsFileProducingRegion()).ToList();
        var seenBanks = new HashSet<int>();

        for (var offset = 0; offset < romSize; offset += bankSize)
        {
            var snesAddress = convertPcToSnes(offset);
            if (snesAddress == -1)
                continue;

            var bank = RomUtil.GetBankFromSnesAddress(snesAddress);
            if (!seenBanks.Add(bank))
                continue;

            var bankStart = bank << 16;
            var bankEnd = bankStart | 0xFFFF;

            // An existing region that EXACTLY matches this bank already covers it (nothing to
            // add -- this is the reconciliation case: a persisted region from a prior
            // migration/import run, or a hand-authored region of identical extent like CT's
            // "BankC0 - location"). One that CROSSES the bank boundary (overlaps but is neither
            // an exact match nor fully nested inside) means the user/migration is expected to
            // have already tiled this bank's remaining bytes by hand (plan doc §B.5) --
            // synthesis must not add a region that would partially cross it.
            var skip = existingFileProducing.Any(r =>
            {
                var overlaps = r.StartSnesAddress <= bankEnd && r.EndSnesAddress >= bankStart;
                if (!overlaps)
                    return false;

                var exactMatch = r.StartSnesAddress == bankStart && r.EndSnesAddress == bankEnd;
                var nestedWithinBank = r.StartSnesAddress >= bankStart && r.EndSnesAddress <= bankEnd;
                return exactMatch || !nestedWithinBank;
            });

            if (skip)
                continue;

            result.Add(new Region
            {
                RegionName = $"bank_{Util.NumberToBaseString(bank, Util.NumberBase.Hexadecimal, 2)}",
                StartSnesAddress = bankStart,
                EndSnesAddress = bankEnd,
                ExportSeparateFile = true,
                Priority = 0,
                ExportType = RegionExportType.Assembly,
            });
        }

        return result;
    }
}

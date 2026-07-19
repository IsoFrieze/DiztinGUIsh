using System.Collections.Generic;
using System.Linq;
using Diz.Core.Interfaces;

namespace Diz.Core.model.snes;

/// <summary>
/// Regions have three roles, derived from the existing flags rather than a new enum field
/// (see docs/diz/regions-as-partition-plan.md §A.3):
///
///   File-producing -- ExportSeparateFile == true. Gets its own output file + an incsrc in
///                      its parent's file.
///   Asset          -- ExportType is Asset or Binary. Gets an incbin into the enclosing file.
///   Annotation     -- neither of the above. Emits nothing; exists purely for meaning
///                      (naming, ContextToApply, the region-path query).
///
/// NOTE: these are independent flag checks -- nothing in the model stops a region from setting
/// both ExportSeparateFile=true and ExportType=Asset. That combination is meaningless today
/// (ExportSeparateFile means "emit a .asm file"; assets go through RegionAssetExportService
/// instead), so RegionValidation rejects it. These helpers report exactly what the flags say
/// rather than silently picking a winner. IsAnnotationRegion is the only one of the three
/// derived from the other two.
/// </summary>
public static class RegionRoleExtensions
{
    public static bool IsFileProducingRegion(this IRegion region) =>
        region.ExportSeparateFile;

    // matches RegionAssetExportService.IsAssetRegion (ExportType != Assembly) -- kept as a
    // separate helper here so callers that only have Diz.Core (not Diz.LogWriter) can ask the
    // same question.
    public static bool IsAssetRegion(this IRegion region) =>
        region.ExportType is RegionExportType.Asset or RegionExportType.Binary;

    public static bool IsAnnotationRegion(this IRegion region) =>
        !region.IsFileProducingRegion() && !region.IsAssetRegion();
}

/// <summary>
/// The two non-crossing constraints from §A.3 step 2:
///
///   File-producing regions must form a LAMINAR family: any two are disjoint or one fully
///   contains the other. Partial crossing (start inside one, end outside it) is an error.
///   Byte-identical duplicate ranges are also an error -- see §A.2.1: "a degenerate case that
///   validation should probably reject outright rather than silently order."
///
///   Asset regions must not overlap each other AT ALL (not even nested).
///
/// Annotation regions are exempt from both checks entirely.
///
/// This returns every problem found rather than throwing on the first one, so a caller (e.g.
/// a future RegionListUserControl wiring) can report the whole list at once -- mirrors the
/// existing single-region checks in RegionGridView_RowValidating, just operating over the
/// whole collection instead of one row.
/// </summary>
public static class RegionValidation
{
    public static List<string> ValidateNonCrossing(IEnumerable<IRegion> allRegions)
    {
        var regions = allRegions.ToList();
        var problems = new List<string>();

        ValidateRolesAreExclusive(regions, problems);
        ValidateLaminarFamily(regions.Where(r => r.IsFileProducingRegion()).ToList(), problems);
        ValidateNoOverlap(regions.Where(r => r.IsAssetRegion()).ToList(), problems);

        return problems;
    }

    // ExportSeparateFile currently means specifically "emit a separate .asm file". Asset regions
    // don't use it -- they get their .bin/.png from RegionAssetExportService, a different path
    // entirely -- which is why CT's three asset regions correctly have it set to false. So the
    // two output roles are mutually exclusive today, and a region claiming both is a data error.
    //
    // Step 4 may well redefine ExportSeparateFile as "produces its own file, of whatever kind",
    // folding assets into the laminar family. That's a deliberate rewiring with a byte-identity
    // gate attached; until then this check pins the current meaning.
    private static void ValidateRolesAreExclusive(IEnumerable<IRegion> regions, List<string> problems)
    {
        foreach (var region in regions.Where(r => r.IsFileProducingRegion() && r.IsAssetRegion()))
        {
            problems.Add(
                $"Region '{region.RegionName}' (${region.StartSnesAddress:X6}-${region.EndSnesAddress:X6}) has " +
                $"both ExportSeparateFile=true and ExportType={region.ExportType}; a region emits either its own " +
                ".asm file or an asset, not both.");
        }
    }

    private static void ValidateLaminarFamily(IReadOnlyList<IRegion> fileProducingRegions, List<string> problems)
    {
        for (var i = 0; i < fileProducingRegions.Count; i++)
        for (var j = i + 1; j < fileProducingRegions.Count; j++)
        {
            var a = fileProducingRegions[i];
            var b = fileProducingRegions[j];

            if (IsDisjoint(a, b))
                continue;

            if (a.StartSnesAddress == b.StartSnesAddress && a.EndSnesAddress == b.EndSnesAddress)
            {
                problems.Add(
                    $"File-producing regions '{a.RegionName}' and '{b.RegionName}' have identical byte ranges " +
                    $"(${a.StartSnesAddress:X6}-${a.EndSnesAddress:X6}); this is ambiguous nesting, not a valid " +
                    "laminar family member.");
                continue;
            }

            if (FullyContains(a, b) || FullyContains(b, a))
                continue; // proper nesting -- fine

            problems.Add(
                $"File-producing regions '{a.RegionName}' (${a.StartSnesAddress:X6}-${a.EndSnesAddress:X6}) and " +
                $"'{b.RegionName}' (${b.StartSnesAddress:X6}-${b.EndSnesAddress:X6}) partially cross; " +
                "file-producing regions must be disjoint or fully nested.");
        }
    }

    private static void ValidateNoOverlap(IReadOnlyList<IRegion> assetRegions, List<string> problems)
    {
        for (var i = 0; i < assetRegions.Count; i++)
        for (var j = i + 1; j < assetRegions.Count; j++)
        {
            var a = assetRegions[i];
            var b = assetRegions[j];

            if (IsDisjoint(a, b))
                continue;

            problems.Add(
                $"Asset regions '{a.RegionName}' (${a.StartSnesAddress:X6}-${a.EndSnesAddress:X6}) and " +
                $"'{b.RegionName}' (${b.StartSnesAddress:X6}-${b.EndSnesAddress:X6}) overlap; " +
                "asset regions must not overlap each other.");
        }
    }

    // EndSnesAddress is inclusive throughout (see A.2.2), so these compare on that basis.
    private static bool IsDisjoint(IRegion a, IRegion b) =>
        a.EndSnesAddress < b.StartSnesAddress || b.EndSnesAddress < a.StartSnesAddress;

    private static bool FullyContains(IRegion outer, IRegion inner) =>
        outer.StartSnesAddress <= inner.StartSnesAddress && outer.EndSnesAddress >= inner.EndSnesAddress;
}

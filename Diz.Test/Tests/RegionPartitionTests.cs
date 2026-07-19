using System.Linq;
using Diz.Core.Interfaces;
using Diz.Core.model.snes;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests;

/// <summary>
/// Tests for the region-partition step of docs/diz/regions-as-partition-plan.md §A.3:
///   - role split by flag (file-producing / asset / annotation)
///   - Data.GetRegionPath (every region covering a byte, most-specific first)
///   - RegionValidation.ValidateNonCrossing (laminar file-producing family + non-overlapping assets)
///
/// EndSnesAddress is inclusive throughout (the last byte IN the region), per §A.2.2.
/// </summary>
public class RegionPartitionTests
{
    private static Region MakeRegion(
        string name,
        int start,
        int end,
        bool exportSeparateFile = false,
        RegionExportType exportType = RegionExportType.Assembly,
        int priority = 0) =>
        new()
        {
            RegionName = name,
            StartSnesAddress = start,
            EndSnesAddress = end,
            ExportSeparateFile = exportSeparateFile,
            ExportType = exportType,
            Priority = priority,
        };

    // ------------------------------------------------------------------
    // role split
    // ------------------------------------------------------------------

    [Fact]
    public void FileProducingRoleComesFromExportSeparateFileFlag()
    {
        var region = MakeRegion("bank_C0", 0xC00000, 0xC0FFFF, exportSeparateFile: true);

        region.IsFileProducingRegion().Should().BeTrue();
        region.IsAssetRegion().Should().BeFalse();
        region.IsAnnotationRegion().Should().BeFalse();
    }

    [Theory]
    [InlineData(RegionExportType.Asset)]
    [InlineData(RegionExportType.Binary)]
    public void AssetRoleComesFromExportTypeAssetOrBinary(RegionExportType exportType)
    {
        var region = MakeRegion("AudioBRR_00", 0xC7730D, 0xC77400, exportType: exportType);

        region.IsAssetRegion().Should().BeTrue();
        region.IsFileProducingRegion().Should().BeFalse();
        region.IsAnnotationRegion().Should().BeFalse();
    }

    [Fact]
    public void AnnotationRoleIsNeitherFileProducingNorAsset()
    {
        var region = MakeRegion("bank_C8_ctx", 0xC80000, 0xC8FFFF); // both flags default/off

        region.IsAnnotationRegion().Should().BeTrue();
        region.IsFileProducingRegion().Should().BeFalse();
        region.IsAssetRegion().Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // GetRegionPath
    // ------------------------------------------------------------------

    [Fact]
    public void InclusiveBoundaryBehaviorAtBothEndsOfARange()
    {
        var data = new Data();
        data.Regions.Add(MakeRegion("r", 0xC00000, 0xC000FF));

        // both endpoints are IN the region (inclusive), the bytes just outside are not
        data.GetRegionPath(0xC00000).Should().ContainSingle();
        data.GetRegionPath(0xC000FF).Should().ContainSingle();
        data.GetRegionPath(0xBFFFFF).Should().BeEmpty();
        data.GetRegionPath(0xC00100).Should().BeEmpty();

        data.GetRegion(0xC00000).Should().NotBeNull();
        data.GetRegion(0xC000FF).Should().NotBeNull();
        data.GetRegion(0xBFFFFF).Should().BeNull();
        data.GetRegion(0xC00100).Should().BeNull();
    }

    [Fact]
    public void NestedRegionsAreOrderedNarrowestFirst()
    {
        var data = new Data();
        var outer = MakeRegion("outer", 0xC00000, 0xC0FFFF, exportSeparateFile: true);
        var middle = MakeRegion("middle", 0xC00100, 0xC001FF, exportSeparateFile: true);
        var inner = MakeRegion("inner", 0xC00150, 0xC0015F, exportType: RegionExportType.Asset);

        // add out of nesting order to prove GetRegionPath sorts rather than preserving
        // insertion order
        data.Regions.Add(middle);
        data.Regions.Add(outer);
        data.Regions.Add(inner);

        var path = data.GetRegionPath(0xC00155);

        path.Select(r => r.RegionName).Should().Equal("inner", "middle", "outer");
    }

    [Fact]
    public void SameExtentRegionsTiebreakOnPriorityDescending()
    {
        var data = new Data();
        var low = MakeRegion("low_priority", 0xC00000, 0xC000FF, priority: 1);
        var high = MakeRegion("high_priority", 0xC00000, 0xC000FF, priority: 5);

        data.Regions.Add(low);
        data.Regions.Add(high);

        var path = data.GetRegionPath(0xC00050);

        path.Select(r => r.RegionName).Should().Equal("high_priority", "low_priority");
    }

    // ------------------------------------------------------------------
    // RegionValidation.ValidateNonCrossing
    // ------------------------------------------------------------------

    [Fact]
    public void AnnotationRegionsOverlapFreelyWithNoValidationProblems()
    {
        var a = MakeRegion("bank_C8", 0xC80000, 0xC8FFFF);
        var b = MakeRegion("audio_ctx", 0xC80500, 0xC90500); // partially crosses `a`, but both are annotation

        var problems = RegionValidation.ValidateNonCrossing([a, b]);

        problems.Should().BeEmpty();
    }

    [Fact]
    public void PartiallyCrossingFileProducingRegionsAreFlagged()
    {
        var a = MakeRegion("audio_data", 0xC7730D, 0xC90000, exportSeparateFile: true);
        var b = MakeRegion("bank_C8", 0xC80000, 0xCA0000, exportSeparateFile: true); // starts inside a, ends past a's end -- partial cross

        var problems = RegionValidation.ValidateNonCrossing([a, b]);

        problems.Should().ContainSingle();
        problems[0].Should().Contain("audio_data").And.Contain("bank_C8").And.Contain("partially cross");
    }

    [Theory]
    [InlineData(RegionExportType.Asset)]
    [InlineData(RegionExportType.Binary)]
    public void RegionClaimingBothOutputRolesIsFlagged(RegionExportType exportType)
    {
        // ExportSeparateFile means "emit a .asm file"; assets go through a different path and
        // correctly leave it false (CT's three asset regions all do). Both set is a data error.
        var confused = MakeRegion("confused", 0xC00000, 0xC000FF,
            exportSeparateFile: true, exportType: exportType);

        var problems = RegionValidation.ValidateNonCrossing([confused]);

        problems.Should().ContainSingle();
        problems[0].Should().Contain("confused").And.Contain("not both");
    }

    [Fact]
    public void AssetRegionNestedInsideAFileProducingParentIsValid()
    {
        // the shape CT actually has: an asset incbin'd inside its enclosing bank .asm file.
        // The asset is exempt from the laminar check (annotation-and-asset regions aren't
        // members of the file-producing family), so the containment is simply not its business.
        var parent = MakeRegion("bank_C0", 0xC00000, 0xC0FFFF, exportSeparateFile: true);
        var asset = MakeRegion("item_font", 0xC00100, 0xC001FF,
            exportType: RegionExportType.Asset);

        RegionValidation.ValidateNonCrossing([parent, asset]).Should().BeEmpty();
    }

    [Fact]
    public void TwoOverlappingAssetRegionsAreFlagged()
    {
        var a = MakeRegion("AudioBRR_00", 0xC7730D, 0xC77400, exportType: RegionExportType.Asset);
        var b = MakeRegion("AudioBRR_01", 0xC77380, 0xC77500, exportType: RegionExportType.Asset);

        var problems = RegionValidation.ValidateNonCrossing([a, b]);

        problems.Should().ContainSingle();
        problems[0].Should().Contain("AudioBRR_00").And.Contain("AudioBRR_01").And.Contain("overlap");
    }

    [Fact]
    public void FullyNestedAssetRegionsAreStillFlaggedSinceAssetsMayNotOverlapAtAll()
    {
        // unlike file-producing regions, nesting does not excuse overlap for assets
        var outer = MakeRegion("outer_asset", 0xC00000, 0xC000FF, exportType: RegionExportType.Binary);
        var inner = MakeRegion("inner_asset", 0xC00010, 0xC0001F, exportType: RegionExportType.Asset);

        var problems = RegionValidation.ValidateNonCrossing([outer, inner]);

        problems.Should().ContainSingle();
        problems[0].Should().Contain("overlap");
    }

    [Fact]
    public void DisjointFileProducingRegionsPassValidation()
    {
        var a = MakeRegion("bank_C0", 0xC00000, 0xC0FFFF, exportSeparateFile: true);
        var b = MakeRegion("bank_C1", 0xC10000, 0xC1FFFF, exportSeparateFile: true);

        RegionValidation.ValidateNonCrossing([a, b]).Should().BeEmpty();
    }

    [Fact]
    public void FullyNestedFileProducingRegionsPassValidation()
    {
        var parent = MakeRegion("bank_C0", 0xC00000, 0xC0FFFF, exportSeparateFile: true);
        var child = MakeRegion("bank_C0_ptr_tables", 0xC00100, 0xC001FF, exportSeparateFile: true);

        RegionValidation.ValidateNonCrossing([parent, child]).Should().BeEmpty();
    }

    [Fact]
    public void IdenticalRangeFileProducingRegionsAreFlaggedAsAmbiguousRatherThanSilentlyNested()
    {
        var a = MakeRegion("region_a", 0xC00000, 0xC000FF, exportSeparateFile: true);
        var b = MakeRegion("region_b", 0xC00000, 0xC000FF, exportSeparateFile: true);

        var problems = RegionValidation.ValidateNonCrossing([a, b]);

        problems.Should().ContainSingle();
        problems[0].Should().Contain("identical byte ranges");
    }

    [Fact]
    public void ValidateNonCrossingChecksFileProducingAndAssetSetsIndependently()
    {
        // a crossing pair among file-producing regions and a clean pair of asset regions in
        // the same call -- only the crossing pair should be reported.
        var fileA = MakeRegion("bank_C7", 0xC70000, 0xC7FFFF, exportSeparateFile: true);
        var fileB = MakeRegion("bank_C8_overlap", 0xC7F000, 0xC90000, exportSeparateFile: true);
        var assetA = MakeRegion("AudioBRR_10", 0xC80000, 0xC80100, exportType: RegionExportType.Asset);
        var assetB = MakeRegion("AudioBRR_11", 0xC80101, 0xC80200, exportType: RegionExportType.Asset);

        var problems = RegionValidation.ValidateNonCrossing([fileA, fileB, assetA, assetB]);

        problems.Should().ContainSingle();
        problems[0].Should().Contain("bank_C7").And.Contain("bank_C8_overlap");
    }
}

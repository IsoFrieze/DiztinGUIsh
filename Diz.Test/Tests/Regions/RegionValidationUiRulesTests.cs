using System.Linq;
using Diz.Core.Interfaces;
using Diz.Core.model.snes;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.Regions;

/// <summary>
/// The per-region rules, now that they live in Diz.Core instead of inside a WinForms grid's
/// row-validating handler. Every rule the grid enforced is pinned here, with the ONE deliberate
/// change called out on its own test: a region whose start equals its end is a legal one-byte
/// region, because the end address is inclusive.
///
/// The gfx/BRR descriptors are a second copy of what the asset exporters enforce, so these are
/// also the tests that stop the two copies drifting.
/// </summary>
public class RegionValidationUiRulesTests
{
    private static RegionRowValues Values(
        string name = "region",
        int start = 0x808000,
        int end = 0x80800F,
        bool separateFile = false,
        RegionExportType exportType = RegionExportType.Assembly,
        string assetType = "",
        string assetName = "",
        string assetOptions = "") =>
        new(name, start, end, separateFile, exportType, assetType, assetName, assetOptions);

    private static RegionRowValues GfxValues(string assetType, int length, string options = "") =>
        Values(
            start: 0x808000,
            end: 0x808000 + length - 1,
            exportType: RegionExportType.Asset,
            assetType: assetType,
            assetOptions: options);

    // =========================================================================================
    // check 1 of the row gauntlet: a region must be named
    // =========================================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RegionName_IsRequired(string name)
    {
        var result = RegionRowValidation.ValidateRow(Values(name: name));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("Region Name is required.");
    }

    // =========================================================================================
    // checks 4 + 5: the start/end relationship. THIS is where the behavior deliberately changed.
    // =========================================================================================

    [Fact]
    public void StartEqualToEnd_IsALegalOneByteRegion()
    {
        // the end address is INCLUSIVE, so start == end covers exactly one byte. The old grid
        // called this "zero-length" and refused it, contradicting its own length column.
        var values = Values(start: 0x808000, end: 0x808000);

        values.RegionLength.Should().Be(1);
        RegionRowValidation.ValidateRow(values).IsValid.Should().BeTrue();
    }

    [Fact]
    public void StartGreaterThanEnd_IsStillRejected()
    {
        // regression guard on the one-byte change above: allowing equality must not have
        // allowed inversion.
        var result = RegionRowValidation.ValidateRow(Values(start: 0x808010, end: 0x808000));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("Start address must not be greater than end address.");
    }

    // =========================================================================================
    // checks 6 + 7: address bounds
    // =========================================================================================

    [Theory]
    [InlineData(-1, 0x10)]
    [InlineData(-1, -1)]
    [InlineData(-2, -1)]
    public void NegativeAddresses_AreRejected(int start, int end)
    {
        var result = RegionRowValidation.ValidateRow(Values(start: start, end: end));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("Negative numbers not allowed in SNES addresses");
    }

    [Theory]
    [InlineData(0x1000000, 0x1000000)]
    [InlineData(0x800000, 0x1000000)]
    public void AddressesAbove24Bits_AreRejected(int start, int end)
    {
        var result = RegionRowValidation.ValidateRow(Values(start: start, end: end));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("SNES address too large (max allowed: 24-bits: 0xFFFFFF)");
    }

    [Fact]
    public void TheLargestLegalAddress_IsAccepted()
    {
        RegionRowValidation.ValidateRow(Values(start: 0xFFFFFF, end: 0xFFFFFF))
            .IsValid.Should().BeTrue();
    }

    // =========================================================================================
    // BANK BOUNDARIES ARE NOT A ROW RULE. There used to be a check here refusing a region that
    // both emits its own file and straddles a bank boundary; it is gone, and this pins that.
    // =========================================================================================

    /// <summary>
    /// A file-producing region may span as many bank boundaries as it likes. The only constraint
    /// on file-producing regions is a relationship BETWEEN regions -- they must nest, never
    /// partially overlap -- which is a whole-collection check, not a row check. Banks do not
    /// appear in it. The emitted assembly handles the seam itself: the writer scopes the
    /// assembler's bank-crossing diagnostic off across such a region and re-emits an ORG at the
    /// discontinuity, so a multi-bank file assembles correctly.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ARegionCrossingABankIsAccepted_WhetherOrNotItEmitsItsOwnFile(bool separateFile)
    {
        RegionRowValidation.ValidateRow(Values(start: 0x80FFF0, end: 0x810010, separateFile: separateFile))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void SeparateFileRegionWithinOneBank_IsAccepted()
    {
        RegionRowValidation.ValidateRow(Values(start: 0x80FFF0, end: 0x80FFFF, separateFile: true))
            .IsValid.Should().BeTrue();
    }

    // =========================================================================================
    // check 9: the asset name is a relative path under the asset root and must not escape it
    // =========================================================================================

    [Theory]
    [InlineData("gfx\\font")]
    [InlineData("../secrets")]
    [InlineData("gfx/../../secrets")]
    [InlineData("/etc/passwd")]
    public void AssetNameThatEscapesTheAssetRoot_IsRejected(string assetName)
    {
        var result = RegionRowValidation.ValidateRow(Values(assetName: assetName));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be(
            "Asset Name must be a relative path: no backslashes, no '..', and no leading '/'.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("font")]
    [InlineData("gfx/font")]
    public void RelativeOrEmptyAssetName_IsAccepted(string assetName)
    {
        // empty is fine: the exporter falls back to the region name.
        RegionRowValidation.ValidateRow(Values(assetName: assetName)).IsValid.Should().BeTrue();
    }

    // =========================================================================================
    // check 10: the asset rules, which only apply when the region exports as a typed asset
    // =========================================================================================

    [Fact]
    public void UnregisteredAssetType_IsRejected_AndTheMessageListsTheKnownOnes()
    {
        var result = RegionRowValidation.ValidateRow(
            Values(exportType: RegionExportType.Asset, assetType: "gfx.snes.3bpp"));

        result.IsValid.Should().BeFalse();
        result.Error.Should().StartWith("Asset Type is required when Export Type is 'Asset'. Expected one of: ");
        result.Error.Should().Contain("gfx.snes.2bpp").And.Contain("gfx.snes.8bpp").And.Contain("audio.snes.brr");
    }

    [Fact]
    public void BlankAssetType_IsRejected_WhenExportTypeIsAsset()
    {
        RegionRowValidation.ValidateRow(Values(exportType: RegionExportType.Asset))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void AssetRules_AreSkipped_WhenTheRegionExportsAsPlainAssembly()
    {
        // nonsense asset fields are harmless while nothing reads them.
        RegionRowValidation.ValidateRow(
                Values(exportType: RegionExportType.Assembly, assetType: "not.a.real.type"))
            .IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("gfx.snes.2bpp", 0x10)]
    [InlineData("gfx.snes.2bpp", 0x100)]
    [InlineData("gfx.snes.4bpp", 0x20)]
    [InlineData("gfx.snes.8bpp", 0x40)]
    public void GfxRegionWholeNumberOfTiles_IsAccepted(string assetType, int length)
    {
        RegionRowValidation.ValidateRow(GfxValues(assetType, length)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("gfx.snes.2bpp", 0x11)]
    [InlineData("gfx.snes.4bpp", 0x21)]
    [InlineData("gfx.snes.8bpp", 0x41)]
    public void GfxRegionWithAPartialTile_IsRejected(string assetType, int length)
    {
        var result = RegionRowValidation.ValidateRow(GfxValues(assetType, length));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("must be a whole multiple of").And.Contain(assetType);
    }

    [Fact]
    public void GfxCellHeightOption_ChangesTheCellSizeTheLengthMustDivideBy()
    {
        // 4bpp with cell_h 12 => 48 bytes per cell; 96 is two cells, 64 is not a whole number.
        RegionRowValidation.ValidateRow(GfxValues("gfx.snes.4bpp", 96, "{\"cell_h\": 12}"))
            .IsValid.Should().BeTrue();

        var result = RegionRowValidation.ValidateRow(GfxValues("gfx.snes.4bpp", 64, "{\"cell_h\": 12}"));
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("48 bytes").And.Contain("8x12 cell");
    }

    [Fact]
    public void GfxWithoutCellHeightOption_AssumesEightRowTiles()
    {
        // 0x20 bytes is one 4bpp 8x8 tile; the message names a tile, not a cell.
        RegionRowValidation.ValidateRow(GfxValues("gfx.snes.4bpp", 0x20)).IsValid.Should().BeTrue();

        RegionRowValidation.ValidateRow(GfxValues("gfx.snes.4bpp", 0x30))
            .Error.Should().Contain("one 4bpp tile");
    }

    [Theory]
    [InlineData("{\"cell_h\": 0}")]
    [InlineData("{\"cell_h\": -4}")]
    [InlineData("{\"cell_h\": \"twelve\"}")]
    [InlineData("{\"cell_h\": 12.5}")]
    public void GfxCellHeightMustBeAPositiveInteger(string options)
    {
        var result = RegionRowValidation.ValidateRow(GfxValues("gfx.snes.4bpp", 96, options));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("Asset Options: cell_h must be an integer >= 1.");
    }

    [Fact]
    public void GfxIgnoresOptionsItDoesNotKnow()
    {
        RegionRowValidation.ValidateRow(
                GfxValues("gfx.snes.4bpp", 0x20, "{\"view\": {\"order\": \"column_major\"}}"))
            .IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(9)]
    [InlineData(18)]
    [InlineData(900)]
    public void BrrRegionWholeNumberOfBlocks_IsAccepted(int length)
    {
        RegionRowValidation.ValidateRow(GfxValues("audio.snes.brr", length)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(10)]
    public void BrrRegionWithAPartialBlock_IsRejected(int length)
    {
        var result = RegionRowValidation.ValidateRow(GfxValues("audio.snes.brr", length));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("9 bytes (one BRR ADPCM block)");
    }

    // =========================================================================================
    // fixed-width text tables: options are mandatory, and the region must be whole records
    // =========================================================================================

    private const string TextOptions = "{\"tbl\": \"text/ct_8px.tbl\", \"record_width\": 11, \"pad\": \"0xEF\"}";

    [Theory]
    [InlineData(11)]
    [InlineData(22)]
    [InlineData(2761)]
    public void TextRegionWholeNumberOfRecords_IsAccepted(int length)
    {
        RegionRowValidation.ValidateRow(GfxValues("text.ct.mapped", length, TextOptions))
            .IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(12)]
    public void TextRegionWithARaggedTail_IsRejected(int length)
    {
        var result = RegionRowValidation.ValidateRow(GfxValues("text.ct.mapped", length, TextOptions));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("record_width (11)");
    }

    [Fact]
    public void TextRegionWithNoAssetOptions_IsRejected()
    {
        var result = RegionRowValidation.ValidateRow(GfxValues("text.ct.mapped", 11));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Text assets require Asset Options");
    }

    [Theory]
    // record_width missing, not a number, or below 1
    [InlineData("{\"tbl\": \"t.tbl\", \"pad\": \"0xEF\"}", "record_width")]
    [InlineData("{\"tbl\": \"t.tbl\", \"record_width\": \"11\", \"pad\": \"0xEF\"}", "record_width")]
    [InlineData("{\"tbl\": \"t.tbl\", \"record_width\": 0, \"pad\": \"0xEF\"}", "record_width")]
    // tbl missing or blank
    [InlineData("{\"record_width\": 11, \"pad\": \"0xEF\"}", "tbl")]
    [InlineData("{\"tbl\": \"  \", \"record_width\": 11, \"pad\": \"0xEF\"}", "tbl")]
    // pad missing, or not a byte literal
    [InlineData("{\"tbl\": \"t.tbl\", \"record_width\": 11}", "pad")]
    [InlineData("{\"tbl\": \"t.tbl\", \"record_width\": 11, \"pad\": \"0x100\"}", "pad")]
    [InlineData("{\"tbl\": \"t.tbl\", \"record_width\": 11, \"pad\": \"nope\"}", "pad")]
    public void TextRegionWithBadAssetOptions_IsRejected(string options, string expectedKey)
    {
        var result = RegionRowValidation.ValidateRow(GfxValues("text.ct.mapped", 11, options));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain(expectedKey);
    }

    // The type is matched exactly: a near-miss has no codec downstream, so it must be rejected
    // here rather than accepted and then failed at build time.
    [Fact]
    public void TextAssetTypeNearMiss_IsRejectedAsAnUnknownType()
    {
        var result = RegionRowValidation.ValidateRow(GfxValues("text.ct.mapped2", 11, TextOptions));

        result.IsValid.Should().BeFalse();
        result.Error.Should().StartWith("Asset Type is required when Export Type is 'Asset'. Expected one of: ");
    }

    [Theory]
    [InlineData("{not json at all")]
    [InlineData("{\"cell_h\": }")]
    public void AssetOptionsThatIsNotJson_IsRejected(string options)
    {
        var result = RegionRowValidation.ValidateRow(GfxValues("gfx.snes.4bpp", 0x20, options));

        result.IsValid.Should().BeFalse();
        result.Error.Should().StartWith("Asset Options is not valid JSON: ");
    }

    [Theory]
    [InlineData("[1, 2, 3]")]
    [InlineData("12")]
    [InlineData("\"a string\"")]
    [InlineData("null")]
    public void AssetOptionsThatIsNotAJsonObject_IsRejected(string options)
    {
        var result = RegionRowValidation.ValidateRow(GfxValues("gfx.snes.4bpp", 0x20, options));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("Asset Options must be a JSON object, e.g. {\"cell_h\": 12}.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void BlankAssetOptions_IsAccepted(string options)
    {
        RegionRowValidation.ValidateRow(GfxValues("gfx.snes.4bpp", 0x20, options))
            .IsValid.Should().BeTrue();
    }

    // =========================================================================================
    // the value type the rules run against
    // =========================================================================================

    [Fact]
    public void RegionLength_CountsTheEndByteToo()
    {
        Values(start: 0x100, end: 0x100).RegionLength.Should().Be(1);
        Values(start: 0x100, end: 0x10F).RegionLength.Should().Be(0x10);
    }

    [Fact]
    public void RegionRowValues_CopiesEveryFieldARuleReads()
    {
        var region = new Region
        {
            RegionName = "name",
            StartSnesAddress = 0x808000,
            EndSnesAddress = 0x80801F,
            ExportSeparateFile = true,
            ExportType = RegionExportType.Asset,
            AssetType = "gfx.snes.4bpp",
            AssetName = "gfx/font",
            AssetOptions = "{\"cell_h\": 12}",
        };

        RegionRowValues.From(region).Should().Be(new RegionRowValues(
            "name", 0x808000, 0x80801F, true, RegionExportType.Asset,
            "gfx.snes.4bpp", "gfx/font", "{\"cell_h\": 12}"));
    }

    [Fact]
    public void RegisteredAssetTypeDescriptors_CoverTheGfxAudioAndTextFamilies()
    {
        RegionAssetTypeValidators.All.SelectMany(d => d.ExampleTypes).Should().BeEquivalentTo(
            new[] { "gfx.snes.2bpp", "gfx.snes.4bpp", "gfx.snes.8bpp", "audio.snes.brr", "text.ct.mapped" });
    }
}

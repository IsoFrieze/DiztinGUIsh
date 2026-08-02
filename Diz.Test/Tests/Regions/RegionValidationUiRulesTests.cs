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
    // packed containers (blob.*): the members are authored, so the authoring is what is checked.
    // The buffer they tile does not exist until the build has unpacked it, which is why nothing
    // here looks at the region's length.
    // =========================================================================================

    // straight out of a real project: an LZ-compressed graphics pack whose buffer is one member.
    private const string ContainerOptions =
        "{\"lz\":{\"mode\":12},\"members\":[{\"name\":\"blob/gfx_pack_L12_AE.buffer\",\"at\":0," +
        "\"len\":4096,\"type\":\"gfx.snes.4bpp\"," +
        "\"sha256\":\"0f2dad865acbdbd772d314778de4ea43d3f5c3314f55b3a599776f36c1dc298b\"}]}";

    // also real: three members tiling one buffer, mixing codecs.
    private const string MultiMemberContainerOptions =
        "{\"lz\":{\"mode\":12},\"members\":[" +
        "{\"name\":\"blob/gfx_pack_L12_4.head\",\"at\":0,\"len\":960,\"type\":\"gfx.snes.4bpp\"," +
        "\"sha256\":\"2736177d42982a4ed8e6adc410d3bae9c995c223b5621d6822ebebdd58e65d70\"}," +
        "{\"name\":\"blob/gfx_pack_L12_4.pad\",\"at\":960,\"len\":32,\"type\":\"raw.bin\"," +
        "\"sha256\":\"af9613760f72635fbdb44a5a0a63c39f12af30f950a6ee5c971be188e89c4051\"}," +
        "{\"name\":\"blob/gfx_pack_L12_4.tail\",\"at\":992,\"len\":2080,\"type\":\"gfx.snes.4bpp\"," +
        "\"sha256\":\"8e2f94f32918b1f2fc027e4b50964383f518d6e43a537510975293097a5d0807\"}]}";

    [Fact]
    public void ContainerRegionWithRealAuthoredMembers_IsAccepted()
    {
        RegionRowValidation.ValidateRow(GfxValues("blob.container", 618, ContainerOptions))
            .IsValid.Should().BeTrue();

        RegionRowValidation.ValidateRow(GfxValues("blob.container", 1300, MultiMemberContainerOptions))
            .IsValid.Should().BeTrue();
    }

    // THE regression this family exists to prevent. A container's bytes as stored are compressed,
    // so its region length has no arithmetic relationship to the members' offsets at all -- any
    // length rule would reject every compressed container in a project at once.
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(618)]
    [InlineData(4097)]
    [InlineData(65536)]
    public void ContainerRegion_IsNeverJudgedByItsLength(int length)
    {
        RegionRowValidation.ValidateRow(GfxValues("blob.container", length, ContainerOptions))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void ContainerRegionWithNoAssetOptions_IsRejected()
    {
        var result = RegionRowValidation.ValidateRow(GfxValues("blob.container", 618));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Container assets require Asset Options");
    }

    [Theory]
    // "members" missing entirely, or not an array
    [InlineData("{\"lz\":{\"mode\":12}}", "\"members\" must be an array")]
    [InlineData("{\"members\":{\"name\":\"blob/a\"}}", "\"members\" must be an array")]
    [InlineData("{\"members\":[]}", "\"members\" is empty")]
    // an entry that is not an object at all
    [InlineData("{\"members\":[\"blob/a\"]}", "must be an object")]
    public void ContainerWithAMalformedMembersList_IsRejected(string options, string expected)
    {
        var result = RegionRowValidation.ValidateRow(GfxValues("blob.container", 618, options));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain(expected);
    }

    private static string OneMember(string body) => "{\"members\":[" + body + "]}";

    private const string Sha = "0f2dad865acbdbd772d314778de4ea43d3f5c3314f55b3a599776f36c1dc298b";

    [Theory]
    // name: missing, blank, or not a string
    [InlineData("{\"at\":0,\"len\":16,\"type\":\"raw.bin\",\"sha256\":\"" + Sha + "\"}", "\"name\"")]
    [InlineData("{\"name\":\"  \",\"at\":0,\"len\":16,\"type\":\"raw.bin\",\"sha256\":\"" + Sha + "\"}", "\"name\"")]
    // at: missing, negative, or not an integer
    [InlineData("{\"name\":\"a\",\"len\":16,\"type\":\"raw.bin\",\"sha256\":\"" + Sha + "\"}", "\"at\"")]
    [InlineData("{\"name\":\"a\",\"at\":-1,\"len\":16,\"type\":\"raw.bin\",\"sha256\":\"" + Sha + "\"}", "\"at\"")]
    [InlineData("{\"name\":\"a\",\"at\":\"0\",\"len\":16,\"type\":\"raw.bin\",\"sha256\":\"" + Sha + "\"}", "\"at\"")]
    // len: missing, zero, or fractional
    [InlineData("{\"name\":\"a\",\"at\":0,\"type\":\"raw.bin\",\"sha256\":\"" + Sha + "\"}", "\"len\"")]
    [InlineData("{\"name\":\"a\",\"at\":0,\"len\":0,\"type\":\"raw.bin\",\"sha256\":\"" + Sha + "\"}", "\"len\"")]
    [InlineData("{\"name\":\"a\",\"at\":0,\"len\":1.5,\"type\":\"raw.bin\",\"sha256\":\"" + Sha + "\"}", "\"len\"")]
    // type: missing or blank
    [InlineData("{\"name\":\"a\",\"at\":0,\"len\":16,\"sha256\":\"" + Sha + "\"}", "\"type\"")]
    [InlineData("{\"name\":\"a\",\"at\":0,\"len\":16,\"type\":\"\",\"sha256\":\"" + Sha + "\"}", "\"type\"")]
    // sha256: missing, wrong length, or uppercase (the build compares lowercase hex)
    [InlineData("{\"name\":\"a\",\"at\":0,\"len\":16,\"type\":\"raw.bin\"}", "\"sha256\"")]
    [InlineData("{\"name\":\"a\",\"at\":0,\"len\":16,\"type\":\"raw.bin\",\"sha256\":\"abc123\"}", "\"sha256\"")]
    [InlineData("{\"name\":\"a\",\"at\":0,\"len\":16,\"type\":\"raw.bin\",\"sha256\":\"0F2DAD865ACBDBD772D314778DE4EA43D3F5C3314F55B3A599776F36C1DC298B\"}", "\"sha256\"")]
    public void ContainerMemberWithABadField_IsRejected(string member, string expectedKey)
    {
        var result = RegionRowValidation.ValidateRow(
            GfxValues("blob.container", 618, OneMember(member)));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("members[0]").And.Contain(expectedKey);
    }

    // The tiling check that CAN be done without the buffer: members are ascending and adjacent
    // to each other. Whether they add up to the whole buffer is the build's half.
    [Fact]
    public void ContainerMembersThatLeaveAHole_AreRejected()
    {
        var options = "{\"members\":[" +
                      "{\"name\":\"a\",\"at\":0,\"len\":16,\"type\":\"raw.bin\",\"sha256\":\"" + Sha + "\"}," +
                      "{\"name\":\"b\",\"at\":32,\"len\":16,\"type\":\"raw.bin\",\"sha256\":\"" + Sha + "\"}]}";

        var result = RegionRowValidation.ValidateRow(GfxValues("blob.container", 618, options));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("members[1]").And.Contain("HOLE").And.Contain("16 unclaimed");
    }

    [Fact]
    public void ContainerMembersThatOverlap_AreRejected()
    {
        var options = "{\"members\":[" +
                      "{\"name\":\"a\",\"at\":0,\"len\":16,\"type\":\"raw.bin\",\"sha256\":\"" + Sha + "\"}," +
                      "{\"name\":\"b\",\"at\":8,\"len\":16,\"type\":\"raw.bin\",\"sha256\":\"" + Sha + "\"}]}";

        var result = RegionRowValidation.ValidateRow(GfxValues("blob.container", 618, options));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("OVERLAPS").And.Contain("8 bytes claimed twice");
    }

    [Fact]
    public void ContainerMembersSharingAName_AreRejected()
    {
        // names are file paths, and the two spellings normalize to the same one.
        var options = "{\"members\":[" +
                      "{\"name\":\"blob/a\",\"at\":0,\"len\":16,\"type\":\"raw.bin\",\"sha256\":\"" + Sha + "\"}," +
                      "{\"name\":\"blob\\\\a\",\"at\":16,\"len\":16,\"type\":\"raw.bin\",\"sha256\":\"" + Sha + "\"}]}";

        var result = RegionRowValidation.ValidateRow(GfxValues("blob.container", 618, options));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("declared twice");
    }

    // Deliberately NOT checked here: the transform-stage key and the member type strings. Their
    // registries live with the exporters, and a stale copy of either would reject authoring the
    // build handles fine, so both are left to the build.
    [Fact]
    public void ContainerIgnoresTheTransformStageBlockAndMemberTypeVocabulary()
    {
        var options = "{\"squish\":{\"mode\":7},\"members\":[" +
                      "{\"name\":\"a\",\"at\":0,\"len\":16,\"type\":\"future.codec.v9\"," +
                      "\"sha256\":\"" + Sha + "\"}]}";

        RegionRowValidation.ValidateRow(GfxValues("blob.container", 618, options))
            .IsValid.Should().BeTrue();
    }

    // =========================================================================================
    // verbatim bytes (raw.*): any byte is a valid byte, so only emptiness is refused
    // =========================================================================================

    [Theory]
    [InlineData("raw.bin", 1)]
    [InlineData("raw.bin", 36)]
    [InlineData("raw.bin", 1215)]
    public void RawRegionOfAnyLength_IsAccepted(string assetType, int length)
    {
        RegionRowValidation.ValidateRow(GfxValues(assetType, length)).IsValid.Should().BeTrue();
    }

    // Both families are matched by prefix, mirroring the exporters' own dispatch: the suffix
    // selects nothing downstream, so an unfamiliar one is not a near-miss with no codec.
    [Theory]
    [InlineData("raw.something")]
    [InlineData("blob.pack")]
    public void PrefixFamilies_AcceptSuffixesTheValidatorHasNeverSeen(string assetType)
    {
        var options = assetType.StartsWith("blob.") ? ContainerOptions : "";

        RegionRowValidation.ValidateRow(GfxValues(assetType, 618, options))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void UnknownTypeMessage_NamesTheContainerAndRawFamiliesToo()
    {
        var result = RegionRowValidation.ValidateRow(
            Values(exportType: RegionExportType.Asset, assetType: "gfx.snes.3bpp"));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("blob.container").And.Contain("raw.bin");
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
    public void RegisteredAssetTypeDescriptors_CoverEveryFamilyAnExporterClaims()
    {
        // one entry per exporter family; the two PREFIX families contribute a representative
        // type rather than an unlistable set of suffixes.
        RegionAssetTypeValidators.All.SelectMany(d => d.ExampleTypes).Should().BeEquivalentTo(
            new[]
            {
                "gfx.snes.2bpp", "gfx.snes.4bpp", "gfx.snes.8bpp", "audio.snes.brr",
                "text.ct.mapped", "blob.container", "raw.bin",
            });
    }
}

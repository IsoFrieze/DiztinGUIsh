using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diz.Core.Interfaces;
using Diz.Core.model.snes;
using Diz.LogWriter.assets;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests;

/// <summary>
/// Tests for what an export reports back: the assembly directive AND the build graph, from the
/// same call. The build graph used to be re-derived from the regions after the fact, which meant
/// the same authoring was read twice and the two readings could disagree about what an asset is.
///
/// Also covers the manifest grammar the recursive node model rests on: a node is described
/// either by its codec's typed block or by the members tiling its buffer, never by both.
/// </summary>
public class AssetBuildNodeTests : IDisposable
{
    private readonly string tempDir;

    public AssetBuildNodeTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "diz-node-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { }
    }

    // ---- test doubles ----------------------------------------------------------------------

    private class FakeByteSource : IReadOnlyByteSource
    {
        private readonly byte[] data;
        public FakeByteSource(byte[] data) => this.data = data;
        public byte? GetRomByte(int offset) => offset >= 0 && offset < data.Length ? data[offset] : null;
        public int? GetRomWord(int offset) => null;
        public int? GetRomLong(int offset) => null;
        public int? GetRomDoubleWord(int offset) => null;
    }

    private class FakeAddressConverter : ISnesAddressConverter
    {
        private const int Base = 0xC00000;
        public int ConvertSnesToPc(int offset) => offset >= Base ? offset - Base : -1;
        public int ConvertPCtoSnes(int offset) => offset + Base;
    }

    /// <summary>
    /// A container exporter, unregistered and used only here: it describes its bytes by the
    /// members tiling them, with a decompression stage in between. Exists to exercise the base's
    /// extension points and grammar check without any production exporter depending on them.
    /// </summary>
    private class FakeContainerExporter : BinaryAssetExporterBase
    {
        protected override string AssetTypePrefix => "blob.";
        protected override string CompiledExtension => ".bin";

        protected override void Validate(RegionAssetExportRequest request) { }

        protected override JsonArray BuildPipeline(RegionAssetExportRequest request) =>
        [
            new JsonObject
            {
                ["codec"] = "compress.test.lzss",
                ["lz"] = new JsonObject { ["mode"] = 12 },
            },
        ];

        protected override JsonArray BuildMembers(RegionAssetExportRequest request) =>
        [
            new JsonObject { ["name"] = "gfx/member_a", ["at"] = 0, ["len"] = 32 },
            new JsonObject { ["name"] = "gfx/member_b", ["at"] = 32, ["len"] = 32 },
        ];

        protected override IReadOnlyList<AssetBuildNode> BuildNodes(RegionAssetExportRequest request) =>
        [
            new AssetBuildNode
            {
                Name = RegionAssetUtil.GetAssetName(request.Region),
                AssetType = RegionAssetUtil.GetAssetType(request.Region),
                Members =
                [
                    new AssetBuildNode { Name = "gfx/member_a", AssetType = "gfx.snes.2bpp" },
                    new AssetBuildNode { Name = "gfx/member_b", AssetType = "gfx.snes.2bpp" },
                ],
            },
        ];
    }

    /// <summary>Declares both a typed block and members -- the one shape the grammar forbids.</summary>
    private class BothBlockAndMembersExporter : FakeContainerExporter
    {
        protected override AssetManifestBlock BuildTypeBlock(RegionAssetExportRequest request) =>
            new() { TypeString = "blob.container", BlockKey = "blob", Block = new JsonObject() };
    }

    /// <summary>Declares neither -- describes its bytes not at all.</summary>
    private class NeitherBlockNorMembersExporter : FakeContainerExporter
    {
        protected override JsonArray BuildMembers(RegionAssetExportRequest request) => null;
    }

    /// <summary>Writes a manifest but claims nothing needs rebuilding.</summary>
    private class NoBuildNodesExporter : FakeContainerExporter
    {
        protected override IReadOnlyList<AssetBuildNode> BuildNodes(RegionAssetExportRequest request) => [];
    }

    private static byte[] MakeFakeRom(int size)
    {
        var rom = new byte[size];
        for (var i = 0; i < size; ++i)
            rom[i] = (byte)(i * 7 + 13);
        return rom;
    }

    private static RegionAssetExportService MakeService(params IRegionAssetExporter[] extra) =>
        new(
            new FakeByteSource(MakeFakeRom(0x1000)),
            new FakeAddressConverter(),
            [
                new BinaryRegionAssetExporter(), new GfxRegionAssetExporter(),
                new TextRegionAssetExporter(), .. extra,
            ]);

    private static Region MakeRegion(int startPc, int length, RegionExportType type,
        string assetType, string name, string assetOptions = null) => new()
    {
        RegionName = name,
        AssetName = name,
        StartSnesAddress = 0xC00000 + startPc,
        EndSnesAddress = 0xC00000 + startPc + length - 1,
        ExportType = type,
        AssetType = assetType,
        AssetOptions = assetOptions,
    };

    // ---- the result object -----------------------------------------------------------------

    [Fact]
    public void ExportReportsBothTheDirectiveAndTheBuildGraph()
    {
        var result = MakeService().ExportRegion(
            MakeRegion(0x200, 64, RegionExportType.Asset, "gfx.snes.2bpp", "gfx/font"), tempDir);

        result.AsmDirective.Should().Be("incbin \"build/assets/gfx/font.bin\"");
        result.BuildNodes.Should().ContainSingle()
            .Which.Name.Should().Be("gfx/font");
    }

    [Fact]
    public void PlainBinaryRegionsReportABuildNodeWithASynthesizedRawType()
    {
        // A binary region authors no asset type -- that field is only for typed assets -- but it
        // still has to be extracted and recompiled like everything else, so the build needs a
        // node for it carrying the type that routes it to the passthrough codec.
        var result = MakeService().ExportRegion(
            MakeRegion(0x300, 64, RegionExportType.Binary, null, "data/raw"), tempDir);

        result.BuildNodes.Should().ContainSingle().Which.AssetType.Should().Be("raw.bin");
    }

    [Fact]
    public void CollectedNodesAreEveryExportedRegionInExportOrder()
    {
        var service = MakeService();

        service.ExportRegion(MakeRegion(0x200, 64, RegionExportType.Asset, "gfx.snes.2bpp", "gfx/zebra"), tempDir);
        service.ExportRegion(MakeRegion(0x300, 64, RegionExportType.Binary, null, "data/apple"), tempDir);

        // export order, not sorted: the generator sorts, so the collection stays a faithful
        // record of what was exported.
        service.ExportedBuildNodes.Select(n => n.Name).Should().Equal("gfx/zebra", "data/apple");
    }

    [Fact]
    public void AssemblyRegionsContributeNoBuildNodes()
    {
        var service = MakeService();

        service.ExportRegion(MakeRegion(0, 16, RegionExportType.Assembly, null, "plain"), tempDir)
            .Should().BeNull();

        service.ExportedBuildNodes.Should().BeEmpty();
    }

    [Fact]
    public void TextAssetsReportTheirCharacterTableAsASharedFile()
    {
        // The table is the one input the manifest only NAMES. The exporter is what validated it,
        // so it is what reports it -- the build no longer re-reads the region's options to guess.
        var options = "{\"tbl\": \"text/ct_8px.tbl\", \"record_width\": 11, \"pad\": \"0xEF\"}";

        var result = MakeService().ExportRegion(
            MakeRegion(0x400, 33, RegionExportType.Asset, "text.ct.mapped", "text/names", options), tempDir);

        result.BuildNodes.Should().ContainSingle()
            .Which.SharedFiles.Should().Equal("text/ct_8px.tbl");
    }

    [Fact]
    public void AnExporterThatRebuildsNothingIsRejected()
    {
        var service = MakeService(new NoBuildNodesExporter());

        var act = () => service.ExportRegion(
            MakeRegion(0x200, 64, RegionExportType.Asset, "blob.container", "blob/pack"), tempDir);

        act.Should().Throw<InvalidOperationException>().WithMessage("*never be recompiled*");
    }

    // ---- the node grammar ------------------------------------------------------------------

    [Fact]
    public void AContainerManifestCarriesPipelineAndMembersInPlaceOfATypedBlock()
    {
        var service = MakeService(new FakeContainerExporter());

        service.ExportRegion(
            MakeRegion(0x200, 64, RegionExportType.Asset, "blob.container", "blob/pack"), tempDir);

        var manifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(tempDir, "blob", "pack.json"))).RootElement;

        // key order is load-bearing (it is what the byte-identity gate pins), and a container
        // slots pipeline + members exactly where a leaf slots its typed block.
        manifest.EnumerateObject().Select(p => p.Name).Should()
            .Equal("name", "type", "source", "pipeline", "members", "generated_by");

        manifest.GetProperty("pipeline")[0].GetProperty("lz").GetProperty("mode").GetInt32()
            .Should().Be(12);
        manifest.GetProperty("members").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void ContainerBuildNodesCarryTheirMembers()
    {
        var service = MakeService(new FakeContainerExporter());

        var result = service.ExportRegion(
            MakeRegion(0x200, 64, RegionExportType.Asset, "blob.container", "blob/pack"), tempDir);

        result.BuildNodes.Should().ContainSingle()
            .Which.Members.Select(m => m.Name).Should().Equal("gfx/member_a", "gfx/member_b");
    }

    [Fact]
    public void ANodeDescribedByBothATypedBlockAndMembersIsRejected()
    {
        // Two descriptions of the same bytes are free to disagree; the grammar allows exactly one.
        var service = MakeService(new BothBlockAndMembersExporter());

        var act = () => service.ExportRegion(
            MakeRegion(0x200, 64, RegionExportType.Asset, "blob.container", "blob/pack"), tempDir);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not both*");
    }

    [Fact]
    public void ANodeDescribedByNeitherATypedBlockNorMembersIsRejected()
    {
        var service = MakeService(new NeitherBlockNorMembersExporter());

        var act = () => service.ExportRegion(
            MakeRegion(0x200, 64, RegionExportType.Asset, "blob.container", "blob/pack"), tempDir);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not neither*");
    }

    [Fact]
    public void LeafManifestsGrowNoPipelineOrMembersKey()
    {
        // The extension points must be invisible to every existing asset type: a key that says
        // nothing would still churn every tracked manifest.
        MakeService().ExportRegion(
            MakeRegion(0x200, 64, RegionExportType.Asset, "gfx.snes.2bpp", "gfx/font"), tempDir);

        var manifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(tempDir, "gfx", "font.json"))).RootElement;

        manifest.EnumerateObject().Select(p => p.Name).Should()
            .Equal("name", "type", "source", "gfx", "generated_by");
    }
}

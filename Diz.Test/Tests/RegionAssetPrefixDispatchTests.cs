using System;
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
/// Asset exporter dispatch keys on the AssetType PREFIX (gfx. / audio.), not on the 3-value
/// RegionExportType enum. Gfx and a would-be BRR exporter both carry ExportType == Asset, so
/// the enum alone can't tell them apart -- the prefix must.
///
/// These tests use a throwaway "audio." exporter built on the same BinaryAssetExporterBase gfx
/// now derives from, so they exercise the shared base machinery (source envelope, manifest key
/// order, incbin) generically -- without pulling in the real BRR exporter.
/// </summary>
public class RegionAssetPrefixDispatchTests : IDisposable
{
    private readonly string tempDir;

    public RegionAssetPrefixDispatchTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "diz-prefix-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(tempDir, recursive: true); }
        catch (IOException) { /* best-effort cleanup */ }
    }

    // ------------------------------------------------------------------
    // test doubles
    // ------------------------------------------------------------------

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
    /// A minimal manifest-writing asset exporter for a hypothetical "audio.*" type, built on the
    /// real base. Deliberately trivial -- its job here is to prove the base + prefix dispatch,
    /// not to be a real codec.
    /// </summary>
    private class FakeAudioAssetExporter : BinaryAssetExporterBase
    {
        protected override string AssetTypePrefix => "audio.";
        protected override string CompiledExtension => ".brr";

        protected override void Validate(RegionAssetExportRequest request)
        {
            // pretend BRR: length must be a multiple of 9
            if (request.Bytes.Length == 0 || request.Bytes.Length % 9 != 0)
                throw new InvalidOperationException(
                    $"Region '{request.Region.RegionName}' is {request.Bytes.Length} bytes, not a whole number of 9-byte BRR blocks.");
        }

        protected override AssetManifestBlock BuildTypeBlock(RegionAssetExportRequest request) =>
            new()
            {
                TypeString = request.Region.AssetType,
                BlockKey = "audio",
                Block = new JsonObject { ["blocks"] = request.Bytes.Length / 9 },
                Options = null,
            };
    }

    private static byte[] MakeFakeRom(int size)
    {
        var rom = new byte[size];
        for (var i = 0; i < size; ++i)
            rom[i] = (byte)(i * 7 + 13);
        return rom;
    }

    private static Region MakeRegion(int startPc, int lengthBytes, string assetType, string assetName) =>
        new()
        {
            RegionName = "test_region",
            StartSnesAddress = 0xC00000 + startPc,
            EndSnesAddress = 0xC00000 + startPc + lengthBytes - 1,
            ExportType = RegionExportType.Asset,
            AssetType = assetType,
            AssetName = assetName,
        };

    private RegionAssetExportService MakeServiceWithBoth(byte[] rom) =>
        new(new FakeByteSource(rom), new FakeAddressConverter(),
            [new BinaryRegionAssetExporter(), new GfxRegionAssetExporter(), new FakeAudioAssetExporter()]);

    // ------------------------------------------------------------------

    [Fact]
    public void GfxAndAudioBothHandleAssetButThePrefixRoutesEach()
    {
        // Two exporters, both keyed to ExportType == Asset. Only the AssetType prefix separates
        // them.
        var rom = MakeFakeRom(0x1000);
        var service = MakeServiceWithBoth(rom);

        var gfxRegion = MakeRegion(0x200, 64, "gfx.snes.2bpp", "gfx/pic");     // 4 * 16 bytes
        var audioRegion = MakeRegion(0x400, 27, "audio.brr", "audio/sample"); // 3 * 9 bytes

        var gfxDirective = service.ExportRegion(gfxRegion, tempDir).AsmDirective;
        var audioDirective = service.ExportRegion(audioRegion, tempDir).AsmDirective;

        gfxDirective.Should().Be("incbin \"build/assets/gfx/pic.bin\"");
        audioDirective.Should().Be("incbin \"build/assets/audio/sample.brr\"");

        // a manifest each, and NOTHING else: the ROM bytes are never copied out.
        File.Exists(Path.Combine(tempDir, "gfx", "pic.json")).Should().BeTrue();
        File.Exists(Path.Combine(tempDir, "audio", "sample.json")).Should().BeTrue();
        Directory.EnumerateFiles(tempDir, "*", SearchOption.AllDirectories)
            .Select(Path.GetExtension).Should().AllBe(".json");
    }

    [Fact]
    public void BaseWritesSharedSourceEnvelopeAndFixedManifestKeyOrder()
    {
        // Proves the base -- not the subclass -- authors the `source` envelope and the outer
        // manifest shape. The fake audio exporter contributes only "type" and the "audio" block.
        var rom = MakeFakeRom(0x1000);
        var service = MakeServiceWithBoth(rom);
        var region = MakeRegion(0x400, 27, "audio.brr", "audio/sample");

        service.ExportRegion(region, tempDir);

        var jsonPath = Path.Combine(tempDir, "audio", "sample.json");
        var json = File.ReadAllText(jsonPath);
        var man = JsonDocument.Parse(json).RootElement;

        man.GetProperty("name").GetString().Should().Be("audio/sample");
        man.GetProperty("type").GetString().Should().Be("audio.brr");
        man.GetProperty("generated_by").GetString().Should().Be("DiztinGUIsh");

        var source = man.GetProperty("source");
        source.GetProperty("rom_offset").GetString().Should().Be("0x400");
        source.GetProperty("length").GetInt32().Should().Be(27);
        source.GetProperty("snes_addr").GetString().Should().Be("0xC00400");
        RegionAssetUtil.Sha256Hex(rom[0x400..(0x400 + 27)])
            .Should().Be(source.GetProperty("source_sha256").GetString());

        man.GetProperty("audio").GetProperty("blocks").GetInt32().Should().Be(3);

        // key order is load-bearing for the byte-identity gate: name, type, source, <block>,
        // generated_by (no ver here since none was set).
        var keys = new System.Collections.Generic.List<string>();
        foreach (var p in man.EnumerateObject())
            keys.Add(p.Name);
        keys.Should().Equal("name", "type", "source", "audio", "generated_by");
    }

    [Fact]
    public void ExplicitVersionSlotsBetweenTypeAndSource()
    {
        var service = MakeServiceWithBoth(MakeFakeRom(0x1000));
        var region = MakeRegion(0x400, 27, "audio.brr", "audio/sample");
        region.AssetVersion = "v2";

        service.ExportRegion(region, tempDir);

        var json = File.ReadAllText(Path.Combine(tempDir, "audio", "sample.json"));
        var man = JsonDocument.Parse(json).RootElement;
        man.GetProperty("ver").GetString().Should().Be("v2");

        var keys = new System.Collections.Generic.List<string>();
        foreach (var p in man.EnumerateObject())
            keys.Add(p.Name);
        keys.Should().Equal("name", "type", "ver", "source", "audio", "generated_by");
    }

    [Fact]
    public void AssetRegionWithNoMatchingPrefixThrowsMentioningTheAssetType()
    {
        // Only gfx + binary registered; an "audio.*" asset has no handler. The error must name
        // the asset type so the author knows which exporter is missing.
        var service = new RegionAssetExportService(
            new FakeByteSource(MakeFakeRom(0x1000)), new FakeAddressConverter(),
            [new BinaryRegionAssetExporter(), new GfxRegionAssetExporter()]);
        var region = MakeRegion(0x400, 27, "audio.brr", "audio/sample");

        var act = () => service.ExportRegion(region, tempDir);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*audio.brr*")
            .WithMessage("*no exporter is registered*");
    }

    [Fact]
    public void GfxExporterDeclinesNonGfxAssetTypes()
    {
        // The refactor must not let gfx swallow every Asset region -- it only claims gfx.*
        new GfxRegionAssetExporter().CanExport(MakeRegion(0, 9, "audio.brr", "a")).Should().BeFalse();
        new GfxRegionAssetExporter().CanExport(MakeRegion(0, 64, "gfx.snes.2bpp", "g")).Should().BeTrue();
    }

    [Fact]
    public void BinaryExporterClaimsTheBinaryExportTypeAndTheRawPrefix()
    {
        // The raw exporter is the one asset kind reachable two ways: the plain-binary export
        // type (which carries no AssetType for the prefix to match on) and the type named
        // outright. Both must land here, and it must still not swallow other typed assets.
        var bin = new BinaryRegionAssetExporter();

        var binRegion = MakeRegion(0, 16, null, "b");
        binRegion.ExportType = RegionExportType.Binary;
        bin.CanExport(binRegion).Should().BeTrue();

        bin.CanExport(MakeRegion(0, 16, "raw.bin", "r")).Should().BeTrue();

        bin.CanExport(MakeRegion(0, 64, "gfx.snes.2bpp", "g")).Should().BeFalse();
    }
}

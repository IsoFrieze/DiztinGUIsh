using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Diz.Core.Interfaces;
using Diz.Core.model.snes;
using Diz.LogWriter.assets;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests;

/// <summary>
/// Tests for exporting a region as a standalone asset (a manifest, and the incbin that
/// replaces the region's inline bytes).
///
/// The point of these is byte-identity: the manifest must describe the region's bytes
/// accurately enough -- offset, length, sha256, geometry -- that the external codec tool can
/// slice them out of the ROM and rebuild them. A test that only checked "a file appeared"
/// would pass just as happily with a manifest pointing at the wrong bytes, so every test here
/// asserts on content.
/// </summary>
public class RegionAssetExportTests : IDisposable
{
    private readonly string tempDir;

    public RegionAssetExportTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "diz-asset-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(tempDir, recursive: true); }
        catch (IOException) { /* best-effort cleanup; a leftover temp dir shouldn't fail a test */ }
    }

    // ------------------------------------------------------------------
    // test doubles: a flat fake ROM with an identity-ish SNES<->PC mapping,
    // so the tests exercise the exporter rather than the address converter.
    // ------------------------------------------------------------------

    private class FakeByteSource : IReadOnlyByteSource
    {
        private readonly byte[] data;
        public FakeByteSource(byte[] data) => this.data = data;

        public byte? GetRomByte(int offset) =>
            offset >= 0 && offset < data.Length ? data[offset] : null;

        public int? GetRomWord(int offset) => null;
        public int? GetRomLong(int offset) => null;
        public int? GetRomDoubleWord(int offset) => null;
    }

    private class FakeAddressConverter : ISnesAddressConverter
    {
        // pretend SNES $C0:0000 maps to PC 0, like HiROM.
        private const int Base = 0xC00000;
        public int ConvertSnesToPc(int offset) => offset >= Base ? offset - Base : -1;
        public int ConvertPCtoSnes(int offset) => offset + Base;
    }

    private static byte[] MakeFakeRom(int size)
    {
        var rom = new byte[size];
        for (var i = 0; i < size; ++i)
            rom[i] = (byte)(i * 7 + 13); // arbitrary but deterministic, and not all-same
        return rom;
    }

    private RegionAssetExportService MakeService(byte[] rom) =>
        new(
            new FakeByteSource(rom),
            new FakeAddressConverter(),
            [new BinaryRegionAssetExporter(), new GfxRegionAssetExporter()]
        );

    private static Region MakeRegion(int startPc, int lengthBytes, RegionExportType type, string assetType = null) =>
        new()
        {
            RegionName = "test_region",
            StartSnesAddress = 0xC00000 + startPc,
            EndSnesAddress = 0xC00000 + startPc + lengthBytes - 1,
            ExportType = type,
            AssetType = assetType,
            AssetName = "gfx/test_asset",
        };

    // ------------------------------------------------------------------

    [Fact]
    public void IncbinOfTheCompiledAssetResolvesFromTheAsmDir()
    {
        // The .asm lives in <project>/generated, so the incbin must walk back up. If it
        // didn't, asar would look for generated/build/assets/... and the build would break.
        var rom = MakeFakeRom(0x1000);
        var service = MakeService(rom);
        var region = MakeRegion(0x200, 64, RegionExportType.Asset, "gfx.snes.2bpp");

        var directive = service.ExportRegion(region, tempDir, asmToProjectRootPrefix: "..");

        directive.Should().Be("incbin \"../build/assets/gfx/test_asset.bin\"");

        // the manifest lands in the manifest root it was given, and is the ONLY thing written.
        File.Exists(Path.Combine(tempDir, "gfx", "test_asset.json")).Should().BeTrue();
    }

    [Fact]
    public void NoRomBytesAreCopiedOutOfTheRom()
    {
        // Phase-3 invariant: Diz writes manifests, never game data. The build slices the bytes
        // straight from the ROM using the manifest, so a raw .bin in the export tree would be
        // a second, drifting copy of data the repo deliberately does not carry.
        var service = MakeService(MakeFakeRom(0x1000));

        service.ExportRegion(MakeRegion(0x200, 64, RegionExportType.Asset, "gfx.snes.2bpp"), tempDir);

        Directory.EnumerateFiles(tempDir, "*", SearchOption.AllDirectories)
            .Select(Path.GetExtension).Should().AllBe(".json");
    }

    [Fact]
    public void BuildTierNameIsConfigurable()
    {
        // the tier directory names are per-project settings, not baked into Diz.
        var service = new RegionAssetExportService(
            new FakeByteSource(MakeFakeRom(0x1000)), new FakeAddressConverter(),
            [new BinaryRegionAssetExporter(), new GfxRegionAssetExporter()],
            buildDir: "out/artifacts");

        var directive = service.ExportRegion(
            MakeRegion(0x200, 64, RegionExportType.Asset, "gfx.snes.2bpp"), tempDir, "..");

        directive.Should().Be("incbin \"../out/artifacts/assets/gfx/test_asset.bin\"");
    }

    [Fact]
    public void AssemblyRegionsAreNotAssetRegions()
    {
        var service = MakeService(MakeFakeRom(0x1000));
        var region = MakeRegion(0, 16, RegionExportType.Assembly);

        service.IsAssetRegion(region).Should().BeFalse();
        service.ExportRegion(region, tempDir).Should().BeNull();
    }

    [Fact]
    public void BinaryExportWritesAManifestAndNoRomBytes()
    {
        var rom = MakeFakeRom(0x1000);
        var service = MakeService(rom);
        var region = MakeRegion(startPc: 0x100, lengthBytes: 0x40, RegionExportType.Binary);

        var directive = service.ExportRegion(region, tempDir, asmToProjectRootPrefix: "..");

        // A plain-binary region is an ordinary asset: the codec extracts and recompiles it like
        // any other, so the incbin names the COMPILED payload under the build tier -- not a copy
        // of the ROM bytes in the generated tree, which is what nothing here may write.
        directive.Should().Be("incbin \"../build/assets/gfx/test_asset.bin\"");

        Directory.EnumerateFiles(tempDir, "*", SearchOption.AllDirectories)
            .Select(Path.GetExtension).Should().AllBe(".json");
    }

    [Fact]
    public void BinaryExportSynthesizesTheRawTypeAndDescribesTheBytes()
    {
        // Binary regions carry no AssetType -- that field is only authored for typed assets --
        // so the manifest's type is synthesized. The block key must be the type's dotted head,
        // which is how the codec locates it.
        var rom = MakeFakeRom(0x1000);
        var service = MakeService(rom);
        var region = MakeRegion(startPc: 0x100, lengthBytes: 0x40, RegionExportType.Binary);

        service.ExportRegion(region, tempDir);

        var json = File.ReadAllText(Path.Combine(tempDir, "gfx", "test_asset.json"));
        var man = JsonDocument.Parse(json).RootElement;

        man.GetProperty("name").GetString().Should().Be("gfx/test_asset");
        man.GetProperty("type").GetString().Should().Be("raw.bin");
        man.GetProperty("raw").GetProperty("ext").GetString().Should().Be(".bin");
        man.GetProperty("generated_by").GetString().Should().Be("DiztinGUIsh");

        var source = man.GetProperty("source");
        source.GetProperty("rom_offset").GetString().Should().Be("0x100");
        source.GetProperty("length").GetInt32().Should().Be(0x40);
        source.GetProperty("snes_addr").GetString().Should().Be("0xC00100");
        RegionAssetUtil.Sha256Hex(rom[0x100..0x140])
            .Should().Be(source.GetProperty("source_sha256").GetString());

        // key order is load-bearing for the byte-identity gate; the raw type gets the same
        // envelope every other asset does.
        man.EnumerateObject().Select(p => p.Name)
            .Should().Equal("name", "type", "source", "raw", "generated_by");
    }

    [Fact]
    public void ExplicitRawTypeOnAnAssetRegionRoutesToTheSameExporter()
    {
        // The type can also be named outright on a typed asset region -- it must produce exactly
        // what the Binary export type produces, or the two spellings would drift.
        var service = MakeService(MakeFakeRom(0x1000));
        var region = MakeRegion(0x100, 0x40, RegionExportType.Asset, "raw.bin");

        service.ExportRegion(region, tempDir).Should().Be("incbin \"build/assets/gfx/test_asset.bin\"");

        var man = JsonDocument.Parse(File.ReadAllText(Path.Combine(tempDir, "gfx", "test_asset.json")))
            .RootElement;
        man.GetProperty("type").GetString().Should().Be("raw.bin");
        man.GetProperty("raw").GetProperty("ext").GetString().Should().Be(".bin");
    }

    [Fact]
    public void ExportRequestCarriesNoManifestRefPrefix()
    {
        // Nothing incbin's out of the manifest tree any more -- every asset's payload comes from
        // the build tier. A prefix pointing at the generated tree would only invite a new
        // exporter to write game bytes beside the manifests again.
        typeof(RegionAssetExportRequest).GetProperty("ManifestRefPrefix").Should().BeNull();
    }

    [Fact]
    public void GfxManifestDescribesTheBytesAccurately()
    {
        var rom = MakeFakeRom(0x1000);
        var service = MakeService(rom);
        var region = MakeRegion(0x200, 64, RegionExportType.Asset, "gfx.snes.2bpp");

        service.ExportRegion(region, tempDir);

        var json = File.ReadAllText(Path.Combine(tempDir, "gfx", "test_asset.json"));
        var man = JsonDocument.Parse(json).RootElement;

        man.GetProperty("name").GetString().Should().Be("gfx/test_asset");
        man.GetProperty("type").GetString().Should().Be("gfx.snes.2bpp");

        var gfx = man.GetProperty("gfx");
        gfx.GetProperty("bpp").GetInt32().Should().Be(2);
        gfx.GetProperty("tiles").GetInt32().Should().Be(4);
        gfx.GetProperty("plane_order").GetString().Should().Be("snes-interleaved-pairs");

        var source = man.GetProperty("source");
        source.GetProperty("length").GetInt32().Should().Be(64);
        source.GetProperty("rom_offset").GetString().Should().Be("0x200");

        // the sha must be of the actual bytes -- this is the oracle the build later checks
        // against, so if it's ever computed over the wrong buffer everything downstream lies.
        RegionAssetUtil.Sha256Hex(rom[0x200..0x240])
            .Should().Be(source.GetProperty("source_sha256").GetString());
    }

    [Fact]
    public void OmittedVersionMeansLatestSoManifestHasNoVerField()
    {
        var service = MakeService(MakeFakeRom(0x1000));
        var region = MakeRegion(0x200, 64, RegionExportType.Asset, "gfx.snes.2bpp");
        region.AssetVersion = null;

        service.ExportRegion(region, tempDir);

        var json = File.ReadAllText(Path.Combine(tempDir, "gfx", "test_asset.json"));
        JsonDocument.Parse(json).RootElement.TryGetProperty("ver", out _).Should().BeFalse();
    }

    [Fact]
    public void ExplicitVersionIsPinnedInManifest()
    {
        var service = MakeService(MakeFakeRom(0x1000));
        var region = MakeRegion(0x200, 64, RegionExportType.Asset, "gfx.snes.2bpp");
        region.AssetVersion = "v1";

        service.ExportRegion(region, tempDir);

        var json = File.ReadAllText(Path.Combine(tempDir, "gfx", "test_asset.json"));
        JsonDocument.Parse(json).RootElement.GetProperty("ver").GetString().Should().Be("v1");
    }

    [Fact]
    public void PartialTileIsRejectedRatherThanTruncated()
    {
        var service = MakeService(MakeFakeRom(0x1000));
        // 40 bytes is 2.5 tiles at 2bpp -- silently rounding would corrupt the image
        var region = MakeRegion(0x200, 40, RegionExportType.Asset, "gfx.snes.2bpp");

        var act = () => service.ExportRegion(region, tempDir);
        act.Should().Throw<InvalidOperationException>().WithMessage("*whole number of 2bpp tiles*");
    }

    [Fact]
    public void UnknownAssetTypeIsRejected()
    {
        var service = MakeService(MakeFakeRom(0x1000));
        var region = MakeRegion(0x200, 64, RegionExportType.Asset, "gfx.snes.3bpp");

        var act = () => service.ExportRegion(region, tempDir);
        act.Should().Throw<InvalidOperationException>().WithMessage("*not a supported SNES graphics type*");
    }

    [Fact]
    public void RegionRunningPastEndOfRomIsRejected()
    {
        var service = MakeService(MakeFakeRom(0x100));
        // starts inside the ROM but runs off the end (0xC0 + 0x80 = 0x140 > 0x100)
        var region = MakeRegion(0xC0, 0x80, RegionExportType.Binary);

        var act = () => service.ExportRegion(region, tempDir);
        act.Should().Throw<InvalidOperationException>().WithMessage("*past the end of the ROM*");
    }

    [Fact]
    public void RegionNameIsUsedWhenAssetNameIsBlank()
    {
        var service = MakeService(MakeFakeRom(0x1000));
        var region = MakeRegion(0x200, 64, RegionExportType.Binary);
        region.AssetName = null;

        var directive = service.ExportRegion(region, tempDir);

        directive.Should().Be("incbin \"build/assets/test_region.bin\"");
        File.Exists(Path.Combine(tempDir, "test_region.json")).Should().BeTrue();
    }
}

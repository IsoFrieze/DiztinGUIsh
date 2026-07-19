using System;
using System.IO;
using System.Text.Json;
using Diz.Core.Interfaces;
using Diz.Core.model.snes;
using Diz.LogWriter.assets;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests;

/// <summary>
/// Tests for exporting a region's bytes as a standalone asset (.bin + manifest).
///
/// The point of these is byte-identity: whatever we write out must be exactly the bytes
/// that were in the ROM, and the manifest must describe them accurately enough that the
/// external codec tool can rebuild them. A test that only checked "a file appeared" would
/// pass just as happily with mangled bytes, so every test here asserts on content.
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
            EndSnesAddress = 0xC00000 + startPc + lengthBytes,
            ExportType = type,
            AssetType = assetType,
            AssetName = "gfx/test_asset",
        };

    // ------------------------------------------------------------------

    [Fact]
    public void AssetsGoToTheProjectRootAndIncbinResolvesFromTheAsmDir()
    {
        // The .asm lives in <project>/generated, so the incbin must walk back up. If it
        // didn't, asar would look for generated/build/assets/... and the build would break.
        var rom = MakeFakeRom(0x1000);
        var service = MakeService(rom);
        var region = MakeRegion(0x200, 64, RegionExportType.Asset, "gfx.snes.2bpp");

        var directive = service.ExportRegion(region, tempDir, asmToProjectRootPrefix: "..");

        directive.Should().Be("incbin \"../build/assets/gfx/test_asset.bin\"");

        // Asset SOURCE must land at the project root, NOT under the generated tree --
        // that directory is rewritten on every export, so a PNG there would be destroyed.
        File.Exists(Path.Combine(tempDir, "assets", "src", "gfx", "test_asset.bin"))
            .Should().BeTrue();
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
    public void BinaryExportWritesExactRomBytesAndEmitsIncbin()
    {
        var rom = MakeFakeRom(0x1000);
        var service = MakeService(rom);
        var region = MakeRegion(startPc: 0x100, lengthBytes: 0x40, RegionExportType.Binary);

        var directive = service.ExportRegion(region, tempDir);

        // incbin points at the BUILD output, not the source-layer seed .bin. If it pointed at
        // the seed, edits to the PNG would compile to a file nothing reads and the ROM would
        // silently keep the original bytes.
        directive.Should().Be("incbin \"build/assets/gfx/test_asset.bin\"");

        var written = File.ReadAllBytes(Path.Combine(tempDir, "assets", "src", "gfx", "test_asset.bin"));
        written.Should().Equal(rom[0x100..0x140]);
    }

    [Fact]
    public void GfxExportWritesExactRomBytes()
    {
        var rom = MakeFakeRom(0x1000);
        var service = MakeService(rom);
        // 4 tiles of 2bpp = 4 * 16 = 64 bytes
        var region = MakeRegion(0x200, 64, RegionExportType.Asset, "gfx.snes.2bpp");

        service.ExportRegion(region, tempDir);

        var written = File.ReadAllBytes(Path.Combine(tempDir, "assets", "src", "gfx", "test_asset.bin"));
        written.Should().Equal(rom[0x200..0x240]);
    }

    [Fact]
    public void GfxManifestDescribesTheBytesAccurately()
    {
        var rom = MakeFakeRom(0x1000);
        var service = MakeService(rom);
        var region = MakeRegion(0x200, 64, RegionExportType.Asset, "gfx.snes.2bpp");

        service.ExportRegion(region, tempDir);

        var json = File.ReadAllText(Path.Combine(tempDir, "assets", "src", "gfx", "test_asset.json"));
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

        var json = File.ReadAllText(Path.Combine(tempDir, "assets", "src", "gfx", "test_asset.json"));
        JsonDocument.Parse(json).RootElement.TryGetProperty("ver", out _).Should().BeFalse();
    }

    [Fact]
    public void ExplicitVersionIsPinnedInManifest()
    {
        var service = MakeService(MakeFakeRom(0x1000));
        var region = MakeRegion(0x200, 64, RegionExportType.Asset, "gfx.snes.2bpp");
        region.AssetVersion = "v1";

        service.ExportRegion(region, tempDir);

        var json = File.ReadAllText(Path.Combine(tempDir, "assets", "src", "gfx", "test_asset.json"));
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
        File.Exists(Path.Combine(tempDir, "assets", "src", "test_region.bin")).Should().BeTrue();
    }
}

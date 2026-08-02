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
/// Pins the real BrrRegionAssetExporter. Proves the audio.* prefix routes to it, %9 BRR-block
/// validation fires (pass/fail), and the manifest it writes matches what the vendored
/// binpack.py round-trip expects: an `incbin` of the compiled `build/assets/audio/*.bin`, and
/// an `audio` block recording the editable `.brr` extension.
/// </summary>
public class BrrRegionAssetExporterTests : IDisposable
{
    private readonly string tempDir;

    public BrrRegionAssetExporterTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "diz-brr-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(tempDir, recursive: true); }
        catch (IOException) { /* best-effort cleanup */ }
    }

    // ---- test doubles (same minimal shape RegionAssetPrefixDispatchTests uses) --------------

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

    private static byte[] MakeFakeRom(int size)
    {
        var rom = new byte[size];
        for (var i = 0; i < size; ++i)
            rom[i] = (byte)(i * 7 + 13);
        return rom;
    }

    private static Region BrrRegion(int startPc, int lengthBytes, string assetName = "audio/AudioBRR_00") =>
        new()
        {
            RegionName = assetName,
            StartSnesAddress = 0xC00000 + startPc,
            EndSnesAddress = 0xC00000 + startPc + lengthBytes - 1,
            ExportType = RegionExportType.Asset,
            AssetType = "audio.snes.brr",
            AssetName = assetName,
        };

    private RegionAssetExportService MakeService(byte[] rom) =>
        new(new FakeByteSource(rom), new FakeAddressConverter(),
            [new BinaryRegionAssetExporter(), new GfxRegionAssetExporter(), new BrrRegionAssetExporter()]);

    // ----------------------------------------------------------------------------------------

    [Fact]
    public void AudioPrefixRoutesToTheBrrExporter()
    {
        var rom = MakeFakeRom(0x1000);
        var service = MakeService(rom);

        // 63 bytes = 7 whole BRR blocks
        var directive = service.ExportRegion(BrrRegion(0x400, 63), tempDir).AsmDirective;

        // CompiledExtension is ".bin", NOT ".brr": the assembler incbin's the compiled build
        // output, and binpack extracts/compiles the editable .brr on either side of it.
        directive.Should().Be("incbin \"build/assets/audio/AudioBRR_00.bin\"");
        File.Exists(Path.Combine(tempDir, "audio", "AudioBRR_00.json")).Should().BeTrue();

        // the manifest is the only thing written -- the sample stays in the ROM.
        File.Exists(Path.Combine(tempDir, "audio", "AudioBRR_00.bin")).Should().BeFalse();

        // and the manifest's hash is over the real ROM bytes: that is what `extract` checks
        // the ROM against, so if it were computed over the wrong buffer nothing downstream
        // would catch it.
        var source = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(tempDir, "audio", "AudioBRR_00.json"))).RootElement.GetProperty("source");
        source.GetProperty("source_sha256").GetString()
            .Should().Be(RegionAssetUtil.Sha256Hex(rom[0x400..(0x400 + 63)]));
    }

    [Fact]
    public void ManifestRecordsAudioBlockWithBrrEditableExtension()
    {
        var rom = MakeFakeRom(0x1000);
        var service = MakeService(rom);

        service.ExportRegion(BrrRegion(0x400, 63), tempDir);

        var json = File.ReadAllText(Path.Combine(tempDir, "audio", "AudioBRR_00.json"));
        var man = JsonDocument.Parse(json).RootElement;

        man.GetProperty("type").GetString().Should().Be("audio.snes.brr");
        // binpack resolves the editable payload extension from here (audio.ext), so no --ext
        // is needed in the ninja rule.
        man.GetProperty("audio").GetProperty("ext").GetString().Should().Be(".brr");
        man.GetProperty("generated_by").GetString().Should().Be("DiztinGUIsh");

        // key order is load-bearing for byte-identity of the tracked manifest.
        var keys = new System.Collections.Generic.List<string>();
        foreach (var p in man.EnumerateObject())
            keys.Add(p.Name);
        keys.Should().Equal("name", "type", "source", "audio", "generated_by");
    }

    [Fact]
    public void ValidationAcceptsWholeBrrBlockLengths()
    {
        var service = MakeService(MakeFakeRom(0x1000));
        // 9, 18, 900 bytes are all whole numbers of 9-byte blocks.
        foreach (var len in new[] { 9, 18, 900 })
        {
            var act = () => service.ExportRegion(BrrRegion(0x400, len, $"audio/ok_{len}"), tempDir);
            act.Should().NotThrow();
        }
    }

    [Fact]
    public void ValidationRejectsPartialBrrBlockNamingTheRegion()
    {
        var service = MakeService(MakeFakeRom(0x1000));

        // 26 bytes is 2 blocks + 8 leftover -- not a whole number of 9-byte blocks.
        var act = () => service.ExportRegion(BrrRegion(0x400, 26, "audio/AudioBRR_bad"), tempDir);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*audio/AudioBRR_bad*")
            .WithMessage("*9-byte BRR blocks*");

        // it must fail BEFORE writing a half-formed asset tree.
        File.Exists(Path.Combine(tempDir, "audio", "AudioBRR_bad.json")).Should().BeFalse();
    }

    [Fact]
    public void GfxExporterDoesNotClaimAudioTypes()
    {
        // both derive from the same base; the prefix is the only discriminator.
        new GfxRegionAssetExporter().CanExport(BrrRegion(0, 9)).Should().BeFalse();
        new BrrRegionAssetExporter().CanExport(BrrRegion(0, 9)).Should().BeTrue();
    }
}

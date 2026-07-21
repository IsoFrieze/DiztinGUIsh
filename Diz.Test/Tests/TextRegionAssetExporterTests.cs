using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Diz.Core.Interfaces;
using Diz.Core.model.snes;
using Diz.LogWriter.assets;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests;

/// <summary>
/// Pins the real TextRegionAssetExporter. Proves the text.* prefix routes to it, the
/// fixed-width record validation fires (pass/fail), the type-specific fields ride in
/// Region.AssetOptions, and the payload + manifest it writes match what the vendored
/// textpack.py round-trip expects: a verbatim `.bin` SEED, an `incbin` of the compiled
/// `build/assets/text/*.bin`, and a `text` block ({tbl, count, record_width, pad, [tokens]})
/// in the exact key order textpack's load_manifest reads.
/// </summary>
public class TextRegionAssetExporterTests : IDisposable
{
    private readonly string tempDir;

    public TextRegionAssetExporterTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "diz-text-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(tempDir, recursive: true); }
        catch (IOException) { /* best-effort cleanup */ }
    }

    // ---- test doubles (same minimal shape the other asset-exporter tests use) ----------------

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

    // The type-specific fields live in AssetOptions, exactly as a project author would set them.
    private const string OptionsWithTokens =
        "{\"tbl\": \"text/ct_8px.tbl\", \"record_width\": 11, \"pad\": \"0xEF\", " +
        "\"tokens\": {\"Blade\": \"0x20\", \"Shield\": \"0x2E\"}}";

    private const string OptionsNoTokens =
        "{\"tbl\": \"text/ct_8px.tbl\", \"record_width\": 11, \"pad\": \"0xEF\"}";

    private static Region TextRegion(
        int startPc, int lengthBytes, string assetName = "text/item_names",
        string assetOptions = OptionsWithTokens, string assetType = "text.ct.mapped") =>
        new()
        {
            RegionName = assetName,
            StartSnesAddress = 0xC00000 + startPc,
            EndSnesAddress = 0xC00000 + startPc + lengthBytes - 1,
            ExportType = RegionExportType.Asset,
            AssetType = assetType,
            AssetName = assetName,
            AssetOptions = assetOptions,
        };

    private RegionAssetExportService MakeService(byte[] rom) =>
        new(new FakeByteSource(rom), new FakeAddressConverter(),
            [
                new BinaryRegionAssetExporter(), new GfxRegionAssetExporter(),
                new BrrRegionAssetExporter(), new TextRegionAssetExporter(),
            ]);

    // ----------------------------------------------------------------------------------------

    [Fact]
    public void TextPrefixRoutesToTheTextExporter()
    {
        var rom = MakeFakeRom(0x1000);
        var service = MakeService(rom);

        // 33 bytes = 3 records of width 11
        var directive = service.ExportRegion(TextRegion(0x400, 33), tempDir);

        // FileExtension is ".bin" (the seed + incbin target), NOT ".yaml": the assembler incbin's
        // the compiled build output, and textpack turns the seed into the editable .yaml.
        directive.Should().Be("incbin \"build/assets/text/item_names.bin\"");
        File.Exists(Path.Combine(tempDir, "assets", "src", "text", "item_names.bin")).Should().BeTrue();
        File.Exists(Path.Combine(tempDir, "assets", "src", "text", "item_names.json")).Should().BeTrue();

        // the seed is the exact ROM bytes, written verbatim by the base.
        File.ReadAllBytes(Path.Combine(tempDir, "assets", "src", "text", "item_names.bin"))
            .Should().Equal(rom[0x400..(0x400 + 33)]);
    }

    [Fact]
    public void ManifestRecordsTextBlockInTextpackKeyOrder()
    {
        var rom = MakeFakeRom(0x1000);
        var service = MakeService(rom);

        service.ExportRegion(TextRegion(0x400, 33), tempDir);

        var json = File.ReadAllText(Path.Combine(tempDir, "assets", "src", "text", "item_names.json"));
        var man = JsonDocument.Parse(json).RootElement;

        man.GetProperty("type").GetString().Should().Be("text.ct.mapped");
        man.GetProperty("generated_by").GetString().Should().Be("DiztinGUIsh");

        var text = man.GetProperty("text");
        text.GetProperty("tbl").GetString().Should().Be("text/ct_8px.tbl");
        text.GetProperty("count").GetInt32().Should().Be(3);           // computed: 33 / 11
        text.GetProperty("record_width").GetInt32().Should().Be(11);
        text.GetProperty("pad").GetString().Should().Be("0xEF");
        text.GetProperty("tokens").GetProperty("Blade").GetString().Should().Be("0x20");
        text.GetProperty("tokens").GetProperty("Shield").GetString().Should().Be("0x2E");

        // the text block's key order is load-bearing for a byte-stable tracked manifest.
        TextBlockKeys(text).Should().Equal("tbl", "count", "record_width", "pad", "tokens");

        // top-level key order (no AssetVersion set => no "ver" key).
        TopLevelKeys(man).Should().Equal("name", "type", "source", "text", "generated_by");
    }

    [Fact]
    public void TokensAreOptionalAndOmittedWhenAbsent()
    {
        var rom = MakeFakeRom(0x1000);
        var service = MakeService(rom);

        service.ExportRegion(TextRegion(0x400, 22, "text/no_tokens", OptionsNoTokens), tempDir);

        var json = File.ReadAllText(Path.Combine(tempDir, "assets", "src", "text", "no_tokens.json"));
        var text = JsonDocument.Parse(json).RootElement.GetProperty("text");

        text.TryGetProperty("tokens", out _).Should().BeFalse();
        TextBlockKeys(text).Should().Equal("tbl", "count", "record_width", "pad");
    }

    [Fact]
    public void ValidationAcceptsWholeRecordLengths()
    {
        var service = MakeService(MakeFakeRom(0x1000));
        // 11, 22, 110 bytes are all whole numbers of 11-byte records.
        foreach (var len in new[] { 11, 22, 110 })
        {
            var act = () => service.ExportRegion(TextRegion(0x400, len, $"text/ok_{len}"), tempDir);
            act.Should().NotThrow();
        }
    }

    [Fact]
    public void ValidationRejectsPartialRecordNamingTheRegion()
    {
        var service = MakeService(MakeFakeRom(0x1000));

        // 30 bytes is 2 records + 8 leftover -- not a whole number of 11-byte records.
        var act = () => service.ExportRegion(TextRegion(0x400, 30, "text/ragged"), tempDir);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*text/ragged*")
            .WithMessage("*11-byte records*");

        // it must fail BEFORE writing a half-formed asset tree.
        File.Exists(Path.Combine(tempDir, "assets", "src", "text", "ragged.bin")).Should().BeFalse();
    }

    [Fact]
    public void MissingAssetOptionsFailsLoudly()
    {
        var service = MakeService(MakeFakeRom(0x1000));

        // a text asset can't be described without tbl/record_width/pad -- no silent default.
        var act = () => service.ExportRegion(TextRegion(0x400, 33, "text/no_opts", assetOptions: null), tempDir);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*text/no_opts*")
            .WithMessage("*Asset Options*");
    }

    [Fact]
    public void OtherExportersDoNotClaimTextTypes()
    {
        // all derive from the same base; the prefix is the only discriminator.
        var region = TextRegion(0, 11);
        new GfxRegionAssetExporter().CanExport(region).Should().BeFalse();
        new BrrRegionAssetExporter().CanExport(region).Should().BeFalse();
        new TextRegionAssetExporter().CanExport(region).Should().BeTrue();
    }

    private static List<string> TopLevelKeys(JsonElement obj)
    {
        var keys = new List<string>();
        foreach (var p in obj.EnumerateObject())
            keys.Add(p.Name);
        return keys;
    }

    private static List<string> TextBlockKeys(JsonElement text) => TopLevelKeys(text);
}

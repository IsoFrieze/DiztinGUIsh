using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Diz.Core.export;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.Cpu._65816;
using Diz.LogWriter;
using Diz.LogWriter.util;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.LogCreatorTests;

/// <summary>
/// What a region exported as an asset looks like in the .asm.
///
/// Two things are under test here, and they're coupled:
///
///  1. the header: one line, machine-parseable JSON after "; inc: ", replacing the older
///     four-line "; --> Included asset:" block. The `incsrc` path (a nested file-producing
///     region) keeps that older block verbatim -- byte-identical re-export depends on it.
///
///  2. the labels bracketing the `incbin`: the region's start address gets its name emitted
///     INLINE, so pointers into it render as that name, plus a text-only "__END" marker so
///     table math can be written symbolically. Emitting the name inline and suppressing its
///     equate in labels.asm are two halves of one operation -- doing only one produces
///     assembly that won't build -- so every case below checks both halves together.
/// </summary>
public class AssetRegionHeaderAndLabelTests
{
    // HiROM: PC 0x0000 -> $C0:0000, linear across banks.
    private const int SnesBase = 0xC00000;

    private const int AssetPcOffset = 0x20;
    private const int AssetLength = 0x10;
    private const int AssetSnesAddress = SnesBase + AssetPcOffset;

    private const string AssetRegionName = "myasset";
    private const string GeneratedAssetLabel = "ASSET_" + AssetRegionName;
    private const string AuthoredLabel = "overworld_gfx";

    private const string ExpectedHeaderLine =
        "; inc: {\"name\":\"myasset\",\"type\":\"binary\",\"off\":\"$000020\",\"snes\":\"$C00020\",\"len\":16}";

    private sealed class ExportedFiles
    {
        public required string AllAsm { get; init; }
        public required string LabelsAsm { get; init; }
    }

    private static Data BuildFixture(bool addAuthoredLabelAtAssetStart, bool addNestedIncSrcRegion)
    {
        const int romSize = 0x40;

        var data = new Data
        {
            RomMapMode = RomMapMode.HiRom,
            RomSpeed = RomSpeed.FastRom,
        };

        var romBytes = new RomBytes();
        for (var i = 0; i < romSize; i++)
            romBytes.Add(new RomByte { Rom = (byte)i, TypeFlag = FlagType.Data8Bit });
        data.RomBytes = romBytes;
        data.Apis.AddIfDoesntExist(new SnesApi(data));

        // the asset region: bytes replaced by an `incbin`, not emitted inline.
        data.Regions.Add(new Region
        {
            RegionName = AssetRegionName,
            AssetName = AssetRegionName,
            StartSnesAddress = AssetSnesAddress,
            EndSnesAddress = AssetSnesAddress + AssetLength - 1,
            ExportType = RegionExportType.Binary,
            Priority = 0,
        });

        if (addNestedIncSrcRegion)
        {
            // an ordinary file-producing region nested inside the synthesized bank region, so
            // one export exercises the `incsrc` path alongside the asset path.
            data.Regions.Add(new Region
            {
                RegionName = "subregion",
                StartSnesAddress = SnesBase,
                EndSnesAddress = SnesBase + 0x0F,
                ExportSeparateFile = true,
                Priority = 0,
                ExportType = RegionExportType.Assembly,
            });
        }

        if (addAuthoredLabelAtAssetStart)
            data.Labels.AddLabel(AssetSnesAddress, new Label { Name = AuthoredLabel });

        return data;
    }

    private static ExportedFiles ExportToTempDir(Data data, bool generateAssetLabels = true)
    {
        var outDir = Path.Combine(Path.GetTempPath(), "diz-test-assetlabels-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        try
        {
            // asset export needs real files on disk (OutputToString mode has no output dir and
            // switches asset export off entirely), so this exports for real and reads it back.
            var logCreator = new LogCreator
            {
                Data = new LogCreatorByteSource(data),
                Settings = new LogWriterSettings
                {
                    Structure = LogWriterSettings.FormatStructure.OneBankPerFile,
                    OutputToString = false,
                    FileOrFolderOutPath = outDir,
                    GenerateAssetLabels = generateAssetLabels,
                }
            };

            var result = logCreator.CreateLog();

            result.Success.Should().BeTrue("export must not fatally fail; msg: {0}", result.FatalErrorMsg ?? "(none)");
            result.ErrorCount.Should().Be(0, "this fixture has no conflicting names or bad regions");

            var labelsAsmPath = Path.Combine(outDir, "labels.asm");

            return new ExportedFiles
            {
                AllAsm = string.Join("\n",
                    Directory.GetFiles(outDir, "*.asm")
                        .Where(f => !string.Equals(Path.GetFileName(f), "labels.asm", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(f => f)
                        .Select(File.ReadAllText)),
                LabelsAsm = File.Exists(labelsAsmPath) ? File.ReadAllText(labelsAsmPath) : "",
            };
        }
        finally
        {
            // best-effort: the export's stream cache can still hold a handle here (same reason
            // BankCrossCheckEmissionTests / BuildFileGeneratorTests swallow this).
            try { Directory.Delete(outDir, recursive: true); } catch (IOException) { }
        }
    }

    private static IEnumerable<string> NonEmptyLines(string text) =>
        text.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0);

    [Fact]
    public void AssetRegionHeaderIsOneLineOfParseableJson()
    {
        var exported = ExportToTempDir(BuildFixture(addAuthoredLabelAtAssetStart: false, addNestedIncSrcRegion: false));

        NonEmptyLines(exported.AllAsm).Should().Contain(ExpectedHeaderLine);

        // the payload after the prefix must be strict JSON, with len as a NUMBER
        var jsonPayload = NonEmptyLines(exported.AllAsm).Single(l => l.StartsWith("; inc: "))["; inc: ".Length..];
        using var parsed = JsonDocument.Parse(jsonPayload);
        var root = parsed.RootElement;

        root.GetProperty("name").GetString().Should().Be(AssetRegionName);
        root.GetProperty("type").GetString().Should().Be("binary");
        root.GetProperty("off").GetString().Should().Be("$000020");
        root.GetProperty("snes").GetString().Should().Be("$C00020");
        root.GetProperty("len").ValueKind.Should().Be(JsonValueKind.Number);
        root.GetProperty("len").GetInt32().Should().Be(AssetLength);
    }

    [Fact]
    public void AssetPathDropsTheOldMultiLineHeaderButIncSrcPathKeepsIt()
    {
        var exported = ExportToTempDir(BuildFixture(addAuthoredLabelAtAssetStart: false, addNestedIncSrcRegion: true));

        exported.AllAsm.Should().NotContain("; --> Included asset:",
            "the asset path emits the compact one-line header instead");
        exported.AllAsm.Should().NotContain("; --> asset defined size");

        // the incsrc path is unchanged, verbatim.
        exported.AllAsm.Should().Contain("; --> Included region: subregion");
        exported.AllAsm.Should().Contain("; --> Included from offset:       $000000");
        exported.AllAsm.Should().Contain("; --> Included from SNES address: $C00000");
        exported.AllAsm.Should().Contain("; --> region defined size (bytes) [actual may differ]: 16");
    }

    [Fact]
    public void UnlabeledAssetRegionGetsAGeneratedNamePairAndNoEquate()
    {
        var exported = ExportToTempDir(BuildFixture(addAuthoredLabelAtAssetStart: false, addNestedIncSrcRegion: false));

        var lines = NonEmptyLines(exported.AllAsm).ToList();
        lines.Should().Contain($"{GeneratedAssetLabel}:");
        lines.Should().Contain($"{GeneratedAssetLabel}__END:");

        // the start label goes immediately before the incbin, the end marker immediately after.
        var startIndex = lines.IndexOf($"{GeneratedAssetLabel}:");
        lines[startIndex + 1].Should().StartWith("incbin ");
        lines[startIndex + 2].Should().Be($"{GeneratedAssetLabel}__END:");

        // emitted inline => must NOT also be defined as an equate, or the assembler sees it twice.
        exported.LabelsAsm.Should().NotContain(GeneratedAssetLabel);
    }

    [Fact]
    public void AuthoredLabelAtAssetStartIsUsedAsIsAndNotEquated()
    {
        var exported = ExportToTempDir(BuildFixture(addAuthoredLabelAtAssetStart: true, addNestedIncSrcRegion: false));

        var lines = NonEmptyLines(exported.AllAsm).ToList();
        lines.Should().Contain($"{AuthoredLabel}:");
        lines.Should().Contain($"{AuthoredLabel}__END:");

        // a hand-authored name wins outright: nothing generated is emitted for this region.
        exported.AllAsm.Should().NotContain(GeneratedAssetLabel);

        exported.LabelsAsm.Should().NotContain(AuthoredLabel);
    }

    [Fact]
    public void AssetLabelsOffRestoresTheUnbracketedOutputAndTheEquate()
    {
        var exported = ExportToTempDir(
            BuildFixture(addAuthoredLabelAtAssetStart: true, addNestedIncSrcRegion: false),
            generateAssetLabels: false);

        var lines = NonEmptyLines(exported.AllAsm).ToList();
        lines.Should().NotContain($"{AuthoredLabel}:");
        lines.Should().NotContain($"{AuthoredLabel}__END:");
        exported.AllAsm.Should().NotContain(GeneratedAssetLabel);

        // nothing defined the label inline, so labels.asm must still equate it.
        exported.LabelsAsm.Should().Contain($"{AuthoredLabel} = $C00020");

        // the compact header is not part of the switch -- it is always emitted.
        NonEmptyLines(exported.AllAsm).Should().Contain(ExpectedHeaderLine);
    }
}

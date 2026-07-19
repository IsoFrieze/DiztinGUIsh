using System;
using System.IO;
using System.Linq;
using Diz.Core.export;
using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.Core.Interfaces;
using Diz.Cpu._65816;
using Diz.LogWriter;
using Diz.LogWriter.util;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.LogCreatorTests;

/// <summary>
/// A file-producing region whose extent crosses a SNES bank boundary must emit asar's
/// `check bankcross off` at the top of its own file and `check bankcross on` at the end --
/// SCOPED, so the rest of the export keeps the check. This is what lets a bank-crossing region
/// (e.g. a large audio-data region spanning several HiROM banks under HiROM's linear mapping)
/// assemble without asar's E5032 while still producing byte-identical output.
///
/// Non-crossing regions must emit NEITHER directive, or the suppression would silently spread.
///
/// Needs a real multi-file export (the directive lives in the per-region file), so this writes
/// to a throwaway temp dir and reads the files back -- same harness as RegionIncSrcPlacementTests.
/// </summary>
public class BankCrossCheckEmissionTests
{
    private static Data BuildHiRomFixture(int romSize, params Region[] regions)
    {
        var data = new Data
        {
            RomMapMode = RomMapMode.HiRom,   // HiROM: PC 0x0000 -> $C0:0000, linear across banks
            RomSpeed = RomSpeed.FastRom,
        };

        var romBytes = new RomBytes();
        for (var i = 0; i < romSize; i++)
            romBytes.Add(new RomByte { Rom = 0x00, TypeFlag = FlagType.Data8Bit });
        data.RomBytes = romBytes;
        data.Apis.AddIfDoesntExist(new SnesApi(data));

        foreach (var r in regions)
            data.Regions.Add(r);

        return data;
    }

    private static string[] ExportAndReadAllAsm(Data data)
    {
        var outDir = Path.Combine(Path.GetTempPath(), "diz-test-bankcross-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        try
        {
            var logCreator = new LogCreator
            {
                Data = new LogCreatorByteSource(data),
                Settings = new LogWriterSettings
                {
                    Structure = LogWriterSettings.FormatStructure.OneBankPerFile,
                    OutputToString = false,
                    FileOrFolderOutPath = outDir,
                }
            };

            var result = logCreator.CreateLog();

            result.Success.Should().BeTrue("export must not fatally fail; msg: {0}", result.FatalErrorMsg ?? "(none)");

            // A sanctioned bank-crossing region must NOT raise the "instruction crossed a bank
            // boundary" diagnostic (the check's own comment says so). This asserts that intent.
            result.ErrorCount.Should().Be(0, "a legitimately bank-crossing region must export cleanly");

            return Directory.GetFiles(outDir, "*.asm").Select(File.ReadAllText).ToArray();
        }
        finally
        {
            // best-effort cleanup: the export's stream cache can still hold a handle when the
            // temp dir is removed (same reason BuildFileGeneratorTests swallows IOException).
            try { Directory.Delete(outDir, recursive: true); } catch (IOException) { }
        }
    }

    private static int Count(string haystack, string needle) =>
        haystack.Split(needle).Length - 1;

    [Fact]
    public void CrossingFileProducingRegionEmitsScopedBankCrossDirectivePair()
    {
        // a file-producing region spanning the C0->C1 seam (PC 0x00000-0x1000F), followed by a
        // non-crossing sibling in C1 -- so the crosser is POPPED (its restore emitted) when the
        // sibling opens, mirroring a bank-crossing region -> next-bank sibling transition.
        var data = BuildHiRomFixture(0x10020,
            new Region
            {
                RegionName = "crosser",
                StartSnesAddress = 0xC00000,
                EndSnesAddress = 0xC1000F,
                ExportSeparateFile = true,
                Priority = 0,
                ExportType = RegionExportType.Assembly,
            },
            new Region
            {
                RegionName = "after",
                StartSnesAddress = 0xC10010,
                EndSnesAddress = 0xC1001F,
                ExportSeparateFile = true,
                Priority = 0,
                ExportType = RegionExportType.Assembly,
            });

        var allAsm = ExportAndReadAllAsm(data);
        var crosser = allAsm.Single(a => a.Contains("check bankcross off"));

        // exactly one off + one on, scoped to this one file, off before on.
        Count(crosser, "check bankcross off").Should().Be(1);
        Count(crosser, "check bankcross on").Should().Be(1);
        crosser.IndexOf("check bankcross off", StringComparison.Ordinal)
            .Should().BeLessThan(crosser.IndexOf("check bankcross on", StringComparison.Ordinal));

        // no OTHER file got the suppression.
        allAsm.Count(a => a.Contains("check bankcross off")).Should().Be(1);
    }

    [Fact]
    public void NonCrossingFileProducingRegionEmitsNoBankCrossDirective()
    {
        // region entirely within bank C0 -> asar has no bank-cross issue, so nothing is emitted.
        var data = BuildHiRomFixture(0x40, new Region
        {
            RegionName = "solo",
            StartSnesAddress = 0xC00000,
            EndSnesAddress = 0xC0003F,
            ExportSeparateFile = true,
            Priority = 0,
            ExportType = RegionExportType.Assembly,
        });

        var allAsm = ExportAndReadAllAsm(data);

        allAsm.Should().NotBeEmpty();
        allAsm.Any(a => a.Contains("check bankcross")).Should()
            .BeFalse("no region crosses a bank, so asar's default check must stay in force everywhere");
    }
}

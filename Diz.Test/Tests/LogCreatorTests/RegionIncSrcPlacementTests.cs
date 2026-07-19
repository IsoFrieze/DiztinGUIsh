using System;
using System.IO;
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
/// Regression fixture for incsrc PLACEMENT (docs/diz/regions-as-partition-plan.md §A.3): a
/// child region's `incsrc` must be written into its PARENT's file, at the child's start
/// offset -- never into a sibling's file.
///
/// The bug this guards against: when one file-producing region ends on the byte immediately
/// before the next sibling begins (contiguous siblings, extremely common -- CT's
/// player_attack_animations / player_tech_animations / item_animations chain in bank CE),
/// SyncRegionStack pops the old region and pushes the new one in the SAME call. The output
/// stream was still pointing at the just-closed sibling's file, so the new sibling's incsrc
/// was "daisy-chained" into the previous sibling's tail instead of landing in the shared
/// parent. asar assembles both shapes to identical bytes, so the byte-identity gate can NOT
/// catch this -- only comparing the .asm text (or this test) can.
///
/// Needs a real multi-file export (incsrc is disabled in single-file string mode), so this
/// writes to a throwaway temp directory and reads the files back.
/// </summary>
public class RegionIncSrcPlacementTests
{
    // Same LoRom/FastRom mapping as LoRomOrgDiscontinuityTests: PC 0x0000 -> $80:8000.
    // Everything stays inside PC 0x00-0x5F / $808000-$80805F, no bank seam involved.
    private const int RomSize = 0x60;

    private static Data BuildFixtureWithContiguousSiblings()
    {
        var data = new Data
        {
            RomMapMode = RomMapMode.LoRom,
            RomSpeed = RomSpeed.FastRom,
        };

        var romBytes = new RomBytes();
        for (var i = 0; i < RomSize; i++)
        {
            romBytes.Add(new RomByte { Rom = 0x00, TypeFlag = FlagType.Data8Bit });
        }
        data.RomBytes = romBytes;
        data.Apis.AddIfDoesntExist(new SnesApi(data));

        // two file-producing siblings, CONTIGUOUS: beta starts on the byte right after
        // alpha's (inclusive, §A.2.2) end. Their shared parent is the synthesized bank_80
        // region ($800000-$80FFFF) that GenerateSyntheticBankRegions adds at export time.
        data.Regions.Add(new Region
        {
            RegionName = "alpha",
            StartSnesAddress = 0x808000,
            EndSnesAddress = 0x80801F,
            ExportSeparateFile = true,
            Priority = 0,
            ExportType = RegionExportType.Assembly,
        });
        data.Regions.Add(new Region
        {
            RegionName = "beta",
            StartSnesAddress = 0x808020,
            EndSnesAddress = 0x80803F,
            ExportSeparateFile = true,
            Priority = 0,
            ExportType = RegionExportType.Assembly,
        });

        return data;
    }

    [Fact]
    public void ContiguousSiblingIncSrcLandsInParentFileNotPreviousSiblingFile()
    {
        var data = BuildFixtureWithContiguousSiblings();

        var outDir = Path.Combine(Path.GetTempPath(), "diz-test-incsrc-" + Guid.NewGuid().ToString("N"));
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
            result.ErrorCount.Should().Be(0);

            var bankAsm = File.ReadAllText(Path.Combine(outDir, "bank_80.asm"));
            var alphaAsm = File.ReadAllText(Path.Combine(outDir, "alpha.asm"));
            var betaAsm = File.ReadAllText(Path.Combine(outDir, "beta.asm"));

            // both children are incsrc'd from their shared parent's file
            bankAsm.Should().Contain("alpha.asm");
            bankAsm.Should().Contain("beta.asm");

            // THE regression assertion: alpha's file must not pull in its sibling. Before the
            // fix, beta's incsrc was appended to alpha.asm because the output stream hadn't
            // switched back to the parent when alpha closed and beta opened at the same offset.
            alphaAsm.Should().NotContain("incsrc");
            betaAsm.Should().NotContain("incsrc");
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }
}

using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.Core.Interfaces;
using Diz.Cpu._65816;
using Diz.Test.Utils;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.LogCreatorTests;

/// <summary>
/// Fixture for the LoRom worked example: a single file-producing region straddling PC
/// 0x7FF0-0x800F. LoRom maps 32 KiB into the UPPER half of each bank, so the SNES address jumps
/// BACKWARD by 0x7FFF exactly at PC 0x8000:
///
///   PC 0x7FFF -> $80:FFFF
///   PC 0x8000 -> $81:8000   (NOT $81:0000)
///
/// This hazard can't occur under HiRom (its mapping is linear), so a HiRom byte-identity gate
/// never exercises this path -- this fixture is the only thing that does.
///
/// The rule under test (LogCreator's AsmCreationInstructions.WriteOutputLinesForRomOffset):
/// emit a real ORG whenever snes(p) != snes(p-1)+1, computed through the same PC->SNES
/// converter used everywhere else, regardless of whether a region boundary is also involved.
/// A naive "one ORG per region start" would silently place the second half of this region at
/// the wrong address (asar would assemble it as if it started at $81:0000).
/// </summary>
public class LoRomOrgDiscontinuityTests
{
    // PC 0x7FF0 -> $80FFF0, PC 0x800F -> $81800F (see header comment above; also independently
    // derived from RomUtil.ConvertPCtoSnes(offset, LoRom, FastRom)).
    private const int RegionStartSnes = 0x80FFF0;
    private const int RegionEndSnes = 0x81800F; // inclusive (last byte IN the region)
    private const int RomSize = 0x8010; // covers PC 0..0x800F

    private static Data BuildLoRomFixture()
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

        // the region under test: a single file-producing region that straddles the LoRom
        // bank seam at PC 0x8000 instead of stopping at it, exactly the case the naive
        // "one ORG per region start" rule gets wrong.
        data.Regions.Add(new Region
        {
            RegionName = "cross_seam",
            StartSnesAddress = RegionStartSnes,
            EndSnesAddress = RegionEndSnes,
            ExportSeparateFile = true,
            Priority = 0,
            ExportType = RegionExportType.Assembly,
        });

        return data;
    }

    [Fact]
    public void OrgReemitsAtTheLoRomBankSeamInsideASingleFileProducingRegion()
    {
        var data = BuildLoRomFixture();

        var result = LogWriterHelper.ExportAssembly(data);

        result.Success.Should().BeTrue(result.FatalErrorMsg);
        result.ErrorCount.Should().Be(0);

        var asm = result.AssemblyOutputStr;

        // sanity: the worked example's own mapping holds for this fixture
        RomUtilConvertsAsExpected();

        // ORG at the very first byte (PC 0, no region covers it here -- falls back to the
        // "first byte ever" case, matching today's always-ORG-on-first-bank-visit behavior)
        asm.Should().Contain("ORG $808000");

        // ORG at the region's own start (PC 0x7FF0 / $80FFF0) -- "unconditionally at region start"
        asm.Should().Contain("ORG $80FFF0");

        // THE critical assertion: ORG re-emits exactly at the backward jump, $81:8000, even
        // though this is the MIDDLE of one continuous file-producing region (no region
        // boundary here at all). Without this, asar would place these bytes as if they
        // started at $81:0000 -- silently wrong output, no error.
        asm.Should().Contain("ORG $818000");

        // regression guard: must NOT show the naive (wrong) placement that a "bank<<16"-style
        // computation would produce for the second half of the region.
        asm.Should().NotContain("ORG $810000");
    }

    private static void RomUtilConvertsAsExpected()
    {
        Diz.Core.util.RomUtil.ConvertPCtoSnes(0x7FF0, RomMapMode.LoRom, RomSpeed.FastRom).Should().Be(0x80FFF0);
        Diz.Core.util.RomUtil.ConvertPCtoSnes(0x7FFF, RomMapMode.LoRom, RomSpeed.FastRom).Should().Be(0x80FFFF);
        Diz.Core.util.RomUtil.ConvertPCtoSnes(0x8000, RomMapMode.LoRom, RomSpeed.FastRom).Should().Be(0x818000);
        Diz.Core.util.RomUtil.ConvertPCtoSnes(0x800F, RomMapMode.LoRom, RomSpeed.FastRom).Should().Be(0x81800F);
    }
}

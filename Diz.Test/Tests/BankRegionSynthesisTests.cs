using System.Collections.Generic;
using System.Linq;
using Diz.Core.Interfaces;
using Diz.Core.model.snes;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests;

/// <summary>
/// Tests for the shared bank-region synthesis logic (docs/diz/regions-as-partition-plan.md
/// §A.5): bank enumeration, EndSnesAddress being inclusive (last byte IN the bank, per §A.2.2),
/// and the skip-if-already-covered rule that reconciles the persisted (migration/import) and
/// in-memory (LogWriter export-time) call sites so they never both add a region for the same
/// bank -- see the "As built -- two deviations to reconcile" note at the end of §A.4.
/// </summary>
public class BankRegionSynthesisTests
{
    private static Region MakeRegion(
        string name, int start, int end, bool exportSeparateFile = true) =>
        new()
        {
            RegionName = name,
            StartSnesAddress = start,
            EndSnesAddress = end,
            ExportSeparateFile = exportSeparateFile,
            Priority = 0,
        };

    // HiRom-shaped: PC offset maps 1:1 into SNES address, bank = (offset >> 16).
    private static int HiRomConvertPcToSnes(int offset) => 0xC00000 + offset;

    [Fact]
    public void OneRegionPerBankIsSynthesizedForAFreshRom()
    {
        const int bankSize = 0x10000;
        var romSize = bankSize * 3; // banks C0, C1, C2

        var result = BankRegionSynthesis.SynthesizeMissingBankRegions(
            existingRegions: [], romSize, bankSize, HiRomConvertPcToSnes);

        result.Should().HaveCount(3);
        result.Select(r => r.RegionName).Should().Equal("bank_C0", "bank_C1", "bank_C2");
    }

    [Fact]
    public void EndSnesAddressIsTheLastByteInclusive()
    {
        const int bankSize = 0x10000;

        var result = BankRegionSynthesis.SynthesizeMissingBankRegions(
            existingRegions: [], romSize: bankSize, bankSize, HiRomConvertPcToSnes);

        var bank = result.Single();
        bank.StartSnesAddress.Should().Be(0xC00000);
        bank.EndSnesAddress.Should().Be(0xC0FFFF); // last byte IN the bank, not one past it
    }

    [Fact]
    public void SynthesizedRegionsAreFileProducingWithPriorityZero()
    {
        const int bankSize = 0x10000;

        var bank = BankRegionSynthesis.SynthesizeMissingBankRegions(
            existingRegions: [], romSize: bankSize, bankSize, HiRomConvertPcToSnes).Single();

        bank.ExportSeparateFile.Should().BeTrue();
        bank.Priority.Should().Be(0);
        bank.IsFileProducingRegion().Should().BeTrue();
    }

    [Fact]
    public void BankExactlyCoveredByAnExistingRegionOfTheSameExtentIsSkipped()
    {
        const int bankSize = 0x10000;
        var romSize = bankSize * 2; // banks C0, C1

        // mirrors CT's hand-authored "BankC0 - location"
        IReadOnlyList<IRegion> existing = [MakeRegion("BankC0 - location", 0xC00000, 0xC0FFFF)];

        var result = BankRegionSynthesis.SynthesizeMissingBankRegions(
            existing, romSize, bankSize, HiRomConvertPcToSnes);

        // bank C0 already covered -- only C1 gets synthesized
        result.Should().ContainSingle();
        result[0].RegionName.Should().Be("bank_C1");
    }

    [Fact]
    public void RerunningSynthesisAfterApplyingResultsIsIdempotent()
    {
        const int bankSize = 0x10000;
        var romSize = bankSize * 2;

        var firstPass = BankRegionSynthesis.SynthesizeMissingBankRegions(
            existingRegions: [], romSize, bankSize, HiRomConvertPcToSnes);
        firstPass.Should().HaveCount(2);

        // simulate persisting firstPass (e.g. the migration having run), then re-running
        // synthesis again (e.g. LogWriter's export-time pass, or a second migration run)
        var secondPass = BankRegionSynthesis.SynthesizeMissingBankRegions(
            firstPass, romSize, bankSize, HiRomConvertPcToSnes);

        secondPass.Should().BeEmpty("everything is already covered by exact-match persisted regions");
    }

    [Fact]
    public void BankPartiallyCrossedByAnExistingRegionIsNotResynthesized()
    {
        const int bankSize = 0x10000;
        var romSize = bankSize * 3; // C0, C1, C2

        // e.g. a "audio data" region crossing from mid-C0 into C1, per plan §B.5 -- the
        // user/migration is expected to have already tiled the rest of C0 and C1 by hand,
        // so synthesis must not add a whole-bank region that would partially cross this.
        IReadOnlyList<IRegion> existing = [MakeRegion("audio_data", 0xC08000, 0xC18000)];

        var result = BankRegionSynthesis.SynthesizeMissingBankRegions(
            existing, romSize, bankSize, HiRomConvertPcToSnes);

        result.Select(r => r.RegionName).Should().Equal("bank_C2");
    }

    [Fact]
    public void RegionFullyNestedWithinABankDoesNotBlockTheWholeBankSynthesis()
    {
        const int bankSize = 0x10000;

        // a small sub-region entirely inside C0 (e.g. a ptr-table file-producing region) is
        // not an exact match, so the whole-bank region is still synthesized as its parent
        IReadOnlyList<IRegion> existing = [MakeRegion("bank_C0_ptr_tables", 0xC00100, 0xC001FF)];

        var result = BankRegionSynthesis.SynthesizeMissingBankRegions(
            existing, romSize: bankSize, bankSize, HiRomConvertPcToSnes);

        result.Should().ContainSingle();
        result[0].RegionName.Should().Be("bank_C0");
        result[0].StartSnesAddress.Should().Be(0xC00000);
        result[0].EndSnesAddress.Should().Be(0xC0FFFF);
    }

    [Fact]
    public void AnnotationRegionsDoNotCountAsCoverage()
    {
        const int bankSize = 0x10000;

        // an annotation region (ExportSeparateFile=false) matching the bank's extent is NOT
        // file-producing, so it must not suppress synthesis of the real file-producing region
        IReadOnlyList<IRegion> existing =
            [MakeRegion("bank_C0_ctx", 0xC00000, 0xC0FFFF, exportSeparateFile: false)];

        var result = BankRegionSynthesis.SynthesizeMissingBankRegions(
            existing, romSize: bankSize, bankSize, HiRomConvertPcToSnes);

        result.Should().ContainSingle();
        result[0].RegionName.Should().Be("bank_C0");
    }

    [Fact]
    public void ZeroBankSizeYieldsNoRegions()
    {
        var result = BankRegionSynthesis.SynthesizeMissingBankRegions(
            existingRegions: [], romSize: 0x10000, bankSize: 0, HiRomConvertPcToSnes);

        result.Should().BeEmpty();
    }

    [Fact]
    public void UnmappablePcOffsetsAreSkippedRatherThanThrowing()
    {
        const int bankSize = 0x10000;
        var romSize = bankSize * 2;

        // first bank unmappable (-1), second maps normally
        var result = BankRegionSynthesis.SynthesizeMissingBankRegions(
            existingRegions: [], romSize, bankSize,
            offset => offset == 0 ? -1 : HiRomConvertPcToSnes(offset));

        result.Should().ContainSingle();
        result[0].RegionName.Should().Be("bank_C1");
    }
}

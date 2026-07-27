using System;
using System.Collections.Generic;
using System.Linq;
using Diz.Core.commands;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.Cpu._65816;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.HarshAutoStep;

/// <summary>
/// SnesApiExtensions.ApplyAutoStepHarshCommand is the single non-UI entry point for applying an
/// AutoStepHarshCommand. These tests pin it to the ISnesApi.AutoStepHarsh call it wraps:
/// applying the command must leave the ROM in byte-for-byte the same state as calling
/// AutoStepHarsh directly with the same arguments on an identical second ROM, and must return
/// the same offset.
///
/// Expectations come from the algorithm itself (run on that second ROM), never from a
/// hand-copied table of what stepping is expected to produce -- so if the decoder changes, the
/// applier is still held to whatever it now does.
/// </summary>
public class ApplyAutoStepHarshCommandTests
{
    private const int TestRomSize = 0x40;

    /// <summary>
    /// How many bytes at the end of the test ROM are single-byte instructions. An instruction
    /// is at most four bytes long, so with a tail this size nothing decoded inside the ROM can
    /// write past its last byte -- which keeps these tests about the applier rather than about
    /// what stepping off the end of a ROM does.
    /// </summary>
    private const int SingleByteTailLength = 4;

    /// <summary>Everything stepping can touch, per ROM byte. Comparing these lists compares state exactly.</summary>
    private readonly record struct ByteState(
        byte Rom, FlagType Flag, int DataBank, int DirectPage, bool MFlag, bool XFlag,
        Architecture Arch, InOutPoint Point);

    private static Data MakeRom()
    {
        var romBytes = new RomBytes();
        for (var i = 0; i < TestRomSize; ++i)
        {
            romBytes.Add(new RomByte
            {
                // an arbitrary but deterministic spread of opcodes, then a tail of $EA (NOP,
                // one byte) so decoding can reach the last byte without running off the end.
                Rom = i < TestRomSize - SingleByteTailLength ? (byte) (i * 7) : (byte) 0xEA,
                TypeFlag = FlagType.Unreached,
                DataBank = 0x11,
                DirectPage = 0x2233,
                MFlag = false,
                XFlag = true,
            });
        }

        var data = new Data
        {
            RomMapMode = RomMapMode.HiRom,
            RomSpeed = RomSpeed.FastRom,
            RomBytes = romBytes,
        };

        data.Apis.AddIfDoesntExist(new SnesApi(data));
        return data;
    }

    private static ISnesApi<IData> ApiFor(Data data) =>
        data.GetSnesApi() ?? throw new InvalidOperationException("test ROM has no SNES api");

    private static List<ByteState> Snapshot(Data data) =>
        Enumerable.Range(0, data.GetRomSize())
            .Select(i => new ByteState(
                data.GetRomByte(i)!.Value,
                data.GetSnesApi()!.GetFlag(i),
                data.GetSnesApi()!.GetDataBank(i),
                data.GetSnesApi()!.GetDirectPage(i),
                data.GetSnesApi()!.GetMFlag(i),
                data.GetSnesApi()!.GetXFlag(i),
                data.GetArchitecture(i),
                data.GetSnesApi()!.GetInOutPoint(i)))
            .ToList();

    [Theory]
    [InlineData(0x00, 0x08)]
    [InlineData(0x08, 0x10)]
    [InlineData(0x10, 1)]
    // the whole ROM in one go
    [InlineData(0x00, TestRomSize)]
    // a range that runs to the last ROM byte
    [InlineData(TestRomSize - SingleByteTailLength, SingleByteTailLength)]
    [InlineData(TestRomSize - 1, 1)]
    // nothing to do
    [InlineData(0x08, 0)]
    public void ApplierIsIdenticalToCallingAutoStepHarshDirectly(int start, int count)
    {
        var viaApplier = MakeRom();
        var viaAlgorithm = MakeRom();

        Snapshot(viaApplier).Should().Equal(Snapshot(viaAlgorithm), "the two test ROMs start identical");

        var applierResult = ApiFor(viaApplier).ApplyAutoStepHarshCommand(new AutoStepHarshCommand
        {
            Start = start, Count = count,
        });

        var algorithmResult = ApiFor(viaAlgorithm).AutoStepHarsh(start, count);

        applierResult.Should().Be(algorithmResult);
        Snapshot(viaApplier).Should().Equal(Snapshot(viaAlgorithm));
    }

    [Fact]
    public void ApplierDecodesAtLeastTheRequestedRangeAndNothingBeforeIt()
    {
        const int start = 0x08;
        const int count = 0x10;

        var data = MakeRom();
        var before = Snapshot(data);

        var result = ApiFor(data).ApplyAutoStepHarshCommand(new AutoStepHarshCommand
        {
            Start = start, Count = count,
        });

        var after = Snapshot(data);

        result.Should().BeGreaterThanOrEqualTo(start + count,
            "stepping stops at the first instruction boundary at or past the end of the range");

        for (var i = 0; i < start; ++i)
            after[i].Should().Be(before[i], $"offset {i:X} is before the range");

        for (var i = start; i < start + count; ++i)
        {
            var decoded = after[i].Flag is FlagType.Opcode or FlagType.Operand;
            decoded.Should().BeTrue($"offset {i:X} is inside the range, so it was decoded");
        }
    }

    [Fact]
    public void ApplierWithAZeroCountChangesNothingAndAnswersTheStartOffset()
    {
        var data = MakeRom();
        var before = Snapshot(data);

        var result = ApiFor(data).ApplyAutoStepHarshCommand(new AutoStepHarshCommand
        {
            Start = 0x0C, Count = 0,
        });

        result.Should().Be(0x0C);
        Snapshot(data).Should().Equal(before);
    }

    [Fact]
    public void ApplierWithANegativeCountChangesNothingAndAnswersTheStartOffset()
    {
        var data = MakeRom();
        var before = Snapshot(data);

        var result = ApiFor(data).ApplyAutoStepHarshCommand(new AutoStepHarshCommand
        {
            Start = 0x0C, Count = -5,
        });

        result.Should().Be(0x0C);
        Snapshot(data).Should().Equal(before);
    }

    [Fact]
    public void ApplierRunningToTheLastRomByteMarksItAndStopsAtTheEndOfTheRom()
    {
        var data = MakeRom();

        var result = ApiFor(data).ApplyAutoStepHarshCommand(new AutoStepHarshCommand
        {
            Start = TestRomSize - SingleByteTailLength, Count = SingleByteTailLength,
        });

        result.Should().Be(TestRomSize, "the tail is single-byte instructions, so it lands exactly on the end");
        Snapshot(data)[TestRomSize - 1].Flag.Should().Be(FlagType.Opcode);
    }

    [Fact]
    public void ApplierReturnsWhereACallerShouldNavigateTo()
    {
        // the returned offset is the instruction boundary stepping stopped at, which is what a
        // host moves the cursor to afterwards.
        var data = MakeRom();

        var result = ApiFor(data).ApplyAutoStepHarshCommand(new AutoStepHarshCommand
        {
            Start = 0, Count = 1,
        });

        result.Should().BeGreaterThan(0);
        result.Should().Be(ApiFor(MakeRom()).AutoStepHarsh(0, 1));
    }
}

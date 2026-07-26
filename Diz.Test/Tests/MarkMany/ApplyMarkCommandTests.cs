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

namespace Diz.Test.Tests.MarkMany;

/// <summary>
/// SnesApiExtensions.ApplyMarkCommand is the single non-UI entry point for applying a
/// MarkCommand. These tests pin it to the Mark* extension methods it dispatches to: for every
/// MarkManyProperty, applying the command must leave the ROM in byte-for-byte the same state
/// as calling the matching Mark* method directly with the same arguments, and must return the
/// same offset.
///
/// Expectations come from the extension methods themselves (run on a second, identical ROM),
/// never from a hand-copied table -- so if a Mark* method changes, the applier is still held
/// to whatever it now does.
/// </summary>
public class ApplyMarkCommandTests
{
    private const int TestRomSize = 0x30;

    /// <summary>Everything a mark can touch, per ROM byte. Comparing these lists compares state exactly.</summary>
    private readonly record struct ByteState(
        byte Rom, FlagType Flag, int DataBank, int DirectPage, bool MFlag, bool XFlag, Architecture Arch);

    private static Data MakeRom()
    {
        var romBytes = new RomBytes();
        for (var i = 0; i < TestRomSize; ++i)
        {
            romBytes.Add(new RomByte
            {
                Rom = (byte) (i * 3),
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
                data.GetArchitecture(i)))
            .ToList();

    private static int CallMarkExtensionDirectly(
        ISnesApi<IData> api, MarkCommand.MarkManyProperty property, object value, int start, int count) =>
        property switch
        {
            MarkCommand.MarkManyProperty.Flag => api.MarkTypeFlag(start, (FlagType) value, count),
            MarkCommand.MarkManyProperty.DataBank => api.MarkDataBank(start, (int) value, count),
            MarkCommand.MarkManyProperty.DirectPage => api.MarkDirectPage(start, (int) value, count),
            MarkCommand.MarkManyProperty.MFlag => api.MarkMFlag(start, (bool) value, count),
            MarkCommand.MarkManyProperty.XFlag => api.MarkXFlag(start, (bool) value, count),
            MarkCommand.MarkManyProperty.CpuArch => api.MarkArchitecture(start, (Architecture) value, count),
            _ => throw new ArgumentOutOfRangeException(nameof(property)),
        };

    [Theory]
    // one case per MarkManyProperty...
    [InlineData(MarkCommand.MarkManyProperty.Flag, FlagType.Text)]
    [InlineData(MarkCommand.MarkManyProperty.DataBank, 0x7E)]
    [InlineData(MarkCommand.MarkManyProperty.DirectPage, 0x1234)]
    [InlineData(MarkCommand.MarkManyProperty.MFlag, true)]
    [InlineData(MarkCommand.MarkManyProperty.XFlag, false)]
    [InlineData(MarkCommand.MarkManyProperty.CpuArch, Architecture.Apuspc700)]
    // ...plus the pointer flag types, whose marking has the extra "also set the data bank to
    // this byte's own bank" side effect that plain data flags don't.
    [InlineData(MarkCommand.MarkManyProperty.Flag, FlagType.Pointer16Bit)]
    [InlineData(MarkCommand.MarkManyProperty.Flag, FlagType.Pointer24Bit)]
    [InlineData(MarkCommand.MarkManyProperty.Flag, FlagType.Pointer32Bit)]
    public void ApplierIsIdenticalToCallingTheMarkExtensionDirectly(
        MarkCommand.MarkManyProperty property, object value)
    {
        const int start = 0x08;
        const int count = 0x10;

        var viaApplier = MakeRom();
        var viaExtension = MakeRom();

        Snapshot(viaApplier).Should().Equal(Snapshot(viaExtension), "the two test ROMs start identical");

        var applierResult = ApiFor(viaApplier).ApplyMarkCommand(new MarkCommand
        {
            Property = property, Start = start, Count = count, Value = value,
        });

        var extensionResult = CallMarkExtensionDirectly(ApiFor(viaExtension), property, value, start, count);

        applierResult.Should().Be(extensionResult);
        Snapshot(viaApplier).Should().Equal(Snapshot(viaExtension));
    }

    [Fact]
    public void ApplierMarksExactlyTheRequestedRangeAndNothingElse()
    {
        const int start = 0x04;
        const int count = 3;

        var data = MakeRom();
        var before = Snapshot(data);

        ApiFor(data).ApplyMarkCommand(new MarkCommand
        {
            Property = MarkCommand.MarkManyProperty.Flag,
            Start = start,
            Count = count,
            Value = FlagType.Graphics,
        });

        var after = Snapshot(data);

        for (var i = 0; i < TestRomSize; ++i)
        {
            var inRange = i >= start && i < start + count;
            after[i].Flag.Should().Be(inRange ? FlagType.Graphics : before[i].Flag, $"offset {i:X}");
        }

        // nothing but the flag moved
        after.Select(b => b with { Flag = FlagType.Unreached })
            .Should().Equal(before.Select(b => b with { Flag = FlagType.Unreached }));
    }

    [Fact]
    public void ApplierReturnsTheOffsetJustPastTheMarkedRange()
    {
        var data = MakeRom();

        var result = ApiFor(data).ApplyMarkCommand(new MarkCommand
        {
            Property = MarkCommand.MarkManyProperty.DataBank, Start = 0, Count = 4, Value = 0x12,
        });

        result.Should().Be(4);
    }

    [Fact]
    public void ApplierStopsAtTheEndOfTheRomWhenTheCountOvershoots()
    {
        var data = MakeRom();

        var result = ApiFor(data).ApplyMarkCommand(new MarkCommand
        {
            Property = MarkCommand.MarkManyProperty.DataBank,
            Start = TestRomSize - 2,
            Count = 0x1000,
            Value = 0x12,
        });

        result.Should().Be(TestRomSize - 1);
        Snapshot(data)[TestRomSize - 1].DataBank.Should().Be(0x12);
    }

    [Fact]
    public void ApplierWithAnUnrecognizedPropertyChangesNothingAndAnswersMinusOne()
    {
        var data = MakeRom();
        var before = Snapshot(data);

        var result = ApiFor(data).ApplyMarkCommand(new MarkCommand
        {
            Property = (MarkCommand.MarkManyProperty) 99, Start = 0, Count = 8, Value = 0,
        });

        result.Should().Be(-1);
        Snapshot(data).Should().Equal(before);
    }

    [Fact]
    public void ApplierWithAZeroCountChangesNothing()
    {
        var data = MakeRom();
        var before = Snapshot(data);

        ApiFor(data).ApplyMarkCommand(new MarkCommand
        {
            Property = MarkCommand.MarkManyProperty.Flag, Start = 2, Count = 0, Value = FlagType.Text,
        });

        Snapshot(data).Should().Equal(before);
    }

    [Fact]
    public void ApplierThrowsWhenTheBoxedValueDoesNotMatchTheProperty()
    {
        var data = MakeRom();

        var apply = () => ApiFor(data).ApplyMarkCommand(new MarkCommand
        {
            // an int where a FlagType is required
            Property = MarkCommand.MarkManyProperty.Flag, Start = 0, Count = 1, Value = 3,
        });

        apply.Should().Throw<InvalidCastException>();
    }
}

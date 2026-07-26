using System;
using System.Collections.Generic;
using Diz.Core.commands;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.Cpu._65816;
using Diz.Ui.ViewModels.MarkMany;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.MarkMany;

public class MarkManyViewModelTests
{
    private const int RomSize = 0x100;
    private const int SnesBase = 0xC00000;

    /// <summary>
    /// Minimal stand-in for the ROM: the ViewModel only reads size, address conversion, and
    /// the CPU state already recorded at an offset.
    /// </summary>
    private sealed class RomStub : IRomSize, IRomByteFlagsGettable, ISnesAddressConverter
    {
        public int DataBankAtOffset { get; init; } = 0x7E;
        public int DirectPageAtOffset { get; init; } = 0x1234;

        public int GetRomSize() => RomSize;
        public int GetBankSize() => 0x10000;

        public int GetMxFlags(int i) => 0;
        public bool GetMFlag(int i) => false;
        public bool GetXFlag(int i) => false;
        public int GetDataBank(int offset) => DataBankAtOffset;
        public int GetDirectPage(int offset) => DirectPageAtOffset;
        public FlagType GetFlag(int offset) => FlagType.Unreached;

        public int ConvertPCtoSnes(int offset) => offset < 0 ? -1 : SnesBase + offset;

        public int ConvertSnesToPc(int address) =>
            address >= SnesBase && address < SnesBase + RomSize ? address - SnesBase : -1;
    }

    private static MarkManyViewModel<RomStub> MakeVm(
        int start = 0x10, int count = 0x10, RomStub? rom = null, Action<Action>? marshaller = null) =>
        new(rom ?? new RomStub(), start, count, marshaller);

    // ------------------------------------------------------------------
    // defaults + seeding
    // ------------------------------------------------------------------

    [Fact]
    public void TheRangeIsSeededFromTheCallersSelection()
    {
        var vm = MakeVm(0x20, 8);

        vm.Range.StartIndex.Should().Be(0x20);
        vm.Range.Count.Should().Be(8);
        vm.Range.EndIndex.Should().Be(0x27);
    }

    [Fact]
    public void TheDefaultsMatchWhatTheMarkManyUiHasAlwaysStartedWith()
    {
        var vm = MakeVm();

        vm.SelectedProperty.Should().Be(MarkCommand.MarkManyProperty.Flag);
        vm.FlagValue.Should().Be(FlagType.Data8Bit);
        vm.ArchitectureValue.Should().Be(Architecture.Cpu65C816);
        vm.RegisterWidthIs8Bit.Should().BeFalse("16-bit is the first choice offered");
    }

    [Fact]
    public void BothRegisterValuesAreSeededFromTheRomAtTheStartOfTheRange()
    {
        var vm = MakeVm(rom: new RomStub { DataBankAtOffset = 0x42, DirectPageAtOffset = 0x0300 });

        vm.DataBankValue.Should().Be(0x42);
        vm.DirectPageValue.Should().Be(0x0300);
    }

    [Fact]
    public void SelectingARegisterPropertyReseedsThatValueFromTheRom()
    {
        var vm = MakeVm(rom: new RomStub { DataBankAtOffset = 0x42, DirectPageAtOffset = 0x0300 });

        vm.DataBankValue = 0x99;
        vm.SelectedProperty = MarkCommand.MarkManyProperty.DataBank;

        vm.DataBankValue.Should().Be(0x42, "picking 'data bank' offers the bank already recorded here");
    }

    [Fact]
    public void SelectingANonRegisterPropertyLeavesTheRegisterValuesAlone()
    {
        var vm = MakeVm();

        vm.DataBankValue = 0x99;
        vm.DirectPageValue = 0x4444;
        vm.SelectedProperty = MarkCommand.MarkManyProperty.MFlag;

        vm.DataBankValue.Should().Be(0x99);
        vm.DirectPageValue.Should().Be(0x4444);
    }

    // ------------------------------------------------------------------
    // which input is in play
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(MarkCommand.MarkManyProperty.Flag, true, false, false, false)]
    [InlineData(MarkCommand.MarkManyProperty.DataBank, false, true, false, false)]
    [InlineData(MarkCommand.MarkManyProperty.DirectPage, false, true, false, false)]
    [InlineData(MarkCommand.MarkManyProperty.MFlag, false, false, true, false)]
    [InlineData(MarkCommand.MarkManyProperty.XFlag, false, false, true, false)]
    [InlineData(MarkCommand.MarkManyProperty.CpuArch, false, false, false, true)]
    public void ExactlyOneValueInputIsInPlayPerProperty(
        MarkCommand.MarkManyProperty property, bool flag, bool register, bool width, bool arch)
    {
        var vm = MakeVm();
        vm.SelectedProperty = property;

        vm.IsFlagValueUsed.Should().Be(flag);
        vm.IsRegisterValueUsed.Should().Be(register);
        vm.IsRegisterWidthUsed.Should().Be(width);
        vm.IsArchitectureValueUsed.Should().Be(arch);
    }

    [Fact]
    public void TheRegisterInputIsOneByteWideForDataBankAndTwoForDirectPage()
    {
        var vm = MakeVm();

        vm.SelectedProperty = MarkCommand.MarkManyProperty.DataBank;
        vm.MaxRegisterValue.Should().Be(0xFF);
        vm.RegisterValueMaxTextLength.Should().Be(3);

        vm.SelectedProperty = MarkCommand.MarkManyProperty.DirectPage;
        vm.MaxRegisterValue.Should().Be(0xFFFF);
        vm.RegisterValueMaxTextLength.Should().Be(5);
    }

    // ------------------------------------------------------------------
    // building the command
    // ------------------------------------------------------------------

    [Fact]
    public void BuildsAFlagCommand()
    {
        var vm = MakeVm(0x20, 4);
        vm.SelectedProperty = MarkCommand.MarkManyProperty.Flag;
        vm.FlagValue = FlagType.Graphics;

        var command = vm.BuildMarkCommand();

        command.Should().NotBeNull();
        command!.Property.Should().Be(MarkCommand.MarkManyProperty.Flag);
        command.Start.Should().Be(0x20);
        command.Count.Should().Be(4);
        command.Value.Should().BeOfType<FlagType>().And.Be(FlagType.Graphics);
    }

    [Fact]
    public void BuildsADataBankCommand()
    {
        var vm = MakeVm();
        vm.SelectedProperty = MarkCommand.MarkManyProperty.DataBank;
        vm.DataBankValue = 0x80;

        var command = vm.BuildMarkCommand();

        command!.Property.Should().Be(MarkCommand.MarkManyProperty.DataBank);
        command.Value.Should().BeOfType<int>().And.Be(0x80);
    }

    [Fact]
    public void BuildsADirectPageCommand()
    {
        var vm = MakeVm();
        vm.SelectedProperty = MarkCommand.MarkManyProperty.DirectPage;
        vm.DirectPageValue = 0x0300;

        var command = vm.BuildMarkCommand();

        command!.Property.Should().Be(MarkCommand.MarkManyProperty.DirectPage);
        command.Value.Should().BeOfType<int>().And.Be(0x0300);
    }

    [Theory]
    [InlineData(MarkCommand.MarkManyProperty.MFlag)]
    [InlineData(MarkCommand.MarkManyProperty.XFlag)]
    public void BuildsAnMOrXCommandFromTheSharedRegisterWidth(MarkCommand.MarkManyProperty property)
    {
        var vm = MakeVm();
        vm.SelectedProperty = property;
        vm.RegisterWidthIs8Bit = true;

        var command = vm.BuildMarkCommand();

        command!.Property.Should().Be(property);
        command.Value.Should().BeOfType<bool>().And.Be(true);
    }

    [Fact]
    public void BuildsACpuArchitectureCommand()
    {
        var vm = MakeVm();
        vm.SelectedProperty = MarkCommand.MarkManyProperty.CpuArch;
        vm.ArchitectureValue = Architecture.GpuSuperFx;

        var command = vm.BuildMarkCommand();

        command!.Property.Should().Be(MarkCommand.MarkManyProperty.CpuArch);
        command.Value.Should().BeOfType<Architecture>().And.Be(Architecture.GpuSuperFx);
    }

    [Fact]
    public void TheCommandTracksLaterRangeEdits()
    {
        var vm = MakeVm(0x10, 0x10);
        vm.Range.StartText = "C00018";

        var command = vm.BuildMarkCommand();

        command!.Start.Should().Be(0x18);
        command.Count.Should().Be(8);
    }

    // ------------------------------------------------------------------
    // validation
    // ------------------------------------------------------------------

    [Fact]
    public void AValidStateBuildsACommand()
    {
        var vm = MakeVm();

        vm.Validate().IsValid.Should().BeTrue();
        vm.CanBuildMarkCommand.Should().BeTrue();
        vm.BuildMarkCommand().Should().NotBeNull();
    }

    [Fact]
    public void AnEmptyRangeIsRejected()
    {
        var vm = MakeVm();
        vm.Range.Count = 0;

        var result = vm.Validate();

        result.IsValid.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
        vm.BuildMarkCommand().Should().BeNull();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0x100)]
    [InlineData(0xFFFF)]
    public void ADataBankOutsideOneByteIsRejected(int value)
    {
        var vm = MakeVm();
        vm.SelectedProperty = MarkCommand.MarkManyProperty.DataBank;
        vm.DataBankValue = value;

        vm.Validate().IsValid.Should().BeFalse();
        vm.BuildMarkCommand().Should().BeNull();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0x10000)]
    public void ADirectPageOutsideSixteenBitsIsRejected(int value)
    {
        var vm = MakeVm();
        vm.SelectedProperty = MarkCommand.MarkManyProperty.DirectPage;
        vm.DirectPageValue = value;

        vm.Validate().IsValid.Should().BeFalse();
        vm.BuildMarkCommand().Should().BeNull();
    }

    [Fact]
    public void AnOutOfRangeRegisterValueOnlyMattersWhenThatRegisterIsSelected()
    {
        var vm = MakeVm();
        vm.SelectedProperty = MarkCommand.MarkManyProperty.Flag;
        vm.DataBankValue = 0x9999;

        vm.Validate().IsValid.Should().BeTrue("the data bank isn't what's being marked");
    }

    [Fact]
    public void AnUnrecognizedPropertyOrValueIsRejected()
    {
        var vm = MakeVm();
        vm.SelectedProperty = (MarkCommand.MarkManyProperty) 99;
        vm.Validate().IsValid.Should().BeFalse();

        vm.SelectedProperty = MarkCommand.MarkManyProperty.Flag;
        vm.FlagValue = (FlagType) 0xAB;
        vm.Validate().IsValid.Should().BeFalse();

        vm.FlagValue = FlagType.Text;
        vm.SelectedProperty = MarkCommand.MarkManyProperty.CpuArch;
        vm.ArchitectureValue = (Architecture) 0xAB;
        vm.Validate().IsValid.Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // session memory
    // ------------------------------------------------------------------

    [Fact]
    public void SettingsCaptureHoldsAValueForEveryProperty()
    {
        var vm = MakeVm();
        vm.SelectedProperty = MarkCommand.MarkManyProperty.XFlag;

        var settings = vm.CaptureSettings();

        settings.SelectedProperty.Should().Be(MarkCommand.MarkManyProperty.XFlag);
        settings.AllSettings.Keys.Should().BeEquivalentTo(Enum.GetValues<MarkCommand.MarkManyProperty>());
        settings.AllSettings[MarkCommand.MarkManyProperty.Flag].Should().BeOfType<FlagType>();
        settings.AllSettings[MarkCommand.MarkManyProperty.DataBank].Should().BeOfType<int>();
        settings.AllSettings[MarkCommand.MarkManyProperty.MFlag].Should().BeOfType<bool>();
        settings.AllSettings[MarkCommand.MarkManyProperty.CpuArch].Should().BeOfType<Architecture>();
    }

    [Fact]
    public void SettingsRoundTripRestoresTheSameCommand()
    {
        var first = MakeVm(0x30, 6);
        first.SelectedProperty = MarkCommand.MarkManyProperty.CpuArch;
        first.ArchitectureValue = Architecture.Apuspc700;
        first.FlagValue = FlagType.Music;
        first.RegisterWidthIs8Bit = true;

        var settings = first.CaptureSettings();

        var second = MakeVm(0x30, 6);
        second.RestoreSettings(settings);

        second.SelectedProperty.Should().Be(MarkCommand.MarkManyProperty.CpuArch);
        second.ArchitectureValue.Should().Be(Architecture.Apuspc700);
        second.FlagValue.Should().Be(FlagType.Music);
        second.RegisterWidthIs8Bit.Should().BeTrue();

        second.BuildMarkCommand().Should().BeEquivalentTo(first.BuildMarkCommand());
    }

    [Fact]
    public void RestoringARegisterValueLosesToTheRomReseed()
    {
        // Documents a real quirk carried over from the old dialog: the selection is applied
        // last, and applying it reseeds the register value from the ROM. So a remembered data
        // bank never survives when data bank is the property being restored.
        var rom = new RomStub { DataBankAtOffset = 0x42 };
        var first = MakeVm(rom: rom);
        first.SelectedProperty = MarkCommand.MarkManyProperty.DataBank;
        first.DataBankValue = 0x99;

        var second = MakeVm(rom: rom);
        second.RestoreSettings(first.CaptureSettings());

        second.DataBankValue.Should().Be(0x42);
    }

    [Fact]
    public void RestoringSurvivesAValueOfTheWrongTypeInsteadOfThrowing()
    {
        var vm = MakeVm();
        vm.FlagValue = FlagType.Music;

        var restore = () => vm.RestoreSettings(new MarkManySettings
        {
            SelectedProperty = MarkCommand.MarkManyProperty.Flag,
            AllSettings =
            {
                [MarkCommand.MarkManyProperty.Flag] = "not a flag type",
                [MarkCommand.MarkManyProperty.MFlag] = 12345,
            },
        });

        restore.Should().NotThrow();
        vm.FlagValue.Should().Be(FlagType.Music, "an unusable stored value leaves the current one alone");
    }

    // ------------------------------------------------------------------
    // notifications
    // ------------------------------------------------------------------

    [Fact]
    public void ChangingThePropertyNotifiesEverythingThatDependsOnIt()
    {
        var vm = MakeVm();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.SelectedProperty = MarkCommand.MarkManyProperty.DataBank;

        raised.Should().Contain(nameof(MarkManyViewModel<RomStub>.SelectedProperty));
        raised.Should().Contain(nameof(MarkManyViewModel<RomStub>.IsRegisterValueUsed));
        raised.Should().Contain(nameof(MarkManyViewModel<RomStub>.MaxRegisterValue));
        raised.Should().Contain(nameof(MarkManyViewModel<RomStub>.SelectedValue));
    }

    [Fact]
    public void EveryNotificationGoesThroughTheMarshaller()
    {
        var queued = new List<Action>();
        var vm = MakeVm(marshaller: queued.Add);

        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);
        vm.Range.PropertyChanged += (_, e) => raised.Add("Range." + e.PropertyName);

        vm.FlagValue = FlagType.Music;
        vm.Range.Count = 4;

        raised.Should().BeEmpty("nothing may bypass the marshaller -- including the child range VM");

        foreach (var queuedAction in queued)
            queuedAction();

        raised.Should().Contain(nameof(MarkManyViewModel<RomStub>.FlagValue));
        raised.Should().Contain("Range." + nameof(AddressRangeViewModel.Count));
    }

    // ------------------------------------------------------------------
    // the whole point: VM builds it, the applier applies it
    // ------------------------------------------------------------------

    [Fact]
    public void ACommandBuiltHereIsAppliedByTheSnesApplier()
    {
        var romBytes = new RomBytes();
        for (var i = 0; i < 0x20; ++i)
            romBytes.Add(new RomByte { Rom = (byte) i });

        var data = new Data
        {
            RomMapMode = RomMapMode.HiRom,
            RomSpeed = RomSpeed.FastRom,
            RomBytes = romBytes,
        };
        data.Apis.AddIfDoesntExist(new SnesApi(data));
        var snesApi = data.GetSnesApi()!;

        var vm = new MarkManyViewModel<ISnesData>(snesApi, startOffset: 4, count: 3);
        vm.SelectedProperty = MarkCommand.MarkManyProperty.Flag;
        vm.FlagValue = FlagType.Text;

        var command = vm.BuildMarkCommand();
        command.Should().NotBeNull();

        snesApi.ApplyMarkCommand(command!);

        snesApi.GetFlag(3).Should().Be(FlagType.Unreached);
        snesApi.GetFlag(4).Should().Be(FlagType.Text);
        snesApi.GetFlag(6).Should().Be(FlagType.Text);
        snesApi.GetFlag(7).Should().Be(FlagType.Unreached);
    }
}

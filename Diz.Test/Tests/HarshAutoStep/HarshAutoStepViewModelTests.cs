using System;
using System.Collections.Generic;
using Diz.Core.Interfaces;
using Diz.Ui.ViewModels.HarshAutoStep;
using Diz.Ui.ViewModels.MarkMany;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.HarshAutoStep;

/// <summary>
/// HarshAutoStepViewModel: a start/end/count range over the ROM plus the validation that
/// decides whether it describes anything worth stepping through.
///
/// The converter below is deliberately trivial (SNES = $C00000 + offset) so every expected
/// number is obvious by inspection. It also honours a second, mirrored bank ($400000), the way
/// real HiROM mappings do, and answers -1 for anything else -- which is what makes "an address
/// that maps nowhere is ignored" observable.
///
/// The range type itself is covered by AddressRangeViewModelTests; what is pinned here is how
/// this ViewModel seeds it, what it refuses, and the command it builds.
/// </summary>
public class HarshAutoStepViewModelTests
{
    private const int RomSize = 0x400;
    private const int SnesBase = 0xC00000;
    private const int MirrorBase = 0x400000;

    private sealed class OffsetConverter : ISnesAddressConverter
    {
        // -1 is what this codebase uses everywhere for "that address isn't in this ROM"
        public int ConvertPCtoSnes(int offset) => offset < 0 ? -1 : SnesBase + offset;

        public int ConvertSnesToPc(int address)
        {
            if (address >= SnesBase && address < SnesBase + RomSize)
                return address - SnesBase;
            if (address >= MirrorBase && address < MirrorBase + RomSize)
                return address - MirrorBase;
            return -1;
        }
    }

    private static HarshAutoStepViewModel MakeVm(
        int startPcOffset = 0x100, Action<Action> marshaller = null) =>
        new(new OffsetConverter(), RomSize, startPcOffset, marshaller);

    private static List<string> RecordNotifications(HarshAutoStepViewModel vm)
    {
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add("vm." + e.PropertyName);
        vm.Range.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        return raised;
    }

    // ------------------------------------------------------------------
    // construction / seeding
    // ------------------------------------------------------------------

    [Fact]
    public void TheRangeIsSeededAtTheCallersOffsetForTheDefaultNumberOfBytes()
    {
        var vm = MakeVm(0x100);

        vm.Range.StartIndex.Should().Be(0x100);
        vm.Range.Count.Should().Be(HarshAutoStepViewModel.DefaultCount);
        vm.Range.EndIndex.Should().Be(0x1FF, "the end is inclusive, so $100 bytes end at start + $FF");
    }

    [Fact]
    public void TheDefaultCountIsTwoHundredAndFiftySixBytes()
    {
        HarshAutoStepViewModel.DefaultCount.Should().Be(0x100);
    }

    [Fact]
    public void SeedingAtOffsetZeroWorks()
    {
        var vm = MakeVm(0);

        vm.Range.StartIndex.Should().Be(0);
        vm.Range.Count.Should().Be(0x100);
        vm.Range.EndIndex.Should().Be(0xFF);
    }

    [Fact]
    public void Quirk1_TheDefaultCountSurvivesIntactRightUpToTheLastByteOfTheRom()
    {
        // The range that ends exactly on the last ROM byte is the interesting one: it fits, so
        // nothing may be shaved off it. (The dialog this replaced clamped the END first and
        // then recomputed the count from it, handing back $FF bytes here instead of $100.)
        var vm = MakeVm(RomSize - 0x100);

        vm.Range.StartIndex.Should().Be(RomSize - 0x100);
        vm.Range.Count.Should().Be(0x100);
        vm.Range.EndIndex.Should().Be(RomSize - 1);
    }

    [Fact]
    public void SeedingAtTheLastByteLeavesExactlyOneByteInTheRange()
    {
        var vm = MakeVm(RomSize - 1);

        vm.Range.StartIndex.Should().Be(RomSize - 1);
        vm.Range.Count.Should().Be(1, "there is only one byte left to step through");
        vm.Range.EndIndex.Should().Be(RomSize - 1);
    }

    [Fact]
    public void SeedingNearTheEndKeepsEveryRemainingByteAndNoMore()
    {
        var vm = MakeVm(RomSize - 0x40);

        vm.Range.Count.Should().Be(0x40);
        vm.Range.EndIndex.Should().Be(RomSize - 1);
    }

    [Fact]
    public void AStartOffsetOutsideTheRomIsClampedIntoIt()
    {
        // unlike a single-destination ViewModel, a range has to be a range: there is no useful
        // "invalid start" state to show, so the ROM's last byte is where it lands.
        var vm = MakeVm(RomSize + 0x1000);

        vm.Range.StartIndex.Should().Be(RomSize - 1);
        vm.Range.Count.Should().Be(1);
        vm.CanBuildAutoStepCommand.Should().BeTrue();
    }

    [Fact]
    public void AFreshlySeededViewModelIsReadyToBuildACommand()
    {
        var vm = MakeVm(0x100);

        vm.Validate().IsValid.Should().BeTrue();
        vm.CanBuildAutoStepCommand.Should().BeTrue();
        vm.ValidationMessage.Should().BeEmpty();
    }

    [Fact]
    public void AConverterIsRequired()
    {
        var make = () => new HarshAutoStepViewModel(null, RomSize, 0);
        make.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ARomWithNoBytesInItIsRejected(int romSize)
    {
        var make = () => new HarshAutoStepViewModel(new OffsetConverter(), romSize, 0);
        make.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TheRangeStartsOutShowingHexadecimalSnesAddresses()
    {
        var vm = MakeVm(0x100);

        vm.Range.UseSnesAddresses.Should().BeTrue();
        vm.Range.UseHexadecimal.Should().BeTrue();
        vm.Range.StartText.Should().Be("C00100");
        vm.Range.EndText.Should().Be("C001FF");
        vm.Range.CountText.Should().Be("100");
    }

    // ------------------------------------------------------------------
    // end-inclusive arithmetic (the contract the old dialog did NOT have)
    // ------------------------------------------------------------------

    [Fact]
    public void Quirk2_TypingAnEndOfStartPlusFFGivesACountOf100Bytes()
    {
        // END IS INCLUSIVE: the byte named in the end box is stepped through. Typing
        // start + $FF therefore asks for $100 bytes, not $FF.
        var vm = MakeVm(0x100);

        vm.Range.EndText = "C001FF";

        vm.Range.StartIndex.Should().Be(0x100);
        vm.Range.EndIndex.Should().Be(0x1FF);
        vm.Range.Count.Should().Be(0x100);
        vm.BuildAutoStepHarshCommand()!.Count.Should().Be(0x100);
    }

    [Fact]
    public void Quirk2_AnEndEqualToTheStartIsASingleByte()
    {
        var vm = MakeVm(0x100);

        vm.Range.EndText = "C00100";

        vm.Range.Count.Should().Be(1);
        vm.CanBuildAutoStepCommand.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // mutual update through the range
    // ------------------------------------------------------------------

    [Fact]
    public void TypingAStartSnesAddressLeavesTheEndAloneAndMovesTheCount()
    {
        var vm = MakeVm(0x100);

        vm.Range.StartText = "C00180";

        vm.Range.StartIndex.Should().Be(0x180);
        vm.Range.EndIndex.Should().Be(0x1FF, "the end stays put");
        vm.Range.Count.Should().Be(0x80);
        vm.Range.CountText.Should().Be("80");
    }

    [Fact]
    public void TypingAnEndSnesAddressLeavesTheStartAloneAndMovesTheCount()
    {
        var vm = MakeVm(0x100);

        vm.Range.EndText = "C0017F";

        vm.Range.StartIndex.Should().Be(0x100, "the start stays put");
        vm.Range.Count.Should().Be(0x80);
    }

    [Fact]
    public void TypingACountLeavesTheStartAloneAndMovesTheEnd()
    {
        var vm = MakeVm(0x100);

        vm.Range.CountText = "40";

        vm.Range.StartIndex.Should().Be(0x100);
        vm.Range.Count.Should().Be(0x40);
        vm.Range.EndIndex.Should().Be(0x13F);
        vm.Range.EndText.Should().Be("C0013F");
    }

    [Fact]
    public void TheAddressBoxesCanBeTypedAsRomFileOffsetsInstead()
    {
        var vm = MakeVm(0x100);
        vm.Range.UseSnesAddresses = false;

        vm.Range.StartText.Should().Be("100", "an offset is never zero-padded");
        vm.Range.EndText.Should().Be("1FF");

        vm.Range.StartText = "180";

        vm.Range.StartIndex.Should().Be(0x180);
        vm.Range.Count.Should().Be(0x80);
    }

    [Fact]
    public void TheBoxesCanBeReadAndWrittenInDecimal()
    {
        var vm = MakeVm(0x100);
        vm.Range.UseHexadecimal = false;

        vm.Range.StartText.Should().Be("12583168", "$C00100 in decimal");
        vm.Range.CountText.Should().Be("256", "$100 in decimal");

        vm.Range.CountText = "64";

        vm.Range.Count.Should().Be(64, "decimal 64, not $64");
        vm.Range.EndIndex.Should().Be(0x100 + 64 - 1);
    }

    [Fact]
    public void DecimalRomFileOffsetsWorkTogether()
    {
        var vm = MakeVm(0x100);
        vm.Range.UseSnesAddresses = false;
        vm.Range.UseHexadecimal = false;

        vm.Range.StartText.Should().Be("256");

        vm.Range.StartText = "300";

        vm.Range.StartIndex.Should().Be(300);
        vm.BuildAutoStepHarshCommand()!.Start.Should().Be(300);
    }

    [Fact]
    public void ASnesAddressInAMirroredBankReachesTheSameByte()
    {
        var vm = MakeVm(0x100);

        vm.Range.StartText = "400180";

        vm.Range.StartIndex.Should().Be(0x180);
    }

    // ------------------------------------------------------------------
    // quirk 4: an address that maps nowhere is ignored, not obeyed
    // ------------------------------------------------------------------

    [Fact]
    public void Quirk4_AStartAddressThatMapsNowhereInThisRomIsIgnored()
    {
        // $7E0000 is SNES WRAM: a real address, but no byte of this ROM is there. The old
        // dialog took the converter's -1 answer literally and jumped the start to offset 0.
        var vm = MakeVm(0x100);

        vm.Range.StartText = "7E0000";

        vm.Range.StartIndex.Should().Be(0x100, "the range must not move for an address it can't reach");
        vm.Range.Count.Should().Be(0x100);
    }

    [Fact]
    public void Quirk4_AnEndAddressThatMapsNowhereInThisRomIsIgnored()
    {
        var vm = MakeVm(0x100);

        vm.Range.EndText = "7E0000";

        vm.Range.StartIndex.Should().Be(0x100);
        vm.Range.EndIndex.Should().Be(0x1FF);
        vm.Range.Count.Should().Be(0x100);
    }

    [Fact]
    public void TextThatIsNotANumberAtAllIsIgnoredToo()
    {
        var vm = MakeVm(0x100);

        vm.Range.StartText = "not an address";
        vm.Range.CountText = "zzz";

        vm.Range.StartIndex.Should().Be(0x100);
        vm.Range.Count.Should().Be(0x100);
    }

    // ------------------------------------------------------------------
    // validation
    // ------------------------------------------------------------------

    [Fact]
    public void AnEmptyRangeIsRefusedWithAMessage()
    {
        var vm = MakeVm(0x100);
        vm.Range.Count = 0;

        var result = vm.Validate();

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be(HarshAutoStepViewModel.EmptyRangeMessage);
        vm.ValidationMessage.Should().Be(HarshAutoStepViewModel.EmptyRangeMessage);
        vm.CanBuildAutoStepCommand.Should().BeFalse();
    }

    [Fact]
    public void ACountTypedAsZeroIsRefused()
    {
        var vm = MakeVm(0x100);

        vm.Range.CountText = "0";

        vm.CanBuildAutoStepCommand.Should().BeFalse();
        vm.BuildAutoStepHarshCommand().Should().BeNull();
    }

    [Fact]
    public void CorrectingAnEmptyRangeMakesTheCommandAvailableAgain()
    {
        var vm = MakeVm(0x100);

        vm.Range.Count = 0;
        vm.CanBuildAutoStepCommand.Should().BeFalse();

        vm.Range.CountText = "20";

        vm.CanBuildAutoStepCommand.Should().BeTrue();
        vm.ValidationMessage.Should().BeEmpty();
        vm.BuildAutoStepHarshCommand()!.Count.Should().Be(0x20);
    }

    [Fact]
    public void ASingleByteIsEnoughToStepThrough()
    {
        var vm = MakeVm(0x100);
        vm.Range.CountText = "1";

        vm.CanBuildAutoStepCommand.Should().BeTrue();
        vm.BuildAutoStepHarshCommand()!.Count.Should().Be(1);
    }

    // ------------------------------------------------------------------
    // building the command
    // ------------------------------------------------------------------

    [Fact]
    public void TheCommandCarriesTheRangesStartAndByteCount()
    {
        var vm = MakeVm(0x120);

        var command = vm.BuildAutoStepHarshCommand();

        command.Should().NotBeNull();
        command!.Start.Should().Be(vm.Range.StartIndex).And.Be(0x120);
        command.Count.Should().Be(vm.Range.Count).And.Be(0x100);
    }

    [Fact]
    public void TheCommandTracksLaterRangeEdits()
    {
        var vm = MakeVm(0x100);

        vm.Range.StartText = "C00110";
        vm.Range.CountText = "8";

        var command = vm.BuildAutoStepHarshCommand();

        command!.Start.Should().Be(0x110);
        command.Count.Should().Be(8);
    }

    [Fact]
    public void ACommandIsBuiltExactlyWhenTheStateSaysItCanBe()
    {
        var vm = MakeVm(0x100);

        vm.CanBuildAutoStepCommand.Should().BeTrue();
        vm.BuildAutoStepHarshCommand().Should().NotBeNull();

        vm.Range.Count = 0;

        vm.CanBuildAutoStepCommand.Should().BeFalse();
        vm.BuildAutoStepHarshCommand().Should().BeNull();
    }

    [Fact]
    public void EachCallBuildsAFreshCommand()
    {
        var vm = MakeVm(0x100);

        var first = vm.BuildAutoStepHarshCommand();
        var second = vm.BuildAutoStepHarshCommand();

        second.Should().NotBeSameAs(first);
        second.Should().BeEquivalentTo(first);
    }

    // ------------------------------------------------------------------
    // marshaller discipline
    // ------------------------------------------------------------------

    [Fact]
    public void EveryNotificationGoesThroughTheMarshaller()
    {
        // the thread rule: a host's marshaller is the only path to the UI thread, so nothing
        // may be raised around it -- including from the child range ViewModel.
        var queued = new List<Action>();
        var vm = MakeVm(0x100, marshaller: queued.Add);
        var raised = RecordNotifications(vm);

        vm.Range.CountText = "40";
        vm.Range.UseHexadecimal = false;

        raised.Should().BeEmpty("nothing may bypass the marshaller -- including the child range VM");
        queued.Should().NotBeEmpty();

        foreach (var queuedAction in queued)
            queuedAction();

        raised.Should().Contain(nameof(AddressRangeViewModel.Count));
        raised.Should().Contain(nameof(AddressRangeViewModel.EndText));
        raised.Should().Contain(nameof(AddressRangeViewModel.UseHexadecimal));
    }

    [Fact]
    public void TheChildRangeSharesThisViewModelsMarshaller()
    {
        var queued = new List<Action>();
        var vm = MakeVm(0x100, marshaller: queued.Add);
        var raised = RecordNotifications(vm);

        vm.Range.StartIndex = 0x140;

        raised.Should().BeEmpty();
        queued.Should().NotBeEmpty("the range VM was handed the same marshaller, not the default one");
    }

    [Fact]
    public void TheEditedFieldIsNotRewrittenUnderTheCaret()
    {
        var vm = MakeVm(0x100, marshaller: null);
        var raised = RecordNotifications(vm);

        vm.Range.StartText = "C00180";

        raised.Should().NotContain(nameof(AddressRangeViewModel.StartText),
            "rewriting the field being typed in would fight the caret");
        raised.Should().Contain(nameof(AddressRangeViewModel.CountText));
    }

    [Fact]
    public void StateIsReadableBeforeADeferredMarshallerHasRunAnything()
    {
        var queued = new List<Action>();
        var vm = MakeVm(0x100, marshaller: queued.Add);

        vm.Range.CountText = "10";

        vm.Range.Count.Should().Be(0x10, "the state changes immediately; only the telling is deferred");
        vm.BuildAutoStepHarshCommand()!.Count.Should().Be(0x10);
        queued.Should().NotBeEmpty();
    }
}

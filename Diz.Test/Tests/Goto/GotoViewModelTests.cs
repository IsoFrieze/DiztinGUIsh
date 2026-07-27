using System;
using System.Collections.Generic;
using Diz.Core.Interfaces;
using Diz.Ui.ViewModels.Goto;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.Goto;

/// <summary>
/// GotoViewModel: a SNES address box and a ROM file offset box that mirror each other, plus a
/// hex/decimal toggle and the validation that decides whether the pair names a destination.
///
/// The converter below is deliberately trivial (SNES = $C00000 + offset) so every expected
/// number is obvious by inspection. It also honours a second, mirrored bank ($400000), which
/// real HiROM mappings do: two different SNES addresses reaching the same ROM byte is what
/// makes "re-express each box from its own number" observably different from "re-express both
/// from the offset".
/// </summary>
public class GotoViewModelTests
{
    private const int RomSize = 0x100;
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

    /// <summary>
    /// A ROM whose first bank sits low in the SNES address space, the way a LoROM's does
    /// ($008000). Used only where the six-digit zero padding has to be visible.
    /// </summary>
    private sealed class LowBankConverter : ISnesAddressConverter
    {
        private const int LowBase = 0x8000;

        public int ConvertPCtoSnes(int offset) => offset < 0 ? -1 : LowBase + offset;

        public int ConvertSnesToPc(int address) =>
            address >= LowBase && address < LowBase + RomSize ? address - LowBase : -1;
    }

    private static GotoViewModel MakeGoto(int startPcOffset = 0x10, Action<Action> marshaller = null) =>
        new(new OffsetConverter(), RomSize, startPcOffset, marshaller);

    private static List<string> RecordNotifications(GotoViewModel vm)
    {
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        return raised;
    }

    // ------------------------------------------------------------------
    // construction / seeding
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(0x00, "C00000", "0")]
    [InlineData(0x10, "C00010", "10")]
    [InlineData(0xAB, "C000AB", "AB")]
    [InlineData(RomSize - 1, "C000FF", "FF")]
    public void SeedsTheSnesAddressPaddedToSixHexDigitsAndTheOffsetUnpadded(
        int startPcOffset, string expectedSnesText, string expectedPcText)
    {
        var vm = MakeGoto(startPcOffset);

        vm.SnesText.Should().Be(expectedSnesText);
        vm.PcText.Should().Be(expectedPcText);
        vm.UseHexadecimal.Should().BeTrue("the base always starts out hexadecimal");
    }

    [Fact]
    public void AFreshlySeededViewModelIsReadyToConfirm()
    {
        var vm = MakeGoto(0x10);

        vm.CanConfirm.Should().BeTrue();
        vm.ValidationMessage.Should().BeEmpty();
        vm.ResultPcOffset.Should().Be(0x10);
    }

    [Fact]
    public void AStartOffsetOutsideTheRomSeedsAnInvalidStateInsteadOfBeingClamped()
    {
        var vm = MakeGoto(RomSize + 0x100);

        vm.CanConfirm.Should().BeFalse();
        vm.ResultPcOffset.Should().BeNull();
    }

    [Fact]
    public void AConverterIsRequired()
    {
        var make = () => new GotoViewModel(null, RomSize, 0);
        make.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ARomWithNoBytesInItIsRejected(int romSize)
    {
        var make = () => new GotoViewModel(new OffsetConverter(), romSize, 0);
        make.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ------------------------------------------------------------------
    // mutual update
    // ------------------------------------------------------------------

    [Fact]
    public void TypingASnesAddressMovesTheOffset()
    {
        var vm = MakeGoto(0x10);

        vm.SnesText = "C00020";

        vm.PcText.Should().Be("20");
        vm.SnesText.Should().Be("C00020");
        vm.ResultPcOffset.Should().Be(0x20);
    }

    [Fact]
    public void TypingAnOffsetMovesTheSnesAddress()
    {
        var vm = MakeGoto(0x10);

        vm.PcText = "20";

        vm.SnesText.Should().Be("C00020");
        vm.PcText.Should().Be("20");
        vm.ResultPcOffset.Should().Be(0x20);
    }

    [Fact]
    public void TextThatDoesNotParseLeavesTheOtherBoxAlone()
    {
        var vm = MakeGoto(0x10);

        vm.SnesText = "not an address";

        vm.SnesText.Should().Be("not an address", "the user has to be able to keep typing");
        vm.PcText.Should().Be("10", "the other box keeps its last good value");
    }

    [Fact]
    public void OffsetTextThatDoesNotParseLeavesTheSnesBoxAlone()
    {
        var vm = MakeGoto(0x10);

        vm.PcText = "zz";

        vm.PcText.Should().Be("zz");
        vm.SnesText.Should().Be("C00010");
    }

    [Fact]
    public void AMirroredSnesAddressReachesTheSameOffsetAndKeepsTheBankThatWasTyped()
    {
        var vm = MakeGoto(0x10);

        vm.SnesText = "400018";

        vm.SnesText.Should().Be("400018", "the box shows the address the user actually typed");
        vm.PcText.Should().Be("18");
        vm.ResultPcOffset.Should().Be(0x18);
    }

    // ------------------------------------------------------------------
    // caret / rewrite rule
    // ------------------------------------------------------------------

    [Fact]
    public void TheEditedBoxIsNotRewrittenWhenAcceptingChangedNothing()
    {
        var vm = MakeGoto(0x10);
        var raised = RecordNotifications(vm);

        vm.SnesText = "C00020";

        raised.Should().Contain(nameof(GotoViewModel.PcText));
        raised.Should().NotContain(nameof(GotoViewModel.SnesText),
            "rewriting the field being typed in would fight the caret");
    }

    [Fact]
    public void TheEditedBoxIsNotRewrittenWhenTheTextDidNotParseEither()
    {
        var vm = MakeGoto(0x10);
        var raised = RecordNotifications(vm);

        vm.PcText = "nonsense";

        raised.Should().NotContain(nameof(GotoViewModel.PcText));
        raised.Should().Contain(nameof(GotoViewModel.SnesText),
            "the other box always re-notifies, so a view stays consistent");
    }

    [Fact]
    public void PastingALabelStripsItDownToTheAddressAndSaysSo()
    {
        var vm = MakeGoto(0x10);
        var raised = RecordNotifications(vm);

        vm.SnesText = "CODE_C0001A";

        vm.SnesText.Should().Be("C0001A", "pasting a label out of the disassembly has to work");
        vm.PcText.Should().Be("1A");
        raised.Should().Contain(nameof(GotoViewModel.SnesText),
            "the box really did change, so the view has to be told");
    }

    [Theory]
    [InlineData("$C00030", "C00030")]
    [InlineData("C0/0030", "C00030")]
    [InlineData("$C0/0030", "C00030")]
    [InlineData("UNREACH_C00030", "C00030")]
    public void DecorationAroundAnAddressIsStrippedOff(string typed, string expected)
    {
        var vm = MakeGoto(0x10);

        vm.SnesText = typed;

        vm.SnesText.Should().Be(expected);
        vm.PcText.Should().Be("30");
    }

    [Fact]
    public void ALabelPastedIntoTheOffsetBoxIsStrippedTheSameWay()
    {
        var vm = MakeGoto(0x10);

        vm.PcText = "DATA8_0000A0";

        vm.PcText.Should().Be("0000A0");
        vm.SnesText.Should().Be("C000A0");
        vm.ResultPcOffset.Should().Be(0xA0);
    }

    // ------------------------------------------------------------------
    // hex / decimal
    // ------------------------------------------------------------------

    [Fact]
    public void SwitchingToDecimalReExpressesBothBoxes()
    {
        var vm = MakeGoto(0x10);

        vm.UseHexadecimal = false;

        vm.SnesText.Should().Be("12582928", "$C00010 in decimal");
        vm.PcText.Should().Be("16", "$10 in decimal");
        vm.CanConfirm.Should().BeTrue();
        vm.ResultPcOffset.Should().Be(0x10);
    }

    [Fact]
    public void SwitchingBackToHexadecimalRestoresTheHexText()
    {
        var vm = MakeGoto(0x10);

        vm.UseHexadecimal = false;
        vm.UseHexadecimal = true;

        vm.SnesText.Should().Be("C00010");
        vm.PcText.Should().Be("10");
    }

    [Fact]
    public void SwitchingBaseReExpressesEachBoxFromItsOwnNumber()
    {
        var vm = MakeGoto(0x10);
        vm.SnesText = "400018";

        vm.UseHexadecimal = false;

        vm.SnesText.Should().Be("4194328", "$400018 in decimal, NOT the canonical $C00018");
        vm.PcText.Should().Be("24");
    }

    [Fact]
    public void SwitchingBaseLeavesInvalidTextExactlyAsTyped()
    {
        var vm = MakeGoto(0x10);
        vm.SnesText = "garbage";

        vm.UseHexadecimal = false;

        vm.SnesText.Should().Be("garbage", "half-converting text that means nothing helps nobody");
        vm.PcText.Should().Be("10", "and its partner is left alone too");
        vm.CanConfirm.Should().BeFalse();
    }

    [Fact]
    public void TypingIsReadInTheSelectedBase()
    {
        var vm = MakeGoto(0x10);
        vm.UseHexadecimal = false;

        vm.PcText = "32";

        vm.ResultPcOffset.Should().Be(32, "decimal 32, not $32");
        vm.PcText.Should().Be("32");
        vm.SnesText.Should().Be("12582944", "$C00020 in decimal");
    }

    [Fact]
    public void TheSnesBoxIsZeroPaddedToSixDigitsInEitherBase()
    {
        // the low-bank converter makes the padding visible: $8010 is only four hex digits and
        // 32784 is only five decimal ones, so anything the box shows past that is padding.
        var vm = new GotoViewModel(new LowBankConverter(), RomSize, 0x10);

        vm.SnesText.Should().Be("008010");
        vm.PcText.Should().Be("10", "an offset is never padded");

        vm.UseHexadecimal = false;

        vm.SnesText.Should().Be("032784");
        vm.PcText.Should().Be("16");
    }

    [Fact]
    public void SettingTheBaseToWhatItAlreadyIsChangesNothing()
    {
        var vm = MakeGoto(0x10);
        var raised = RecordNotifications(vm);

        vm.UseHexadecimal = true;

        raised.Should().BeEmpty();
        vm.SnesText.Should().Be("C00010");
    }

    [Fact]
    public void SwitchingBaseRefreshesBothTextProjections()
    {
        var vm = MakeGoto(0x10);
        var raised = RecordNotifications(vm);

        vm.UseHexadecimal = false;

        raised.Should().Contain(nameof(GotoViewModel.UseHexadecimal));
        raised.Should().Contain(nameof(GotoViewModel.SnesText));
        raised.Should().Contain(nameof(GotoViewModel.PcText));
    }

    // ------------------------------------------------------------------
    // validation
    // ------------------------------------------------------------------

    [Fact]
    public void AnUnreadableSnesAddressIsReportedAsAnInvalidSnesAddress()
    {
        var vm = MakeGoto(0x10);

        vm.SnesText = "zzz";

        vm.Validate().IsValid.Should().BeFalse();
        vm.ValidationMessage.Should().Be(GotoViewModel.InvalidSnesAddressMessage);
    }

    [Fact]
    public void ASnesAddressOutsideThisRomIsReportedAsAnInvalidSnesAddress()
    {
        var vm = MakeGoto(0x10);

        // $7E0000 is SNES WRAM: a real address, but no byte of this ROM is there.
        vm.SnesText = "7E0000";

        vm.ValidationMessage.Should().Be(GotoViewModel.InvalidSnesAddressMessage);
        vm.CanConfirm.Should().BeFalse();
        vm.ResultPcOffset.Should().BeNull();
    }

    [Fact]
    public void AnUnreadableOffsetIsReportedAsAnInvalidRomFileOffset()
    {
        var vm = MakeGoto(0x10);

        // only the offset box is spoiled: the SNES box still holds a good address
        vm.PcText = "zzz";

        vm.ValidationMessage.Should().Be(GotoViewModel.InvalidPcOffsetMessage);
        vm.CanConfirm.Should().BeFalse();
    }

    [Fact]
    public void WhenBothBoxesAreBadTheSnesMessageIsTheOneReported()
    {
        var vm = MakeGoto(0x10);

        vm.PcText = "zzz";
        vm.SnesText = "yyy";

        vm.ValidationMessage.Should().Be(GotoViewModel.InvalidSnesAddressMessage);
    }

    [Fact]
    public void AnOffsetPastTheEndOfTheRomIsRefused()
    {
        var vm = MakeGoto(0x10);

        vm.PcText = "100"; // ROM is 0x100 bytes: 0x100 is one past the last one

        vm.CanConfirm.Should().BeFalse();
        vm.ResultPcOffset.Should().BeNull();
    }

    [Fact]
    public void TheLastByteOfTheRomIsAValidDestination()
    {
        var vm = MakeGoto(0x10);

        vm.PcText = "FF";

        vm.CanConfirm.Should().BeTrue();
        vm.ResultPcOffset.Should().Be(RomSize - 1);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("zzz")]
    [InlineData("7E0000")]      // a real SNES address, but not in this ROM
    [InlineData("FFFFFFFF")]    // reads as -1 in hex
    [InlineData("1000000")]     // past every bank this ROM has
    [InlineData("C0 0010")]     // a space in the middle: not a number
    public void ConfirmIsRefusedForEveryInvalidSnesAddressReachableByTyping(string typed)
    {
        var vm = MakeGoto(0x10);

        vm.SnesText = typed;

        vm.CanConfirm.Should().BeFalse();
        vm.ResultPcOffset.Should().BeNull();
        vm.ValidationMessage.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("zz")]
    [InlineData("100")]         // one past the end of the ROM
    [InlineData("FFFFFFFF")]    // reads as -1 in hex
    public void ConfirmIsRefusedForEveryInvalidOffsetReachableByTyping(string typed)
    {
        var vm = MakeGoto(0x10);

        vm.PcText = typed;

        vm.CanConfirm.Should().BeFalse();
        vm.ResultPcOffset.Should().BeNull();
        vm.ValidationMessage.Should().NotBeEmpty();
    }

    [Fact]
    public void CorrectingABadAddressMakesConfirmAvailableAgain()
    {
        var vm = MakeGoto(0x10);

        vm.SnesText = "zzz";
        vm.CanConfirm.Should().BeFalse();

        vm.SnesText = "C00040";

        vm.CanConfirm.Should().BeTrue();
        vm.ValidationMessage.Should().BeEmpty();
        vm.ResultPcOffset.Should().Be(0x40);
    }

    [Fact]
    public void EditingRenotifiesTheValidationProperties()
    {
        var vm = MakeGoto(0x10);
        var raised = RecordNotifications(vm);

        vm.SnesText = "zzz";

        raised.Should().Contain(nameof(GotoViewModel.CanConfirm));
        raised.Should().Contain(nameof(GotoViewModel.ValidationMessage));
        raised.Should().Contain(nameof(GotoViewModel.ResultPcOffset));
    }

    // ------------------------------------------------------------------
    // the -1 sentinel stays in the parsing layer
    // ------------------------------------------------------------------

    [Fact]
    public void AnAddressThatMapsNowhereShowsUpAsAnUnusableOffsetNotAsMinusOne()
    {
        var vm = MakeGoto(0x10);

        vm.SnesText = "7E0000";

        // the conversion really did answer -1, and the offset box shows that number the way
        // it shows any other. What must NOT happen is -1 escaping as a destination.
        vm.PcText.Should().Be("FFFFFFFF");
        vm.ResultPcOffset.Should().BeNull("-1 is a parser answer, never a place to go");
    }

    [Fact]
    public void HexTextThatWouldReadAsANegativeNumberIsNotAccepted()
    {
        var vm = MakeGoto(0x10);

        vm.PcText = "FFFFFFFF";

        vm.PcText.Should().Be("FFFFFFFF", "kept as typed, because it was never accepted");
        vm.SnesText.Should().Be("C00010", "and nothing moved");
        vm.ResultPcOffset.Should().BeNull();
    }

    [Theory]
    [InlineData(true, "-20", "20", 0x20)]
    [InlineData(false, "-32", "32", 32)]
    public void AMinusSignIsStrippedRatherThanMakingTheNumberNegative(
        bool useHexadecimal, string typed, string expectedText, int expectedOffset)
    {
        // documenting what the shared address parser does: a leading '-' is punctuation and
        // gets filtered out with everything else, so the box ends up showing the positive
        // number that was actually accepted. No negative offset can reach a caller.
        var vm = MakeGoto(0x10);
        vm.UseHexadecimal = useHexadecimal;

        vm.PcText = typed;

        vm.PcText.Should().Be(expectedText);
        vm.ResultPcOffset.Should().Be(expectedOffset);
    }

    // ------------------------------------------------------------------
    // marshaller discipline
    // ------------------------------------------------------------------

    [Fact]
    public void EveryNotificationGoesThroughTheMarshaller()
    {
        // the thread rule: a host's marshaller is the only path to the UI thread, so nothing
        // may be raised around it.
        var queued = new List<Action>();
        var vm = MakeGoto(0x10, marshaller: queued.Add);
        var raised = RecordNotifications(vm);

        vm.SnesText = "C00020";
        vm.UseHexadecimal = false;

        raised.Should().BeEmpty("nothing may bypass the marshaller");
        queued.Should().NotBeEmpty();

        foreach (var queuedAction in queued)
            queuedAction();

        raised.Should().Contain(nameof(GotoViewModel.PcText));
        raised.Should().Contain(nameof(GotoViewModel.UseHexadecimal));
        raised.Should().Contain(nameof(GotoViewModel.CanConfirm));
    }

    [Fact]
    public void AWithheldNotificationStaysWithheldEvenWhenTheMarshallerDefers()
    {
        var queued = new List<Action>();
        var vm = MakeGoto(0x10, marshaller: queued.Add);
        var raised = RecordNotifications(vm);

        vm.SnesText = "C00020";
        foreach (var queuedAction in queued)
            queuedAction();

        raised.Should().NotContain(nameof(GotoViewModel.SnesText),
            "deferring the notifications must not resurrect the one the caret rule withheld");
    }

    [Fact]
    public void StateIsReadableBeforeADeferredMarshallerHasRunAnything()
    {
        var queued = new List<Action>();
        var vm = MakeGoto(0x10, marshaller: queued.Add);

        vm.SnesText = "C00040";

        vm.PcText.Should().Be("40", "the state changes immediately; only the telling is deferred");
        vm.ResultPcOffset.Should().Be(0x40);
        queued.Should().NotBeEmpty();
    }
}

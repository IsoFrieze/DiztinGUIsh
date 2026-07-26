using System;
using System.Collections.Generic;
using Diz.Core.Interfaces;
using Diz.Ui.ViewModels.MarkMany;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.MarkMany;

/// <summary>
/// AddressRangeViewModel: start/end/count over a ROM, with SNES-vs-ROM-offset and hex-vs-dec
/// display. Uses a deliberately simple address converter (SNES = $C00000 + offset) so the
/// expected numbers are obvious by inspection and the test is about the range logic, not
/// about any particular ROM mapping.
/// </summary>
public class AddressRangeViewModelTests
{
    private const int RomSize = 0x100;
    private const int SnesBase = 0xC00000;

    private sealed class OffsetConverter : ISnesAddressConverter
    {
        // -1 is what this codebase uses everywhere for "that address isn't in this ROM"
        public int ConvertPCtoSnes(int offset) => offset < 0 ? -1 : SnesBase + offset;

        public int ConvertSnesToPc(int address) =>
            address >= SnesBase && address < SnesBase + RomSize ? address - SnesBase : -1;
    }

    private static AddressRangeViewModel MakeRange(
        int start = 0x10, int count = 0x10, Action<Action>? marshaller = null)
    {
        var range = new AddressRangeViewModel(new OffsetConverter(), RomSize, marshaller);
        range.SetRange(start, count);
        return range;
    }

    // ------------------------------------------------------------------
    // construction
    // ------------------------------------------------------------------

    [Fact]
    public void SetRangeSeedsStartEndAndCount()
    {
        var range = MakeRange(0x20, 8);

        range.StartIndex.Should().Be(0x20);
        range.Count.Should().Be(8);
        range.EndIndex.Should().Be(0x27, "end is the last byte IN the range, not one past it");
        range.RomSize.Should().Be(RomSize);
    }

    [Fact]
    public void ARangeNeedsAtLeastOneByteOfRom()
    {
        var make = () => new AddressRangeViewModel(new OffsetConverter(), 0);
        make.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ------------------------------------------------------------------
    // mutual update
    // ------------------------------------------------------------------

    [Fact]
    public void SettingStartLeavesEndAloneAndChangesCount()
    {
        var range = MakeRange(0x10, 0x10); // end = 0x1F

        range.StartIndex = 0x18;

        range.EndIndex.Should().Be(0x1F);
        range.Count.Should().Be(8);
    }

    [Fact]
    public void SettingEndLeavesStartAloneAndChangesCount()
    {
        var range = MakeRange(0x10, 0x10);

        range.EndIndex = 0x2F;

        range.StartIndex.Should().Be(0x10);
        range.Count.Should().Be(0x20);
    }

    [Fact]
    public void SettingCountLeavesStartAloneAndMovesEnd()
    {
        var range = MakeRange(0x10, 0x10);

        range.Count = 4;

        range.StartIndex.Should().Be(0x10);
        range.EndIndex.Should().Be(0x13);
    }

    [Fact]
    public void MovingStartPastEndCollapsesTheRangeToOneByte()
    {
        var range = MakeRange(0x10, 0x10); // end = 0x1F

        range.StartIndex = 0x40;

        range.StartIndex.Should().Be(0x40);
        range.Count.Should().Be(1);
        range.EndIndex.Should().Be(0x40);
    }

    [Fact]
    public void MovingEndBeforeStartCollapsesTheRangeToOneByte()
    {
        var range = MakeRange(0x40, 0x10);

        range.EndIndex = 0x10;

        range.StartIndex.Should().Be(0x40);
        range.Count.Should().Be(1);
    }

    // ------------------------------------------------------------------
    // clamping / edges
    // ------------------------------------------------------------------

    [Fact]
    public void ACountThatWouldRunOffTheEndOfTheRomIsTrimmedToFit()
    {
        var range = MakeRange(RomSize - 4, 1);

        range.Count = 0x1000;

        range.Count.Should().Be(4);
        range.EndIndex.Should().Be(RomSize - 1);
    }

    [Fact]
    public void ASeededRangeThatOverflowsTheRomIsTrimmedToFit()
    {
        var range = MakeRange(RomSize - 2, 0x1000);

        range.StartIndex.Should().Be(RomSize - 2);
        range.Count.Should().Be(2);
        range.EndIndex.Should().Be(RomSize - 1);
    }

    [Fact]
    public void AStartPastTheEndOfTheRomLandsOnTheLastByte()
    {
        var range = MakeRange();

        range.StartIndex = RomSize + 500;

        range.StartIndex.Should().Be(RomSize - 1);
    }

    [Fact]
    public void ANegativeStartLandsOnTheFirstByte()
    {
        var range = MakeRange(0x20, 8);

        range.StartIndex = -50;

        range.StartIndex.Should().Be(0);
    }

    [Fact]
    public void TheRangeCanCoverTheWholeRom()
    {
        var range = MakeRange(0, RomSize);

        range.StartIndex.Should().Be(0);
        range.EndIndex.Should().Be(RomSize - 1);
        range.Count.Should().Be(RomSize);
        range.StartSnesAddress.Should().Be(SnesBase);
        range.EndSnesAddress.Should().Be(SnesBase + RomSize - 1);
    }

    [Fact]
    public void ANonPositiveCountEmptiesTheRangeAndEndBecomesMinusOne()
    {
        var range = MakeRange(0x20, 8);

        range.Count = 0;

        range.Count.Should().Be(0);
        range.EndIndex.Should().Be(-1);
    }

    // ------------------------------------------------------------------
    // text projections
    // ------------------------------------------------------------------

    [Fact]
    public void SnesAddressesInHexArePaddedToSixDigitsAndRomOffsetsAreNot()
    {
        var range = MakeRange(0x10, 0x10);

        range.UseSnesAddresses = true;
        range.UseHexadecimal = true;
        range.StartText.Should().Be("C00010");
        range.EndText.Should().Be("C0001F");
        range.CountText.Should().Be("10", "a count is a byte count, never an address");

        range.UseSnesAddresses = false;
        range.StartText.Should().Be("10");
        range.EndText.Should().Be("1F");
    }

    [Fact]
    public void DecimalTextIsNeverPadded()
    {
        var range = MakeRange(0x10, 0x10);
        range.UseHexadecimal = false;

        range.UseSnesAddresses = true;
        range.StartText.Should().Be((SnesBase + 0x10).ToString());

        range.UseSnesAddresses = false;
        range.StartText.Should().Be("16");
        range.CountText.Should().Be("16");
    }

    [Theory]
    [InlineData(true, true, "C00020", 0x20)]   // snes + hex
    [InlineData(false, true, "20", 0x20)]      // rom offset + hex
    [InlineData(false, false, "32", 0x20)]     // rom offset + decimal
    public void SettingStartTextParsesInTheCurrentBaseAndAddressSpace(
        bool useSnes, bool useHex, string text, int expectedStartIndex)
    {
        var range = MakeRange(0x10, 0x40);
        range.UseSnesAddresses = useSnes;
        range.UseHexadecimal = useHex;

        range.StartText = text;

        range.StartIndex.Should().Be(expectedStartIndex);
    }

    [Fact]
    public void SettingEndTextAndCountTextParseTheSameWay()
    {
        var range = MakeRange(0x10, 0x10);

        range.EndText = "C0002F";
        range.EndIndex.Should().Be(0x2F);
        range.StartIndex.Should().Be(0x10);

        range.CountText = "8";
        range.Count.Should().Be(8);
        range.EndIndex.Should().Be(0x17);
    }

    [Fact]
    public void TextThatIsNotANumberIsIgnoredEntirely()
    {
        var range = MakeRange(0x10, 0x10);

        range.StartText = "not a number";
        range.EndText = "";
        range.CountText = "zzz";

        range.StartIndex.Should().Be(0x10);
        range.Count.Should().Be(0x10);
    }

    [Fact]
    public void ASnesAddressThatIsNotInThisRomIsIgnoredEntirely()
    {
        var range = MakeRange(0x10, 0x10);
        range.UseSnesAddresses = true;

        // $7E0000 is SNES WRAM: a real address, but it is not part of the ROM, so the
        // converter answers -1 and there is no offset to move to.
        range.StartText = "7E0000";

        range.StartIndex.Should().Be(0x10);
        range.Count.Should().Be(0x10);
    }

    [Fact]
    public void AnEmptyRangeShowsTheConverterAnswerForOffsetMinusOne()
    {
        // documenting an edge, not endorsing it: with no bytes selected there is no end
        // address, and end index -1 converts to -1 (shown as the hex of -1).
        var range = MakeRange(0x20, 0);

        range.EndIndex.Should().Be(-1);
        range.EndSnesAddress.Should().Be(-1);
        range.EndText.Should().Be("FFFFFFFF");
    }

    // ------------------------------------------------------------------
    // notifications
    // ------------------------------------------------------------------

    [Fact]
    public void EditingOneTextFieldRefreshesTheOtherTwoButNotItself()
    {
        var range = MakeRange(0x10, 0x10);
        var raised = new List<string>();
        range.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        range.StartText = "C00018";

        raised.Should().Contain(nameof(AddressRangeViewModel.EndText));
        raised.Should().Contain(nameof(AddressRangeViewModel.CountText));
        raised.Should().NotContain(nameof(AddressRangeViewModel.StartText),
            "rewriting the field being typed in would fight the caret");
    }

    [Fact]
    public void ChangingTheDisplayTogglesRefreshesAllThreeTextFields()
    {
        var range = MakeRange(0x10, 0x10);
        var raised = new List<string>();
        range.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        range.UseHexadecimal = false;

        raised.Should().Contain(nameof(AddressRangeViewModel.StartText));
        raised.Should().Contain(nameof(AddressRangeViewModel.EndText));
        raised.Should().Contain(nameof(AddressRangeViewModel.CountText));
        raised.Should().Contain(nameof(AddressRangeViewModel.AddressTextDigitCount));
    }

    [Fact]
    public void EveryNotificationGoesThroughTheMarshaller()
    {
        // the thread rule: a host's marshaller is the only path to the UI thread, so nothing
        // may be raised around it.
        var queued = new List<Action>();
        var range = MakeRange(0x10, 0x10, marshaller: queued.Add);

        var raised = new List<string>();
        range.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        range.StartIndex = 0x18;

        raised.Should().BeEmpty("nothing may bypass the marshaller");
        queued.Should().NotBeEmpty();

        foreach (var queuedAction in queued)
            queuedAction();

        raised.Should().Contain(nameof(AddressRangeViewModel.StartIndex));
    }
}

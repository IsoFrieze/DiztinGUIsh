using System.Globalization;
using Diz.Core;
using Diz.Core.Interfaces;
using Diz.Core.util;

namespace Diz.Ui.ViewModels.MarkMany;

/// <summary>
/// A start / end / count byte range over a ROM, with the two display toggles the range
/// widgets in this app have always had: SNES address vs ROM file offset, and hexadecimal
/// vs decimal. Reusable: anything that asks the user for "a run of bytes" wants this.
///
/// Canonical state is a <see cref="CorrectingRange"/> holding ROM file offsets (PC offsets),
/// which enforces the invariants (start inside the ROM, start+count never past the end).
/// Everything else here is a projection of that: the SNES addresses and the three text
/// properties are computed on read and parsed on write.
///
/// Mutual-update rule (this is a deliberate UX choice, not what a raw range object does):
/// everything is expressed in terms of the START index.
///   - set START -> END stays put, COUNT changes
///   - set END   -> START stays put, COUNT changes
///   - set COUNT -> START stays put, END changes
/// A change that would make COUNT negative yields COUNT = 1 instead.
/// </summary>
public sealed class AddressRangeViewModel : ViewModelNotifierBase
{
    private readonly ISnesAddressConverter addressConverter;
    private readonly IDataRange range;

    private bool useSnesAddresses = true;
    private bool useHexadecimal = true;

    /// <param name="addressConverter">Converts between ROM file offsets and SNES addresses.</param>
    /// <param name="romSize">Total ROM size in bytes; the range can never leave [0, romSize).</param>
    /// <param name="notificationMarshaller">See <see cref="ViewModelNotifierBase"/>.</param>
    public AddressRangeViewModel(
        ISnesAddressConverter addressConverter,
        int romSize,
        Action<Action>? notificationMarshaller = null)
        : base(notificationMarshaller)
    {
        ArgumentNullException.ThrowIfNull(addressConverter);
        if (romSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(romSize), romSize,
                "A range needs at least one byte of ROM to describe.");
        }

        this.addressConverter = addressConverter;
        range = new CorrectingRange(romSize);
    }

    /// <summary>Total ROM size the range is clamped to.</summary>
    public int RomSize => range.MaxCount;

    // ------------------------------------------------------------------
    // canonical state (ROM file offsets)
    // ------------------------------------------------------------------

    /// <summary>First ROM file offset in the range. Setting it leaves END alone and changes COUNT.</summary>
    public int StartIndex
    {
        get => range.StartIndex;
        set => ApplyStartIndex(value);
    }

    /// <summary>
    /// Last ROM file offset in the range, INCLUSIVE. -1 when the range is empty.
    /// Setting it leaves START alone and changes COUNT.
    /// </summary>
    public int EndIndex
    {
        get => range.EndIndex;
        set => ApplyEndIndex(value);
    }

    /// <summary>Number of bytes in the range. Setting it leaves START alone and changes END.</summary>
    public int Count
    {
        get => range.RangeCount;
        set => ApplyCount(value);
    }

    /// <summary>Set start and count together (used when a host seeds the range from a selection).</summary>
    public void SetRange(int startIndex, int count)
    {
        range.ManualUpdate(startIndex, count);
        RaiseRangeChanged();
    }

    /// <summary>SNES address of <see cref="StartIndex"/>.</summary>
    public int StartSnesAddress => addressConverter.ConvertPCtoSnes(range.StartIndex);

    /// <summary>SNES address of <see cref="EndIndex"/> (converted as-is when the range is empty).</summary>
    public int EndSnesAddress => addressConverter.ConvertPCtoSnes(range.EndIndex);

    // ------------------------------------------------------------------
    // display toggles
    // ------------------------------------------------------------------

    /// <summary>
    /// true: start/end text is a SNES address. false: it is a ROM file offset.
    /// Only affects START and END -- COUNT is a byte count either way.
    /// </summary>
    public bool UseSnesAddresses
    {
        get => useSnesAddresses;
        set
        {
            if (useSnesAddresses == value)
                return;
            useSnesAddresses = value;
            OnPropertyChanged(nameof(UseSnesAddresses));
            OnPropertyChanged(nameof(AddressTextDigitCount));
            RaiseTextChanged();
        }
    }

    /// <summary>true: text is read and written as hexadecimal. false: decimal.</summary>
    public bool UseHexadecimal
    {
        get => useHexadecimal;
        set
        {
            if (useHexadecimal == value)
                return;
            useHexadecimal = value;
            OnPropertyChanged(nameof(UseHexadecimal));
            OnPropertyChanged(nameof(AddressTextDigitCount));
            RaiseTextChanged();
        }
    }

    /// <summary>
    /// Zero-padding width for the start/end text: SNES addresses in hex are padded to the
    /// usual 6 digits, everything else is unpadded.
    /// </summary>
    public int AddressTextDigitCount => useHexadecimal && useSnesAddresses ? 6 : 0;

    // ------------------------------------------------------------------
    // text projections
    // ------------------------------------------------------------------

    /// <summary>
    /// START as text, in the current base and address space. Assigning unparseable text
    /// leaves the range untouched (the other two text properties still re-notify, so a view
    /// bound to them stays consistent).
    /// </summary>
    public string StartText
    {
        get => NumberToText(DisplayedAddress(range.StartIndex), AddressTextDigitCount);
        set
        {
            if (TryParseAddress(value, out var offset))
                ApplyStartIndex(offset, notify: false);

            RaiseRangeChanged(exceptTextProperty: nameof(StartText));
        }
    }

    /// <summary>END as text. See <see cref="StartText"/>.</summary>
    public string EndText
    {
        get => NumberToText(DisplayedAddress(range.EndIndex), AddressTextDigitCount);
        set
        {
            if (TryParseAddress(value, out var offset))
                ApplyEndIndex(offset, notify: false);

            RaiseRangeChanged(exceptTextProperty: nameof(EndText));
        }
    }

    /// <summary>COUNT as text -- always a plain byte count, never an address. See <see cref="StartText"/>.</summary>
    public string CountText
    {
        get => NumberToText(range.RangeCount, 0);
        set
        {
            if (TryParseNumber(value, out var count))
                ApplyCount(count, notify: false);

            RaiseRangeChanged(exceptTextProperty: nameof(CountText));
        }
    }

    // ------------------------------------------------------------------
    // internals
    // ------------------------------------------------------------------

    private void ApplyStartIndex(int newStartIndex, bool notify = true)
    {
        // changing START: leave END where it is, adjust the byte count to match.
        var updatedCount = range.EndIndex - newStartIndex + 1;
        if (updatedCount < 0)
            updatedCount = 1;

        range.ManualUpdate(newStartIndex, updatedCount);

        if (notify)
            RaiseRangeChanged();
    }

    private void ApplyEndIndex(int newEndIndex, bool notify = true)
    {
        // changing END: leave START where it is, adjust the byte count to match.
        var updatedCount = newEndIndex - range.StartIndex + 1;
        if (updatedCount < 0)
            updatedCount = 1;

        range.ManualUpdate(range.StartIndex, updatedCount);

        if (notify)
            RaiseRangeChanged();
    }

    private void ApplyCount(int newCount, bool notify = true)
    {
        // changing COUNT: leave START where it is, END follows.
        range.ManualUpdate(range.StartIndex, newCount);

        if (notify)
            RaiseRangeChanged();
    }

    private int DisplayedAddress(int romOffset) =>
        useSnesAddresses ? addressConverter.ConvertPCtoSnes(romOffset) : romOffset;

    private string NumberToText(int value, int digitCount) =>
        Util.NumberToBaseString(
            value,
            useHexadecimal ? Util.NumberBase.Hexadecimal : Util.NumberBase.Decimal,
            digitCount);

    private bool TryParseNumber(string? text, out int result) =>
        int.TryParse(
            text,
            useHexadecimal ? NumberStyles.HexNumber : NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out result);

    /// <summary>
    /// Parse start/end text into a ROM file offset. Returns false -- meaning "ignore this
    /// input entirely" -- when the text isn't a number, and also when it is a SNES address
    /// that maps nowhere in this ROM (the converter answers -1 for those).
    /// </summary>
    private bool TryParseAddress(string? text, out int romOffset)
    {
        romOffset = -1;
        if (!TryParseNumber(text, out var parsed))
            return false;

        romOffset = useSnesAddresses ? addressConverter.ConvertSnesToPc(parsed) : parsed;
        return romOffset != -1;
    }

    private void RaiseRangeChanged(string? exceptTextProperty = null)
    {
        OnPropertyChanged(nameof(StartIndex));
        OnPropertyChanged(nameof(EndIndex));
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(StartSnesAddress));
        OnPropertyChanged(nameof(EndSnesAddress));
        RaiseTextChanged(exceptTextProperty);
    }

    /// <summary>
    /// Re-notify the text projections. <paramref name="exceptTextProperty"/> is skipped: when
    /// the user is mid-edit in one field, pushing a reformatted value back into that same
    /// field would fight the caret. The other fields must still update.
    /// </summary>
    private void RaiseTextChanged(string? exceptTextProperty = null)
    {
        if (exceptTextProperty != nameof(StartText))
            OnPropertyChanged(nameof(StartText));
        if (exceptTextProperty != nameof(EndText))
            OnPropertyChanged(nameof(EndText));
        if (exceptTextProperty != nameof(CountText))
            OnPropertyChanged(nameof(CountText));
    }
}

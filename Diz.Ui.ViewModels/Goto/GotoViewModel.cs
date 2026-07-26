using System.Globalization;
using Diz.Core.Interfaces;
using Diz.Core.util;

namespace Diz.Ui.ViewModels.Goto;

/// <summary>
/// "Go to a location in the ROM": the user types either a SNES address or a ROM file offset
/// and the caller navigates there.
///
/// Both text projections are visible at the same time and each is the other's mirror --
/// accepting one rewrites the other -- which is why this does not reuse the start/end/count
/// range ViewModel: that one projects a single number in one base at a time behind radio
/// buttons.
///
/// This ViewModel decides WHERE to go and never goes there. Whoever opened it reads
/// <see cref="ResultPcOffset"/> afterwards and performs its own navigation, so nothing here
/// needs to know that a window exists.
///
/// STATE IS THE TWO STRINGS, not one number, because the two boxes are allowed to disagree:
/// text that does not parse stays on screen exactly as typed while the other box keeps its
/// last good value. <see cref="Validate"/> decides whether the pair currently means anything,
/// and no result can be read until it does.
///
/// CARET RULE (differs from the range ViewModel's): assigning one text property always
/// re-notifies the other one, and re-notifies the assigned one ONLY when what was accepted
/// differs from what was assigned. Accepting can change the text because the parser strips
/// decoration -- pasting the label "CODE_C012AB" leaves "C012AB" in the box -- and that
/// rewrite has to reach the view. Anything else would reformat the field under the caret.
///
/// The -1 that this codebase uses for "that address is not in this ROM" never leaves the
/// parsing helpers at the bottom of this file: every number that escapes them is a real
/// offset, and "there is no number" is expressed as false, or as a null result.
/// </summary>
public sealed class GotoViewModel : ViewModelNotifierBase
{
    /// <summary>Reported when the ROM file offset box does not name a byte in this ROM.</summary>
    public const string InvalidPcOffsetMessage = "Invalid ROM File Offset";

    /// <summary>Reported when the SNES address box does not map to a byte in this ROM.</summary>
    public const string InvalidSnesAddressMessage = "Invalid SNES Address";

    /// <summary>
    /// SNES addresses are written zero-padded to six digits, in whichever base is selected.
    /// ROM file offsets are written unpadded.
    /// </summary>
    public const int SnesTextDigitCount = 6;

    private readonly ISnesAddressConverter addressConverter;
    private readonly int romSize;

    private string snesText;
    private string pcText;
    private bool useHexadecimal = true;

    /// <param name="addressConverter">Converts between ROM file offsets and SNES addresses.</param>
    /// <param name="romSize">Total ROM size in bytes; a destination must land inside [0, romSize).</param>
    /// <param name="startPcOffset">ROM file offset the two boxes start out describing.</param>
    /// <param name="notificationMarshaller">See <see cref="ViewModelNotifierBase"/>.</param>
    public GotoViewModel(
        ISnesAddressConverter addressConverter,
        int romSize,
        int startPcOffset,
        Action<Action>? notificationMarshaller = null)
        : base(notificationMarshaller)
    {
        ArgumentNullException.ThrowIfNull(addressConverter);
        if (romSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(romSize), romSize,
                "There is nowhere to go in a ROM with no bytes in it.");
        }

        this.addressConverter = addressConverter;
        this.romSize = romSize;

        // seeded, not clamped: an offset outside the ROM shows up as an invalid starting
        // state rather than silently becoming a different destination.
        snesText = AddressToText(addressConverter.ConvertPCtoSnes(startPcOffset));
        pcText = OffsetToText(startPcOffset);
    }

    // ------------------------------------------------------------------
    // text projections
    // ------------------------------------------------------------------

    /// <summary>
    /// The SNES address, as text. Assigning text that parses to an address moves the ROM file
    /// offset to match; assigning text that does not parse leaves the offset alone and keeps
    /// the text exactly as given, so the user can carry on editing it.
    /// </summary>
    public string SnesText
    {
        get => snesText;
        set
        {
            var assigned = value ?? "";

            if (TryAcceptTypedText(assigned, out var accepted, out var snesAddress))
            {
                snesText = accepted;

                // the converter answers -1 for an address that is not in this ROM. That is
                // shown, not swallowed: the offset box then reads as invalid and says so.
                pcText = OffsetToText(addressConverter.ConvertSnesToPc(snesAddress));
            }
            else
            {
                snesText = assigned;
            }

            RaiseStateChanged(TextToWithhold(nameof(SnesText), snesText, assigned));
        }
    }

    /// <summary>The ROM file offset, as text. Mirror of <see cref="SnesText"/>.</summary>
    public string PcText
    {
        get => pcText;
        set
        {
            var assigned = value ?? "";

            if (TryAcceptTypedText(assigned, out var accepted, out var pcOffset))
            {
                pcText = accepted;
                snesText = AddressToText(addressConverter.ConvertPCtoSnes(pcOffset));
            }
            else
            {
                pcText = assigned;
            }

            RaiseStateChanged(TextToWithhold(nameof(PcText), pcText, assigned));
        }
    }

    /// <summary>
    /// true: both boxes are read and written as hexadecimal. false: decimal.
    ///
    /// Flipping this re-expresses BOTH boxes in the new base, but only when the current state
    /// is valid -- text that means nothing is left exactly as typed rather than being
    /// half-converted into a different base. Each box is re-expressed from its own number, so
    /// a SNES address in a mirrored bank keeps the bank the user typed.
    /// </summary>
    public bool UseHexadecimal
    {
        get => useHexadecimal;
        set
        {
            if (useHexadecimal == value)
                return;

            // read both numbers in the base being left behind, before the switch.
            int snesAddress = 0, pcOffset = 0;
            var reExpress =
                Validate().IsValid &&
                TryReadNumber(snesText, out snesAddress) &&
                TryReadNumber(pcText, out pcOffset);

            useHexadecimal = value;

            if (reExpress)
            {
                snesText = AddressToText(snesAddress);
                pcText = OffsetToText(pcOffset);
            }

            OnPropertyChanged(nameof(UseHexadecimal));
            RaiseStateChanged();
        }
    }

    // ------------------------------------------------------------------
    // validation and result
    // ------------------------------------------------------------------

    /// <summary>
    /// Why the two boxes do not currently name a place to go, or IsValid.
    ///
    /// When both boxes are bad the SNES address is the one reported: it is the field the user
    /// is most likely to have typed into, and only one message fits on screen.
    /// </summary>
    public ValidationResult Validate()
    {
        if (!TryReadSnesAddressAsOffset(out _))
            return ValidationResult.Fail(InvalidSnesAddressMessage);

        if (!TryReadPcOffset(out _))
            return ValidationResult.Fail(InvalidPcOffsetMessage);

        return ValidationResult.Ok;
    }

    /// <summary>
    /// True when <see cref="ResultPcOffset"/> names a real destination. A host must refuse to
    /// confirm while this is false -- including on Enter, not just on the confirm button.
    /// </summary>
    public bool CanConfirm => Validate().IsValid;

    /// <summary>The validation message to display, or empty when there is nothing wrong.</summary>
    public string ValidationMessage => Validate().Error ?? "";

    /// <summary>
    /// The ROM file offset to navigate to, or null when the current state does not name one
    /// (equivalently: whenever <see cref="CanConfirm"/> is false).
    /// </summary>
    public int? ResultPcOffset => CanConfirm && TryReadPcOffset(out var pcOffset) ? pcOffset : null;

    // ------------------------------------------------------------------
    // reading the boxes
    // ------------------------------------------------------------------

    private bool TryReadPcOffset(out int pcOffset) =>
        TryReadNumber(pcText, out pcOffset) && IsInsideRom(pcOffset);

    /// <summary>The SNES box, converted to the ROM file offset it names.</summary>
    private bool TryReadSnesAddressAsOffset(out int pcOffset)
    {
        pcOffset = 0;
        if (!TryReadNumber(snesText, out var snesAddress))
            return false;

        var converted = addressConverter.ConvertSnesToPc(snesAddress);
        if (!IsInsideRom(converted))
            return false;

        pcOffset = converted;
        return true;
    }

    private bool IsInsideRom(int pcOffset) => pcOffset >= 0 && pcOffset < romSize;

    /// <summary>
    /// Accept text the user typed: strip the decoration this codebase's addresses tend to
    /// arrive wrapped in, then read the number. <paramref name="accepted"/> is the text that
    /// belongs in the box afterwards, and is only meaningful when this returns true.
    /// </summary>
    private bool TryAcceptTypedText(string text, out string accepted, out int value)
    {
        accepted = text;
        value = 0;

        // rewrites `accepted` in place: pulls the address out of a label ("CODE_C012AB" ->
        // "C012AB") and drops punctuation ("$C6/BBBB" -> "C6BBBB"). Pasting either of those
        // straight out of the disassembly has always worked and has to keep working.
        if (!ByteUtil.TryParseNum_Stripped(ref accepted, NumberStyle, out _))
            return false;

        // read the stripped text again here rather than trusting the number that helper
        // produced: it parses with the ambient culture, and an address has to mean the same
        // number on every machine.
        return TryReadNumber(accepted, out value);
    }

    /// <summary>
    /// Read text as a number in the current base. False means "there is no usable number
    /// here", which covers unparseable text and negative values alike.
    /// </summary>
    private bool TryReadNumber(string text, out int value) =>
        int.TryParse(text, NumberStyle, CultureInfo.InvariantCulture, out value) && value >= 0;

    private NumberStyles NumberStyle => useHexadecimal ? NumberStyles.HexNumber : NumberStyles.Number;

    // ------------------------------------------------------------------
    // writing the boxes
    // ------------------------------------------------------------------

    private string AddressToText(int snesAddress) => NumberToText(snesAddress, SnesTextDigitCount);

    private string OffsetToText(int pcOffset) => NumberToText(pcOffset, 0);

    private string NumberToText(int value, int digitCount) =>
        Util.NumberToBaseString(
            value,
            useHexadecimal ? Util.NumberBase.Hexadecimal : Util.NumberBase.Decimal,
            digitCount);

    // ------------------------------------------------------------------
    // notifications
    // ------------------------------------------------------------------

    /// <summary>
    /// The name of the text property to withhold a notification for: the one just assigned,
    /// unless accepting it changed the text, in which case the view has to be told.
    /// </summary>
    private static string? TextToWithhold(string propertyName, string stored, string assigned) =>
        string.Equals(stored, assigned, StringComparison.Ordinal) ? propertyName : null;

    private void RaiseStateChanged(string? exceptTextProperty = null)
    {
        if (exceptTextProperty != nameof(SnesText))
            OnPropertyChanged(nameof(SnesText));
        if (exceptTextProperty != nameof(PcText))
            OnPropertyChanged(nameof(PcText));

        OnPropertyChanged(nameof(CanConfirm));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(ResultPcOffset));
    }
}

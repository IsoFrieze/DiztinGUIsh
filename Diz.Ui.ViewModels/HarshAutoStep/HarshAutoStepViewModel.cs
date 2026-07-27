using Diz.Core.commands;
using Diz.Core.Interfaces;
using Diz.Core.util;
using Diz.Ui.ViewModels.MarkMany;

namespace Diz.Ui.ViewModels.HarshAutoStep;

/// <summary>
/// "Harsh auto step": pick a run of bytes and decode all of it as instructions, ignoring
/// control flow. Produces an <see cref="AutoStepHarshCommand"/> describing that request.
///
/// This ViewModel BUILDS the command and never applies it. Applying is
/// SnesApiExtensions.ApplyAutoStepHarshCommand in Diz.Cpu.65816 -- an assembly this one is not
/// allowed to reference, and the separation is the point: the same command can come from a
/// window, a script, or an external API, and one applier handles all of them.
///
/// The whole user-facing surface is the range, so this type is deliberately thin: it owns an
/// <see cref="AddressRangeViewModel"/>, seeds it, and answers whether what the range currently
/// holds can be turned into a command. Keeping even that much here rather than in the hosts
/// means the seeding rule, the validation rule, and the command shape exist once instead of
/// once per toolkit.
///
/// END IS INCLUSIVE, because <see cref="AddressRangeViewModel"/>'s is: the range $8000..$80FF
/// is $100 bytes. The number that reaches the algorithm is a plain byte count either way.
///
/// Hosts that need to react to range edits (refreshing a validation message, enabling a
/// confirm button) subscribe to <see cref="Range"/>'s own PropertyChanged -- this type raises
/// nothing of its own, because it has no state of its own.
/// </summary>
public sealed class HarshAutoStepViewModel : ViewModelNotifierBase
{
    /// <summary>
    /// How many bytes the range covers when a window first opens. A round $100 of decoding is
    /// enough to see whether a stretch of bytes is plausibly code without committing to much.
    /// Clamped down only when the ROM genuinely ends first.
    /// </summary>
    public const int DefaultCount = 0x100;

    /// <summary>Reported when the range holds no bytes, so stepping would do nothing.</summary>
    public const string EmptyRangeMessage = "The range is empty: pick at least one byte to step through.";

    /// <param name="addressConverter">Converts between ROM file offsets and SNES addresses.</param>
    /// <param name="romSize">Total ROM size in bytes; the range can never leave [0, romSize).</param>
    /// <param name="startPcOffset">
    /// ROM file offset the range starts at. Clamped into the ROM -- unlike a single-destination
    /// ViewModel, which can show an out-of-ROM seed as invalid, a range has to be a range.
    /// </param>
    /// <param name="notificationMarshaller">See <see cref="ViewModelNotifierBase"/>.</param>
    public HarshAutoStepViewModel(
        ISnesAddressConverter addressConverter,
        int romSize,
        int startPcOffset,
        Action<Action>? notificationMarshaller = null)
        : base(notificationMarshaller)
    {
        ArgumentNullException.ThrowIfNull(addressConverter);

        // the child range shares this VM's marshaller, so one host-supplied hop to the UI
        // thread covers the whole tree.
        Range = new AddressRangeViewModel(addressConverter, romSize, NotificationMarshaller);
        Range.SetRange(startPcOffset, DefaultCount);
    }

    /// <summary>The run of bytes to step through.</summary>
    public AddressRangeViewModel Range { get; }

    /// <summary>Why the current range can't be turned into a command, or IsValid.</summary>
    public ValidationResult Validate() =>
        Range.Count <= 0
            ? ValidationResult.Fail(EmptyRangeMessage)
            : ValidationResult.Ok;

    /// <summary>
    /// True when <see cref="BuildAutoStepHarshCommand"/> will return a command. A host must
    /// refuse to confirm while this is false -- including on Enter, not just on the button.
    /// </summary>
    public bool CanBuildAutoStepCommand => Validate().IsValid;

    /// <summary>The validation message to display, or empty when there is nothing wrong.</summary>
    public string ValidationMessage => Validate().Error ?? "";

    /// <summary>
    /// The command describing what the user asked for, or null if the current state isn't
    /// valid (see <see cref="Validate"/>). Applying it is the caller's job.
    /// </summary>
    public AutoStepHarshCommand? BuildAutoStepHarshCommand() =>
        !CanBuildAutoStepCommand
            ? null
            : new AutoStepHarshCommand
            {
                Start = Range.StartIndex,
                Count = Range.Count,
            };
}

using Diz.Core.Interfaces;
using Diz.Core.util;

namespace Diz.Ui.ViewModels.ImportRom;

/// <summary>
/// The options offered when a SNES ROM is turned into a new project: which memory mapping to
/// read it as, which interrupt vectors to generate labels for, and which extra data to
/// synthesize up front.
///
/// This ViewModel READS NOTHING. Analysing a ROM belongs to the SNES importer, in an assembly
/// this one may not reference, so the caller hands in a delegate that turns a map mode into a
/// <see cref="SnesVectorSnapshot"/> and this type only decides when to call it and what the
/// answer means. Same separation as the misalignment checker's caller-supplied scan.
///
/// It also never asks the user anything. When the choices on screen deserve a warning it says
/// so through <see cref="RequiresConfirmation"/> and <see cref="ConfirmationMessage"/>; whoever
/// is hosting it decides how to put that question, and what to do with the answer.
///
/// THE SELECTED MODE IS THE ONE THAT COUNTS. Detection is a starting suggestion, and the user
/// may overrule it. Everything derived here -- vector values, the cartridge title, whether a
/// warning is warranted -- follows <see cref="SelectedRomMapMode"/>, never the detected mode.
/// A ROM read as HiROM must not be described using LoROM offsets just because detection guessed
/// LoROM first.
/// </summary>
public sealed class SnesImportRomViewModel : ViewModelNotifierBase
{
    /// <summary>Shown in place of the detected mapping when nothing could be detected.</summary>
    public const string DetectionFailedMessage = "Couldn't auto detect ROM Map Mode!";

    /// <summary>
    /// Why the vector column is full of placeholders. The values are unreadable rather than
    /// zero, and saying nothing at all leaves the user guessing at a row of question marks.
    /// </summary>
    public const string VectorsUnreadableMessage =
        "The interrupt vectors can't be read at this ROM map mode -- the values shown are placeholders.";

    /// <summary>Warning text when the ROM's mapping could not be worked out at all.</summary>
    public const string DetectionFailedConfirmationMessage = "ROM Map type couldn't be detected.";

    /// <summary>Warning text when the user overrules a mapping that WAS detected.</summary>
    public const string OverriddenMapModeConfirmationMessage =
        "The ROM map type selected is different than what was detected.";

    private readonly Func<RomMapMode, SnesVectorSnapshot> recomputeForMapMode;

    // guards the one re-entrancy hazard here: applying a snapshot writes to every row, and a
    // host binding that echoes those writes back must not start another recompute. See
    // ApplySnapshot.
    private bool applyingSnapshot;

    private RomMapMode selectedRomMapMode;
    private string cartridgeTitle;
    private string statusText = "";
    private bool generateHeaderFlags = true;
    private bool generateBankRegions = true;

    /// <param name="initialSnapshot">
    /// Vector values and cartridge title as read at <paramref name="detectedRomMapMode"/>. Seeds
    /// the rows; their order is kept for the lifetime of this ViewModel.
    /// </param>
    /// <param name="detectedRomMapMode">What analysis thought the ROM's mapping was.</param>
    /// <param name="detectionSucceeded">Whether that guess is worth anything.</param>
    /// <param name="romSpeedText">
    /// The detected ROM speed, already rendered. Display only: the speed has never been
    /// user-overridable, so it does not move when the map mode does.
    /// </param>
    /// <param name="alwaysEnabledVectorNames">
    /// Vectors whose labels are generated unconditionally and which the user cannot switch off
    /// (see <see cref="SnesVectorRowViewModel.IsAlwaysEnabled"/>). Passed in rather than
    /// hardcoded so this type knows nothing about any particular CPU's vector table.
    /// </param>
    /// <param name="initiallyEnabledVectorNames">Vectors ticked when the screen first appears.</param>
    /// <param name="recomputeForMapMode">
    /// Re-reads the vector table and cartridge title at a given map mode. Supplied by the caller
    /// because the ROM analyser lives outside the assemblies this one may reference.
    /// </param>
    /// <param name="notificationMarshaller">See <see cref="ViewModelNotifierBase"/>.</param>
    public SnesImportRomViewModel(
        SnesVectorSnapshot initialSnapshot,
        RomMapMode detectedRomMapMode,
        bool detectionSucceeded,
        string romSpeedText,
        IEnumerable<string> alwaysEnabledVectorNames,
        IEnumerable<string> initiallyEnabledVectorNames,
        Func<RomMapMode, SnesVectorSnapshot> recomputeForMapMode,
        Action<Action>? notificationMarshaller = null)
        : base(notificationMarshaller)
    {
        ArgumentNullException.ThrowIfNull(initialSnapshot);
        ArgumentNullException.ThrowIfNull(alwaysEnabledVectorNames);
        ArgumentNullException.ThrowIfNull(initiallyEnabledVectorNames);
        ArgumentNullException.ThrowIfNull(recomputeForMapMode);

        this.recomputeForMapMode = recomputeForMapMode;

        DetectedRomMapMode = detectedRomMapMode;
        DetectionSucceeded = detectionSucceeded;
        RomSpeedText = romSpeedText ?? "";

        selectedRomMapMode = detectedRomMapMode;
        cartridgeTitle = initialSnapshot.CartridgeTitle ?? "";
        statusText = initialSnapshot.VectorsReadable ? "" : VectorsUnreadableMessage;

        var alwaysEnabled = alwaysEnabledVectorNames.ToHashSet(StringComparer.Ordinal);
        var initiallyEnabled = initiallyEnabledVectorNames.ToHashSet(StringComparer.Ordinal);

        Vectors = initialSnapshot.Vectors
            .Select(vector => new SnesVectorRowViewModel(
                vector.Name,
                vector.DisplayValue,
                isEnabled: vector.IsReadable && initiallyEnabled.Contains(vector.Name),
                isSelectable: vector.IsReadable,
                isAlwaysEnabled: alwaysEnabled.Contains(vector.Name),
                NotificationMarshaller))
            .ToList();
    }

    /// <summary>The mapping analysis guessed at. Only a starting point; see the type summary.</summary>
    public RomMapMode DetectedRomMapMode { get; }

    /// <summary>Whether <see cref="DetectedRomMapMode"/> means anything.</summary>
    public bool DetectionSucceeded { get; }

    /// <summary>The detected ROM speed, rendered. Never changes -- it is not user-overridable.</summary>
    public string RomSpeedText { get; }

    /// <summary>What detection came up with, in words, for display next to the mode picker.</summary>
    public string DetectionMessage =>
        DetectionSucceeded ? Util.GetEnumDescription(DetectedRomMapMode) : DetectionFailedMessage;

    /// <summary>Every mapping the user may pick, in declaration order.</summary>
    public IReadOnlyList<RomMapMode> RomMapModeChoices { get; } = Enum.GetValues<RomMapMode>();

    /// <summary>The vector table, in vector-table order. The row set never changes.</summary>
    public IReadOnlyList<SnesVectorRowViewModel> Vectors { get; }

    /// <summary>
    /// The mapping the ROM will actually be imported as. Changing it re-reads everything that
    /// depends on the mapping, because the vector table and the settings header both move.
    /// </summary>
    public RomMapMode SelectedRomMapMode
    {
        get => selectedRomMapMode;
        set
        {
            // a snapshot being applied writes to the rows; if a host echoes that back to here we
            // must not start a second recompute on top of the first.
            if (applyingSnapshot || value == selectedRomMapMode)
                return;

            selectedRomMapMode = value;
            OnPropertyChanged(nameof(SelectedRomMapMode));

            ApplySnapshot(recomputeForMapMode(value));

            OnPropertyChanged(nameof(RequiresConfirmation));
            OnPropertyChanged(nameof(ConfirmationMessage));
        }
    }

    /// <summary>The cartridge's internal name, read at the selected mapping.</summary>
    public string CartridgeTitle
    {
        get => cartridgeTitle;
        private set => this.SetField(ref cartridgeTitle, value ?? "");
    }

    /// <summary>
    /// One line explaining anything unusual about what is currently on screen, or empty when
    /// there is nothing to say.
    /// </summary>
    public string StatusText
    {
        get => statusText;
        private set => this.SetField(ref statusText, value ?? "");
    }

    /// <summary>Whether to mark the ROM's header bytes as data during import.</summary>
    public bool GenerateHeaderFlags
    {
        get => generateHeaderFlags;
        set => this.SetField(ref generateHeaderFlags, value);
    }

    /// <summary>
    /// Whether to create one whole-bank region per bank up front. On by default, which is what
    /// importing has always done; the switch exists so it can be declined.
    /// </summary>
    public bool GenerateBankRegions
    {
        get => generateBankRegions;
        set => this.SetField(ref generateBankRegions, value);
    }

    /// <summary>
    /// The vectors to generate labels for, in vector-table order. This is what the caller reads
    /// back when the user is done. Always-on rows are in here even when everything the user can
    /// reach has been switched off.
    /// </summary>
    public IReadOnlyList<string> EnabledVectorNames =>
        Vectors.Where(row => row.IsEnabled).Select(row => row.Name).ToList();

    /// <summary>
    /// Whether the current choices are risky enough that the user should be asked to confirm
    /// before importing. This type only reports it -- asking is the host's job.
    /// </summary>
    public bool RequiresConfirmation => ConfirmationMessage != null;

    /// <summary>
    /// What is risky about the current choices, or null when nothing is.
    ///
    /// The two cases are mutually exclusive on purpose: once detection has failed there is no
    /// detected mapping to differ from, so telling the user their choice "differs from what was
    /// detected" would be meaningless.
    /// </summary>
    public string? ConfirmationMessage
    {
        get
        {
            if (!DetectionSucceeded)
                return DetectionFailedConfirmationMessage;

            if (DetectedRomMapMode != SelectedRomMapMode)
                return OverriddenMapModeConfirmationMessage;

            return null;
        }
    }

    /// <summary>
    /// Take on everything a freshly-read snapshot says. Rows are matched by name and the row set
    /// is never rebuilt: a collection change here would invalidate host bindings mid-update, and
    /// the vector table has a fixed shape anyway.
    /// </summary>
    private void ApplySnapshot(SnesVectorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        applyingSnapshot = true;
        try
        {
            var byName = snapshot.Vectors.ToDictionary(vector => vector.Name, StringComparer.Ordinal);

            foreach (var row in Vectors)
            {
                var found = byName.TryGetValue(row.Name, out var vector);
                var readable = found && vector!.IsReadable;

                row.DisplayValue = found ? vector!.DisplayValue : SnesVectorSnapshot.UnreadablePlaceholder;
                row.IsSelectable = readable;

                // a re-read replaces the tick state rather than preserving it: the values are
                // different bytes now, so the previous choices were about different vectors.
                // Always-on rows ignore this and stay on.
                row.ForceEnabled(readable);
            }

            CartridgeTitle = snapshot.CartridgeTitle ?? "";
            StatusText = snapshot.VectorsReadable ? "" : VectorsUnreadableMessage;
        }
        finally
        {
            applyingSnapshot = false;
        }
    }
}

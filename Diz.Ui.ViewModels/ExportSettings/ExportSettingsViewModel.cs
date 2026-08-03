using Diz.Core.export;
using Diz.Core.util;

namespace Diz.Ui.ViewModels.ExportSettings;

/// <summary>
/// The "Export Disassembly" settings screen: how each line of the generated assembly is
/// written, where it is written to, and which optional pieces are included.
///
/// This ViewModel CANNOT VALIDATE A LINE TEMPLATE AND CANNOT GENERATE A SAMPLE. Both live in
/// the assembly-writing layer, which this one is not allowed to reference, so the caller hands
/// in two delegates and this type only decides when to call them and what to do with the
/// answers. Same separation as the misalignment checker's caller-supplied scan and the ROM
/// importer's caller-supplied recompute.
///
/// THE LINE-FORMAT PROPERTY IS CALLED <see cref="LineTemplate"/>, NOT "the F-word for a layout
/// string". Every type and member name in this assembly is swept for the vocabulary that marks
/// UI-toolkit leakage, and the word that names the settings record's own field is one of the
/// banned substrings -- it would fail twice, once for the property and once for its compiler
/// generated backing field. The name difference from
/// <see cref="LogWriterSettings.Format"/> is deliberate and must stay.
///
/// RAW TEXT ROUND-TRIPS. The exclude-authors box holds exactly what was typed until
/// <see cref="BuildSettings"/> is called; the settings record trims, de-duplicates and sorts
/// that list on the way in, so normalizing on every keystroke would rewrite the box under the
/// caret and eat the comma that was just typed -- making a second author impossible to start.
/// The one normalization that IS applied per keystroke is lower-casing the line template, which
/// the format parser depends on.
///
/// THE DISK IS TOUCHED ONLY ON DEMAND. One validator rule asks whether the output directory
/// really exists, which is a filesystem call; running it on every keystroke would hit the disk
/// once per character. Instead the answer is remembered, and it is re-read only when
/// <see cref="RefreshOutputPathStatus"/> is called -- once at construction, and thereafter
/// whenever the host decides the path has settled (commit, focus loss, or a debounce). Live
/// validation runs against the remembered answer, and a path nothing is remembered about is
/// assumed to exist so a half-typed path does not flash an error.
///
/// It never asks the user anything. When the output directory is missing it says so through
/// <see cref="NeedsOutputDirectoryCreated"/>; whoever hosts it decides how to put the question,
/// and calls <see cref="CreateOutputDirectory"/> if the answer was yes.
/// </summary>
public sealed class ExportSettingsViewModel : ViewModelNotifierBase
{
    /// <summary>
    /// What the sample box says instead of sample assembly when the line template cannot be
    /// parsed. Kept as the wording the screen has always used.
    /// </summary>
    public const string InvalidLineTemplateMessage = "Invalid format!";

    /// <summary>
    /// Why picking "all in one file" is a bad idea right now, in the assembly writer's own
    /// words -- it refuses that mode outright and this is the text it refuses with, minus the
    /// leading blank line it prefixes for console output. The option is still selectable: a
    /// project that already stores it must remain editable, and the mode is meant to come back.
    /// </summary>
    public const string SingleFileWarningText =
        "Temporary limitation: Sorry, single file output mode is broken in this version of Diz. " +
        "If you need it, please open an issue on github so we can fix it.\r\n" +
        "Please change exporter settings, set Structure to 'one bank per bank' mode.";

    /// <summary>Fewest bytes per data line the screen will accept.</summary>
    public const int MinDataPerLine = 1;

    /// <summary>Most bytes per data line the screen will accept.</summary>
    public const int MaxDataPerLine = 16;

    private readonly LogWriterSettings baseSettings;
    private readonly IFilesystemService fs;
    private readonly RememberedDirectoryExistence directoryExistence;
    private readonly Func<string, bool> isLineTemplateValid;
    private readonly Func<LogWriterSettings, string> generateSampleText;

    // set while the constructor is still filling fields in: the recompute below reads every
    // property, so it must not run until all of them hold their starting values.
    private bool suspendRecompute = true;

    private string lineTemplate;
    private int dataPerLine;
    private LogWriterSettings.FormatUnlabeled unlabeled;
    private LogWriterSettings.FormatStructure structure;
    private bool newLine;
    private bool outputExtraWhitespace;
    private bool generateFullLine;
    private bool includeUnusedLabels;
    private bool printLabelSpecificComments;
    private bool generatePlusMinusLabels;
    private bool generateAssetLabels;
    private string outputPath;
    private string excludedAuthorsText;

    private bool lineTemplateIsValid;
    private string sampleOutputText = "";
    private IReadOnlyList<string> problems = [];
    private string statusText = "";
    private bool canStartExport;

    /// <param name="settings">
    /// The settings to edit. Everything this screen does not show -- the directory tier names,
    /// the base output path stamped on by the project, the experimental switches -- is carried
    /// through untouched and comes back out of <see cref="BuildSettings"/> unchanged.
    /// </param>
    /// <param name="fs">
    /// Filesystem access, for the one validator rule that needs it and for creating a missing
    /// output directory. Read from at construction and then only when
    /// <see cref="RefreshOutputPathStatus"/> or <see cref="CreateOutputDirectory"/> is called.
    /// </param>
    /// <param name="isLineTemplateValid">
    /// Whether a line template parses. Supplied by the caller because the parser lives outside
    /// the assemblies this one may reference.
    /// </param>
    /// <param name="generateSampleText">
    /// Renders a few lines of assembly the way the given settings would write them, for the
    /// preview box. Supplied by the caller for the same reason, and expected to return its own
    /// error text rather than throwing.
    /// </param>
    /// <param name="notificationMarshaller">See <see cref="ViewModelNotifierBase"/>.</param>
    public ExportSettingsViewModel(
        LogWriterSettings settings,
        IFilesystemService fs,
        Func<string, bool> isLineTemplateValid,
        Func<LogWriterSettings, string> generateSampleText,
        Action<Action>? notificationMarshaller = null)
        : base(notificationMarshaller)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(isLineTemplateValid);
        ArgumentNullException.ThrowIfNull(generateSampleText);

        baseSettings = settings;
        this.fs = fs;
        this.isLineTemplateValid = isLineTemplateValid;
        this.generateSampleText = generateSampleText;
        directoryExistence = new RememberedDirectoryExistence(fs);

        lineTemplate = NormalizeLineTemplate(settings.Format);
        dataPerLine = ClampDataPerLine(settings.DataPerLine);
        unlabeled = settings.Unlabeled;
        structure = settings.Structure;
        newLine = settings.NewLine;
        outputExtraWhitespace = settings.OutputExtraWhitespace;
        generateFullLine = settings.GenerateFullLine;
        includeUnusedLabels = settings.IncludeUnusedLabels;
        printLabelSpecificComments = settings.PrintLabelSpecificComments;
        generatePlusMinusLabels = settings.GeneratePlusMinusLabels;
        generateAssetLabels = settings.GenerateAssetLabels;
        outputPath = settings.FileOrFolderOutPath ?? "";

        // the record stores this normalized already; joining with ", " is how the screen has
        // always displayed it, and it round-trips because BuildSettings splits on the comma.
        excludedAuthorsText = string.Join(", ", settings.ExcludedLabelAuthors);

        suspendRecompute = false;
        RefreshOutputPathStatus();
    }

    /// <summary>
    /// The template every emitted line is built from -- the placeholders and the literal text
    /// between them. Lower-cased as it is set: the parser looks its placeholders up by name, so
    /// a capital in a placeholder would stop it resolving. That also lower-cases the literal
    /// text, which is long-standing behaviour and the reason no upper-case literal can appear
    /// in exported lines.
    /// </summary>
    public string LineTemplate
    {
        get => lineTemplate;
        set => SetAndRecompute(ref lineTemplate, NormalizeLineTemplate(value), nameof(LineTemplate));
    }

    /// <summary>Whether <see cref="LineTemplate"/> currently parses.</summary>
    public bool LineTemplateIsValid
    {
        get => lineTemplateIsValid;
        private set => this.SetField(ref lineTemplateIsValid, value, propertyName: nameof(LineTemplateIsValid));
    }

    /// <summary>
    /// A few lines of assembly as the current settings would write them, or
    /// <see cref="InvalidLineTemplateMessage"/> when the template does not parse and there is
    /// nothing to render.
    /// </summary>
    public string SampleOutputText
    {
        get => sampleOutputText;
        private set => this.SetField(ref sampleOutputText, value ?? "", propertyName: nameof(SampleOutputText));
    }

    /// <summary>
    /// How many bytes go on one line of emitted data. Clamped to
    /// <see cref="MinDataPerLine"/>..<see cref="MaxDataPerLine"/>: the stored setting is a plain
    /// integer with nothing stopping a hand-edited project from putting anything in it, and the
    /// screen has to be able to show whatever it finds.
    /// </summary>
    public int DataPerLine
    {
        get => dataPerLine;
        set => SetAndRecompute(ref dataPerLine, ClampDataPerLine(value), nameof(DataPerLine));
    }

    /// <summary>What to do about bytes that carry no label.</summary>
    public LogWriterSettings.FormatUnlabeled Unlabeled
    {
        get => unlabeled;
        set => SetAndRecompute(ref unlabeled, value, nameof(Unlabeled));
    }

    /// <summary>One file per bank, or everything in a single file.</summary>
    public LogWriterSettings.FormatStructure Structure
    {
        get => structure;
        set
        {
            if (SetAndRecompute(ref structure, value, nameof(Structure)))
                OnPropertyChanged(nameof(StructureWarningText));
        }
    }

    /// <summary>
    /// What is wrong with the currently selected structure, or empty when nothing is. Only "all
    /// in one file" has anything to say; see <see cref="SingleFileWarningText"/>.
    /// </summary>
    public string StructureWarningText =>
        Structure == LogWriterSettings.FormatStructure.SingleFile ? SingleFileWarningText : "";

    /// <summary>Emit a blank line between banks/sections.</summary>
    public bool NewLine
    {
        get => newLine;
        set => SetAndRecompute(ref newLine, value, nameof(NewLine));
    }

    /// <summary>Pad emitted lines out so columns line up.</summary>
    public bool OutputExtraWhitespace
    {
        get => outputExtraWhitespace;
        set => SetAndRecompute(ref outputExtraWhitespace, value, nameof(OutputExtraWhitespace));
    }

    /// <summary>Emit the whole templated line rather than only its instruction part.</summary>
    public bool GenerateFullLine
    {
        get => generateFullLine;
        set => SetAndRecompute(ref generateFullLine, value, nameof(GenerateFullLine));
    }

    /// <summary>Emit labels nothing refers to, instead of dropping them.</summary>
    public bool IncludeUnusedLabels
    {
        get => includeUnusedLabels;
        set => SetAndRecompute(ref includeUnusedLabels, value, nameof(IncludeUnusedLabels));
    }

    /// <summary>Emit the comment attached to a label as well as the one attached to the address.</summary>
    public bool PrintLabelSpecificComments
    {
        get => printLabelSpecificComments;
        set => SetAndRecompute(ref printLabelSpecificComments, value, nameof(PrintLabelSpecificComments));
    }

    /// <summary>Use anonymous +/- branch targets where a named label is not needed.</summary>
    public bool GeneratePlusMinusLabels
    {
        get => generatePlusMinusLabels;
        set => SetAndRecompute(ref generatePlusMinusLabels, value, nameof(GeneratePlusMinusLabels));
    }

    /// <summary>Give each region exported as a binary asset a name at its start address.</summary>
    public bool GenerateAssetLabels
    {
        get => generateAssetLabels;
        set => SetAndRecompute(ref generateAssetLabels, value, nameof(GenerateAssetLabels));
    }

    /// <summary>
    /// Where the export is written. Relative paths are relative to the project file's directory
    /// and are created if they do not exist yet.
    /// </summary>
    public string OutputPath
    {
        get => outputPath;
        set => SetAndRecompute(ref outputPath, value ?? "", nameof(OutputPath));
    }

    /// <summary>
    /// The exclude-by-author blocklist, exactly as typed. Nothing is trimmed, de-duplicated,
    /// sorted or dropped until <see cref="BuildSettings"/> runs -- see the type summary for why
    /// normalizing here would make the box unusable.
    /// </summary>
    public string ExcludedAuthorsText
    {
        get => excludedAuthorsText;
        set => SetAndRecompute(ref excludedAuthorsText, value ?? "", nameof(ExcludedAuthorsText));
    }

    /// <summary>
    /// Everything currently wrong with the settings, in the validator's own words. Recomputed on
    /// every change, but always against the remembered answer to the one question that needs the
    /// disk (see the type summary).
    /// </summary>
    public IReadOnlyList<string> Problems
    {
        get => problems;
        private set
        {
            if (problems.SequenceEqual(value))
                return;

            problems = value;
            OnPropertyChanged(nameof(Problems));
        }
    }

    /// <summary>
    /// One line describing the most pressing thing wrong right now, or empty when nothing is.
    /// </summary>
    public string StatusText
    {
        get => statusText;
        private set => this.SetField(ref statusText, value ?? "", propertyName: nameof(StatusText));
    }

    /// <summary>
    /// Whether the settings are complete enough to export with. This is what gates the button
    /// that starts the export, so it covers the output path as well as the line template --
    /// which is new: the screen used to gate only on the template and let path problems surface
    /// after it closed.
    /// </summary>
    public bool CanStartExport
    {
        get => canStartExport;
        private set => this.SetField(ref canStartExport, value, propertyName: nameof(CanStartExport));
    }

    /// <summary>
    /// The directory the export needs, as the settings currently resolve it. Empty when the path
    /// does not resolve to one.
    /// </summary>
    public string OutputDirectoryToCreate => ResolveOutputDirectory(BuildSettings());

    /// <summary>
    /// Whether <see cref="OutputDirectoryToCreate"/> was missing the last time the disk was
    /// asked. False for a path the disk has not been asked about yet, so a half-typed path never
    /// looks like a missing directory; call <see cref="RefreshOutputPathStatus"/> to find out.
    /// </summary>
    public bool NeedsOutputDirectoryCreated
    {
        get
        {
            var directory = OutputDirectoryToCreate;
            return !string.IsNullOrEmpty(directory) && !directoryExistence.DirectoryExists(directory);
        }
    }

    /// <summary>
    /// Ask the disk again whether the output directory exists, and re-run validation against the
    /// answer. The host calls this when the path has settled -- on commit, on focus loss, or
    /// behind a debounce -- never per keystroke.
    /// </summary>
    public void RefreshOutputPathStatus()
    {
        directoryExistence.Recheck(OutputDirectoryToCreate);
        Recompute();
        OnPropertyChanged(nameof(NeedsOutputDirectoryCreated));
    }

    /// <summary>
    /// Create the output directory the settings ask for. Whether to do this is the user's call
    /// and asking is the host's job; this only carries it out, and re-reads the result.
    /// </summary>
    public void CreateOutputDirectory()
    {
        var directory = OutputDirectoryToCreate;
        if (!string.IsNullOrEmpty(directory))
            fs.CreateDirectory(directory);

        RefreshOutputPathStatus();
    }

    /// <summary>
    /// The settings as edited. THE ONLY PLACE THE RECORD IS REBUILT -- the screen used to
    /// reassemble it on every keystroke, which is what forced the normalized author list back
    /// into the box mid-typing. Everything not shown on this screen is carried through from the
    /// settings this ViewModel was built from.
    /// </summary>
    public LogWriterSettings BuildSettings() =>
        baseSettings with
        {
            Format = LineTemplate,
            DataPerLine = DataPerLine,
            Unlabeled = Unlabeled,
            Structure = Structure,
            NewLine = NewLine,
            OutputExtraWhitespace = OutputExtraWhitespace,
            GenerateFullLine = GenerateFullLine,
            IncludeUnusedLabels = IncludeUnusedLabels,
            PrintLabelSpecificComments = PrintLabelSpecificComments,
            GeneratePlusMinusLabels = GeneratePlusMinusLabels,
            GenerateAssetLabels = GenerateAssetLabels,
            FileOrFolderOutPath = OutputPath,

            // the record's own setter does the trimming, de-duplicating and sorting; the raw
            // text reaches it untouched and this is the moment it gets normalized.
            ExcludedLabelAuthorsList = ExcludedAuthorsText,
        };

    private static string NormalizeLineTemplate(string? value) => (value ?? "").ToLower();

    private static int ClampDataPerLine(int value) => Math.Clamp(value, MinDataPerLine, MaxDataPerLine);

    private static string ResolveOutputDirectory(LogWriterSettings settings) =>
        Path.GetDirectoryName(settings.BuildFullOutputPath()) ?? "";

    private bool SetAndRecompute<T>(ref T field, T value, string propertyName)
    {
        if (!this.SetField(ref field, value, propertyName: propertyName))
            return false;

        Recompute();
        return true;
    }

    /// <summary>
    /// Re-derive everything that follows from the current settings: whether the template parses,
    /// the sample, the validator's complaints, and whether export can start.
    ///
    /// The sample is rebuilt on ANY change, not just a template change, because the sample is
    /// rendered from the whole settings record -- bytes per line, padding and blank lines are all
    /// visible in it. That matches what the screen has always done.
    /// </summary>
    private void Recompute()
    {
        if (suspendRecompute)
            return;

        var settings = BuildSettings();

        LineTemplateIsValid = isLineTemplateValid(LineTemplate);
        SampleOutputText = LineTemplateIsValid ? generateSampleText(settings) : InvalidLineTemplateMessage;

        var found = new LogWriterSettingsValidator(directoryExistence)
            .Validate(settings)
            .Errors
            .Select(failure => failure.ErrorMessage)
            .ToList();

        Problems = found;
        CanStartExport = LineTemplateIsValid && found.Count == 0;
        StatusText = !LineTemplateIsValid
            ? InvalidLineTemplateMessage
            : found.Count > 0
                ? found[0]
                : "";
    }

    /// <summary>
    /// A filesystem view that answers "does this directory exist?" from what was last actually
    /// read off the disk, so live validation can run on every keystroke without a filesystem call
    /// per character. A directory nothing has been read about is reported as existing: the
    /// alternative is that every partially-typed path is announced as missing.
    /// </summary>
    private sealed class RememberedDirectoryExistence(IFilesystemService inner) : IFilesystemService
    {
        private readonly Dictionary<string, bool> remembered = new(StringComparer.OrdinalIgnoreCase);

        public bool DirectoryExists(string? outputDirectoryName) =>
            !remembered.TryGetValue(outputDirectoryName ?? "", out var exists) || exists;

        public void CreateDirectory(string name) => inner.CreateDirectory(name);

        /// <summary>Read the disk for one directory and remember what it said.</summary>
        public void Recheck(string? outputDirectoryName)
        {
            var key = outputDirectoryName ?? "";
            remembered[key] = inner.DirectoryExists(key);
        }
    }
}

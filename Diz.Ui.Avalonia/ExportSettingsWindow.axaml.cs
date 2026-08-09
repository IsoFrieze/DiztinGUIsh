using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Diz.Controllers.interfaces;
using Diz.Core.export;
using Diz.Ui.ViewModels.ExportSettings;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia "Export Disassembly" settings window: a passive view over the same
/// ExportSettingsViewModel the WinForms dialog hosts. Everything that decides what the choices
/// mean lives in the ViewModel -- whether the line template parses, what the sample looks like,
/// what the validator complains about, and whether an export can start at all. This file is
/// widget wiring plus the two things a ViewModel is not allowed to do: put a question to the
/// user, and open a file picker.
///
/// Layout is in ExportSettingsWindow.axaml; the x:Name fields and InitializeComponent come from
/// the Avalonia XAML source generator -- never hand-write them, or the generator suppresses its
/// own version and every named control is null at runtime.
///
/// PushText VALUE SUPPRESSION IS MANDATORY ON ALL THREE EDITABLE BOXES, and the
/// <see cref="updatingWidgets"/> flag alone will not do it. Avalonia raises TextChanged on a
/// LATER dispatcher turn, not inside the Text setter, so by the time the event arrives the flag
/// is down again and a write this window made looks exactly like a keystroke. Comparing the text
/// instead does not depend on when the event fires. This is the opposite situation from the ROM
/// import window, which documents at length why it needs none: that window has no editable text
/// at all, and combo/checkbox events do fire synchronously inside the write that caused them.
///
/// LETTING AN ECHO THROUGH HERE IS NOT HARMLESS. The line template is lower-cased as the
/// ViewModel stores it, so a pushed keystroke comes back as a DIFFERENT string -- type a capital
/// and the round trip retypes the box. That is also why writes go through
/// <see cref="WriteText"/>, which restores the caret: a plain assignment to
/// <see cref="TextBox.Text"/> drops it at the end of the box, which would move the caret to the
/// end after every upper-case character.
///
/// Unlike a modal dialog there is no blocking call: the window completes a task when the user
/// finishes. Closing it any other way (the X, Escape) counts as cancel.
/// </summary>
internal sealed partial class ExportSettingsWindow : Window
{
    /// <summary>The picker filter used when the export goes to one file rather than one per bank.</summary>
    private const string SingleFileFilter = "Assembly Files|*.asm|All Files|*.*";

    /// <summary>
    /// The structure picker's entries, in the order they are offered. "All in one file" is listed
    /// first because that is where it has always been; the assembly writer refuses that mode right
    /// now, which the window says out loud rather than by hiding the choice.
    /// </summary>
    private static readonly StructureChoice[] StructureChoices =
    [
        new(LogWriterSettings.FormatStructure.SingleFile, "All in one file"),
        new(LogWriterSettings.FormatStructure.OneBankPerFile, "One bank per file"),
    ];

    /// <summary>The unlabeled-instruction picker's entries, in the order they are offered.</summary>
    private static readonly UnlabeledChoice[] UnlabeledChoices =
    [
        new(LogWriterSettings.FormatUnlabeled.ShowAll, "Create All"),
        new(LogWriterSettings.FormatUnlabeled.ShowInPoints, "In points only"),
        new(LogWriterSettings.FormatUnlabeled.ShowNone, "None"),
    ];

    private readonly TaskCompletionSource<bool> completion = new();
    private readonly IFileDialogService fileDialogService;

    private ExportSettingsViewModel? vm;

    // true while widget values are being written FROM the ViewModel; the input handlers below
    // bail out then, so a ViewModel-driven refresh can't be mistaken for the user clicking.
    // Covers the combos, the checkboxes and the spinner, whose change events are raised inside
    // the write itself -- but NOT TextChanged, which arrives later (see PushText).
    private bool updatingWidgets;

    /// <summary>Parameterless ctor required by the Avalonia XAML compiler (AVLN3000). Never used
    /// at runtime -- the window is always created with a picker. The field stays null; no
    /// ctor-body code dereferences it, so tooling instantiation is still safe.</summary>
    public ExportSettingsWindow() : this(null!) { }

    /// <param name="fileDialogService">Opens the file/folder picker behind the Browse button.</param>
    public ExportSettingsWindow(IFileDialogService fileDialogService)
    {
        this.fileDialogService = fileDialogService;

        InitializeComponent();

        UnlabeledCombo.ItemsSource = UnlabeledChoices;
        StructureCombo.ItemsSource = StructureChoices;

        Closed += (_, _) =>
        {
            DetachViewModel();
            // closing without starting the export is a cancel; a no-op if the button already
            // answered.
            completion.TrySetResult(false);
        };
    }

    /// <summary>Completes when the user is done: true if they started the export, false if not.</summary>
    public Task<bool> Completion => completion.Task;

    /// <summary>A structure mode paired with the text to show for it. The picker binds to these,
    /// not to the bare enum: the enums carry no display text, so without this the list would read
    /// "OneBankPerFile".</summary>
    private sealed record StructureChoice(LogWriterSettings.FormatStructure Value, string Text);

    /// <summary>An unlabeled-instruction mode paired with the text to show for it, for the same
    /// reason as <see cref="StructureChoice"/>.</summary>
    private sealed record UnlabeledChoice(LogWriterSettings.FormatUnlabeled Value, string Text);

    // ------------------------------------------------------------------ VM attach/detach

    /// <param name="viewModel">Holds the settings being edited, and everything that validates them.</param>
    public void AttachViewModel(ExportSettingsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        DetachViewModel();

        vm = viewModel;
        vm.PropertyChanged += ViewModel_PropertyChanged;

        RefreshAllWidgets();
    }

    public void DetachViewModel()
    {
        if (vm == null)
            return;

        vm.PropertyChanged -= ViewModel_PropertyChanged;
        vm = null;
    }

    // ------------------------------------------------------------------ ViewModel -> widgets

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (vm == null)
            return;

        switch (e.PropertyName)
        {
            case nameof(ExportSettingsViewModel.LineTemplate):
                WriteText(LineTemplateBox, vm.LineTemplate);
                break;

            case nameof(ExportSettingsViewModel.OutputPath):
                WriteText(OutputPathBox, vm.OutputPath);
                break;

            case nameof(ExportSettingsViewModel.ExcludedAuthorsText):
                WriteText(ExcludeAuthorsBox, vm.ExcludedAuthorsText);
                break;

            case nameof(ExportSettingsViewModel.DataPerLine):
                WriteWidgets(() => DataPerLineBox.Value = vm.DataPerLine);
                break;

            case nameof(ExportSettingsViewModel.SampleOutputText):
                SampleBox.Text = vm.SampleOutputText;
                break;

            case nameof(ExportSettingsViewModel.CanStartExport):
                StartExportButton.IsEnabled = vm.CanStartExport;
                break;

            case nameof(ExportSettingsViewModel.StructureWarningText):
                StructureWarning.Text = vm.StructureWarningText;
                break;

            case nameof(ExportSettingsViewModel.Problems):
            case nameof(ExportSettingsViewModel.StatusText):
                RefreshProblems();
                break;
        }
    }

    private void RefreshAllWidgets()
    {
        if (vm == null)
            return;

        WriteWidgets(() =>
        {
            LineTemplateBox.Text = vm.LineTemplate;
            DataPerLineBox.Value = vm.DataPerLine;
            UnlabeledCombo.SelectedItem = UnlabeledChoices.FirstOrDefault(c => c.Value == vm.Unlabeled);
            StructureCombo.SelectedItem = StructureChoices.FirstOrDefault(c => c.Value == vm.Structure);
            NewLineCheck.IsChecked = vm.NewLine;
            ExtraWhitespaceCheck.IsChecked = vm.OutputExtraWhitespace;
            FullLineCheck.IsChecked = vm.GenerateFullLine;
            LabelCommentsCheck.IsChecked = vm.PrintLabelSpecificComments;
            UnusedLabelsCheck.IsChecked = vm.IncludeUnusedLabels;
            PlusMinusLabelsCheck.IsChecked = vm.GeneratePlusMinusLabels;
            AssetLabelsCheck.IsChecked = vm.GenerateAssetLabels;
            OutputPathBox.Text = vm.OutputPath;
            ExcludeAuthorsBox.Text = vm.ExcludedAuthorsText;

            SampleBox.Text = vm.SampleOutputText;
            StartExportButton.IsEnabled = vm.CanStartExport;
            StructureWarning.Text = vm.StructureWarningText;
            RefreshProblems();
        });
    }

    /// <summary>
    /// Say what is stopping the export. Every complaint is listed when there is more than one;
    /// otherwise the one-line summary is shown, which also covers an unparseable line template --
    /// not something the settings validator has an opinion about.
    /// </summary>
    private void RefreshProblems() =>
        ProblemsText.Text = vm == null
            ? ""
            : vm.Problems.Count > 0
                ? string.Join(Environment.NewLine, vm.Problems)
                : vm.StatusText;

    // ------------------------------------------------------------------ widgets -> ViewModel

    private void LineTemplateBox_TextChanged(object? sender, TextChangedEventArgs e) =>
        PushText(LineTemplateBox, vm?.LineTemplate, text => vm!.LineTemplate = text);

    private void OutputPathBox_TextChanged(object? sender, TextChangedEventArgs e) =>
        PushText(OutputPathBox, vm?.OutputPath, text => vm!.OutputPath = text);

    // the "does this directory exist?" rule reads the disk, so it waits until the path has settled.
    private void OutputPathBox_LostFocus(object? sender, RoutedEventArgs e) =>
        vm?.RefreshOutputPathStatus();

    private void ExcludeAuthorsBox_TextChanged(object? sender, TextChangedEventArgs e) =>
        PushText(ExcludeAuthorsBox, vm?.ExcludedAuthorsText, text => vm!.ExcludedAuthorsText = text);

    private void DataPerLineBox_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e) =>
        PushToViewModel(() =>
        {
            // the spinner clamps to the same range the ViewModel does, and an empty box reports
            // null rather than a number -- leave the setting alone until it holds one again.
            if (vm != null && e.NewValue.HasValue)
                vm.DataPerLine = (int)e.NewValue.Value;
        });

    private void UnlabeledCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        PushToViewModel(() =>
        {
            if (UnlabeledCombo.SelectedItem is UnlabeledChoice choice && vm != null)
                vm.Unlabeled = choice.Value;
        });

    private void StructureCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        PushToViewModel(() =>
        {
            if (StructureCombo.SelectedItem is StructureChoice choice && vm != null)
                vm.Structure = choice.Value;
        });

    private void NewLineCheck_Changed(object? sender, RoutedEventArgs e) =>
        PushToViewModel(() =>
        {
            if (vm != null)
                vm.NewLine = NewLineCheck.IsChecked == true;
        });

    private void ExtraWhitespaceCheck_Changed(object? sender, RoutedEventArgs e) =>
        PushToViewModel(() =>
        {
            if (vm != null)
                vm.OutputExtraWhitespace = ExtraWhitespaceCheck.IsChecked == true;
        });

    private void FullLineCheck_Changed(object? sender, RoutedEventArgs e) =>
        PushToViewModel(() =>
        {
            if (vm != null)
                vm.GenerateFullLine = FullLineCheck.IsChecked == true;
        });

    private void LabelCommentsCheck_Changed(object? sender, RoutedEventArgs e) =>
        PushToViewModel(() =>
        {
            if (vm != null)
                vm.PrintLabelSpecificComments = LabelCommentsCheck.IsChecked == true;
        });

    private void UnusedLabelsCheck_Changed(object? sender, RoutedEventArgs e) =>
        PushToViewModel(() =>
        {
            if (vm != null)
                vm.IncludeUnusedLabels = UnusedLabelsCheck.IsChecked == true;
        });

    private void PlusMinusLabelsCheck_Changed(object? sender, RoutedEventArgs e) =>
        PushToViewModel(() =>
        {
            if (vm != null)
                vm.GeneratePlusMinusLabels = PlusMinusLabelsCheck.IsChecked == true;
        });

    private void AssetLabelsCheck_Changed(object? sender, RoutedEventArgs e) =>
        PushToViewModel(() =>
        {
            if (vm != null)
                vm.GenerateAssetLabels = AssetLabelsCheck.IsChecked == true;
        });

    // ------------------------------------------------------------------ the two questions

    // async void: these are event handlers, and both the picker and the message box are async
    // because this toolkit has no blocking form of either.
    private async void BrowseButton_Click(object? sender, RoutedEventArgs e) =>
        await EnsureRealOutputDirectory(forcePickPath: true);

    private async void StartExportButton_Click(object? sender, RoutedEventArgs e)
    {
        // guard: the button is disabled while the settings are not exportable, but IsDefault means
        // Enter can reach it too, so the confirm path is gated here as well.
        if (vm?.CanStartExport != true)
            return;

        if (!await EnsureRealOutputDirectory(forcePickPath: false))
            return;

        completion.TrySetResult(true);
        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        completion.TrySetResult(false);
        Close();
    }

    /// <summary>
    /// Make sure the output directory is one that exists, asking as needed. Declining to create it
    /// is not a refusal to export -- it means "let me point somewhere else" -- so a new path gets
    /// picked and the question can come round a second time, for that path.
    ///
    /// The question is put with this toolkit's own message box rather than through the shared one,
    /// which is WinForms in every backend and would put a WinForms window over this one.
    /// </summary>
    /// <param name="forcePickPath">
    /// True when the user pressed Browse, so the picker opens even if the current path is fine.
    /// </param>
    /// <returns>true if the output directory now exists; false if the user backed out.</returns>
    private async Task<bool> EnsureRealOutputDirectory(bool forcePickPath)
    {
        var outcome = await AskToCreateOutputDirectory(
            "Press OK to create and use this path, Cancel to select a new path instead.");

        if (outcome == CreateDirectoryOutcome.Created)
            return true;

        if ((forcePickPath || outcome == CreateDirectoryOutcome.Declined) && !await PickOutputPath())
            return false;

        return await AskToCreateOutputDirectory() != CreateDirectoryOutcome.Declined;
    }

    private enum CreateDirectoryOutcome
    {
        AlreadyExists,
        Declined,
        Created,
    }

    private async Task<CreateDirectoryOutcome> AskToCreateOutputDirectory(string extraMsg = "")
    {
        if (vm == null)
            return CreateDirectoryOutcome.AlreadyExists;

        vm.RefreshOutputPathStatus();
        if (!vm.NeedsOutputDirectoryCreated)
            return CreateDirectoryOutcome.AlreadyExists;

        var wantsIt = await AvaloniaDialogs.ConfirmAsync(this, "Output Directory",
            "Output Directory does not exist.\nWould you like to create it now?\n" +
            $"{vm.OutputDirectoryToCreate}\n\n{extraMsg}");

        if (!wantsIt)
            return CreateDirectoryOutcome.Declined;

        vm.CreateOutputDirectory();
        return CreateDirectoryOutcome.Created;
    }

    /// <summary>
    /// Open the picker the current structure calls for -- a file when everything goes into one
    /// file, a folder when there is one file per bank -- and store the answer relative to the
    /// project's own directory when it sits underneath it.
    /// </summary>
    private async Task<bool> PickOutputPath()
    {
        if (vm == null)
            return false;

        var settings = vm.BuildSettings();
        var startingAt = settings.BuildFullOutputPath();

        var picked = settings.Structure == LogWriterSettings.FormatStructure.SingleFile
            ? await fileDialogService.PromptSaveFileAsync("", SingleFileFilter, startingAt)
            : await fileDialogService.PromptSelectFolderAsync("", startingAt);

        if (string.IsNullOrEmpty(picked))
            return false;

        vm.OutputPath = settings.WithPathRelativeTo(picked, settings.BaseOutputPath).FileOrFolderOutPath;
        vm.RefreshOutputPathStatus();
        return true;
    }

    // ------------------------------------------------------------------ plumbing

    /// <summary>Run a user-input handler, unless the change came from the ViewModel in the first place.</summary>
    private void PushToViewModel(Action push)
    {
        if (updatingWidgets)
            return;

        push();
    }

    /// <summary>
    /// Hand a box's text to the ViewModel -- unless the box is only reporting back what the
    /// ViewModel itself just put there.
    ///
    /// The updatingWidgets flag cannot decide that on its own. Avalonia raises TextChanged on a
    /// LATER dispatcher turn, not inside the Text setter, so by the time the event arrives the
    /// flag is down again and a write this window made is indistinguishable from a keystroke.
    /// Comparing the text instead does not depend on when the event fires.
    /// </summary>
    private void PushText(TextBox box, string? viewModelText, Action<string> assign)
    {
        if (vm == null || updatingWidgets)
            return;

        var text = box.Text ?? "";
        if (string.Equals(text, viewModelText, StringComparison.Ordinal))
            return;

        assign(text);
    }

    /// <summary>
    /// Put text in a box without stealing the caret.
    ///
    /// One of these boxes is normalized as it is stored -- the line template is lower-cased,
    /// because the parser looks its placeholders up by name -- so a keystroke pushed into the
    /// ViewModel can come straight back as a DIFFERENT string, and a plain assignment to
    /// <see cref="TextBox.Text"/> then drops the caret at the end of the box. Typing an upper-case
    /// letter mid-string would jump the caret to the end after every single character. Writing only
    /// on a real difference and putting the caret back where it was leaves the box showing what the
    /// settings actually say without moving under the person typing.
    /// </summary>
    private void WriteText(TextBox box, string text)
    {
        if (string.Equals(box.Text ?? "", text, StringComparison.Ordinal))
            return;

        WriteWidgets(() =>
        {
            var caret = box.CaretIndex;
            var selectionStart = box.SelectionStart;
            var selectionEnd = box.SelectionEnd;

            box.Text = text;

            box.CaretIndex = Math.Min(caret, text.Length);
            box.SelectionStart = Math.Min(selectionStart, text.Length);
            box.SelectionEnd = Math.Min(selectionEnd, text.Length);
        });
    }

    /// <summary>Write widget state without the input handlers treating it as user input.</summary>
    private void WriteWidgets(Action write)
    {
        var previous = updatingWidgets;
        updatingWidgets = true;
        try
        {
            write();
        }
        finally
        {
            updatingWidgets = previous;
        }
    }
}

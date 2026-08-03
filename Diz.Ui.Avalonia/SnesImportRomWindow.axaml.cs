using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Diz.Core.Interfaces;
using Diz.Core.util;
using Diz.Ui.ViewModels.ImportRom;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia "new project from a SNES ROM" window: a passive view over the same
/// SnesImportRomViewModel the WinForms window hosts. Everything that decides what the choices
/// mean lives in the ViewModel -- re-reading the vector table after a map-mode change is a
/// delegate the ViewModel was handed, and the "are you sure?" question a risky mapping warrants
/// is put by the caller once this window closes. This file is widget wiring only: it never reads
/// the ROM and never creates a project.
///
/// Layout is in SnesImportRomWindow.axaml; the x:Name fields and InitializeComponent come from
/// the Avalonia XAML source generator -- never hand-write them, or the generator suppresses its
/// own version and every named control is null at runtime.
///
/// NO PushText-style value suppression here, and none should be added. The sibling windows that
/// need it (goto, mark-many, harsh auto step) host TEXT BOXES, and Avalonia raises TextChanged on
/// a later dispatcher turn -- long after a plain "am I writing?" flag has been cleared -- so a
/// ViewModel-driven refresh there is indistinguishable from a keystroke. This window has no
/// editable text at all: the vector words and the ROM information are TextBlocks, and the only
/// inputs are one combo box and some checkboxes, whose change events fire synchronously inside
/// the write that caused them and are therefore fully covered by <see cref="updatingWidgets"/>.
/// Same reasoning as the misaligned-flags window.
///
/// The vector rows are the one part driven by binding rather than by hand: they hold no typeable
/// text, so there is no caret for a binding to fight, and the row objects raise PropertyChanged
/// for everything that moves.
///
/// Unlike a modal dialog there is no blocking call: the window completes a task when the user
/// finishes. Closing it any other way (the X, Escape) counts as cancel.
/// </summary>
internal sealed partial class SnesImportRomWindow : Window
{
    private readonly TaskCompletionSource<bool> completion = new();

    private SnesImportRomViewModel? vm;

    // true while widget values are being written FROM the ViewModel; the input handlers below
    // bail out then, so a ViewModel-driven refresh can't be mistaken for the user clicking.
    // Sufficient on its own here -- see the class summary on why no PushText is needed.
    private bool updatingWidgets;

    public SnesImportRomWindow()
    {
        InitializeComponent();

        Closed += (_, _) =>
        {
            DetachViewModel();
            // closing without pressing OK is a cancel; a no-op if OK already answered.
            completion.TrySetResult(false);
        };
    }

    /// <summary>Completes when the user is done: true if they confirmed, false if they cancelled.</summary>
    public Task<bool> Completion => completion.Task;

    /// <summary>
    /// A map mode paired with the text to show for it. The picker binds to these, not to the bare
    /// enum: the ViewModel deliberately carries no display strings, so without this the list would
    /// read "Sa1Rom" instead of "SA - 1 ROM".
    /// </summary>
    private sealed record MapModeChoice(RomMapMode Value, string Description);

    // ------------------------------------------------------------------ VM attach/detach

    /// <param name="viewModel">Holds the import choices, and re-reads the ROM when the mapping changes.</param>
    public void AttachViewModel(SnesImportRomViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        DetachViewModel();

        vm = viewModel;
        vm.PropertyChanged += ViewModel_PropertyChanged;

        WriteWidgets(() =>
        {
            MapModeCombo.ItemsSource = viewModel.RomMapModeChoices
                .Select(mode => new MapModeChoice(mode, Util.GetEnumDescription(mode)))
                .ToList();

            // the row set never changes for the life of a ViewModel, so this is assigned once and
            // the rows keep themselves up to date through their own PropertyChanged.
            VectorRows.ItemsSource = viewModel.Vectors;
        });

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
            case nameof(SnesImportRomViewModel.SelectedRomMapMode):
                WriteWidgets(SelectMapModeItem);
                break;

            case nameof(SnesImportRomViewModel.CartridgeTitle):
                RomTitle.Text = vm.CartridgeTitle;
                break;

            case nameof(SnesImportRomViewModel.StatusText):
                StatusText.Text = vm.StatusText;
                break;
        }
    }

    private void RefreshAllWidgets()
    {
        if (vm == null)
            return;

        WriteWidgets(() =>
        {
            SelectMapModeItem();
            HeaderFlagsCheck.IsChecked = vm.GenerateHeaderFlags;
            BankRegionsCheck.IsChecked = vm.GenerateBankRegions;

            DetectMessage.Text = vm.DetectionMessage;
            RomSpeed.Text = vm.RomSpeedText;
            RomTitle.Text = vm.CartridgeTitle;
            StatusText.Text = vm.StatusText;
        });
    }

    /// <summary>Point the picker at whichever entry carries the ViewModel's selected mapping.</summary>
    private void SelectMapModeItem()
    {
        if (vm == null)
            return;

        MapModeCombo.SelectedItem = MapModeCombo.ItemsSource?
            .OfType<MapModeChoice>()
            .FirstOrDefault(choice => choice.Value == vm.SelectedRomMapMode);
    }

    // ------------------------------------------------------------------ widgets -> ViewModel

    private void MapModeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        PushToViewModel(() =>
        {
            if (MapModeCombo.SelectedItem is MapModeChoice choice && vm != null)
                vm.SelectedRomMapMode = choice.Value;
        });

    private void HeaderFlagsCheck_Changed(object? sender, RoutedEventArgs e) =>
        PushToViewModel(() =>
        {
            if (vm != null)
                vm.GenerateHeaderFlags = HeaderFlagsCheck.IsChecked == true;
        });

    private void BankRegionsCheck_Changed(object? sender, RoutedEventArgs e) =>
        PushToViewModel(() =>
        {
            if (vm != null)
                vm.GenerateBankRegions = BankRegionsCheck.IsChecked == true;
        });

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        completion.TrySetResult(true);
        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        completion.TrySetResult(false);
        Close();
    }

    // ------------------------------------------------------------------ plumbing

    /// <summary>Run a user-input handler, unless the change came from the ViewModel in the first place.</summary>
    private void PushToViewModel(Action push)
    {
        if (updatingWidgets)
            return;

        push();
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

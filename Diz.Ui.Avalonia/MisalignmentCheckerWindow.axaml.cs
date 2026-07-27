using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Diz.Ui.ViewModels.MisalignmentChecker;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia "misaligned flags" window: a passive view over the same
/// MisalignmentCheckerViewModel the WinForms window hosts. The sweep that finds misalignments is
/// the ViewModel's caller-seeded delegate, and the repair belongs to whoever opened this window,
/// so this file is widget wiring only -- it never scans and never fixes. The caller applies
/// ProjectController.FixMisalignedFlags once <see cref="Completion"/> reports true.
///
/// Layout is in MisalignmentCheckerWindow.axaml; the x:Name fields and InitializeComponent come
/// from the Avalonia XAML source generator -- never hand-write them, or the generator suppresses
/// its own version and every named control is null at runtime.
///
/// NO re-entrancy guard here, unlike GotoWindow / MarkManyWindow / HarshAutoStepWindow. Those
/// need one because the user types into boxes the ViewModel writes back to, so a
/// ViewModel-driven refresh is indistinguishable from a keystroke. This window has no writable
/// text at all -- the report box is read-only -- so there is no echo loop to break, and adding
/// the pattern here would be ceremony guarding nothing.
///
/// Fix is enabled from the moment the window opens. Scanning is a preview; fixing without
/// scanning first has always been allowed and the instruction paragraph says so outright.
///
/// Unlike a modal dialog there is no blocking call: the window completes a task when the user
/// finishes. Closing it any other way (the X, Escape) counts as cancel.
/// </summary>
internal sealed partial class MisalignmentCheckerWindow : Window
{
    private readonly TaskCompletionSource<bool> completion = new();

    private MisalignmentCheckerViewModel? vm;

    public MisalignmentCheckerWindow()
    {
        InitializeComponent();

        Closed += (_, _) =>
        {
            DetachViewModel();
            // closing without pressing Fix is a cancel; a no-op if Fix already answered.
            completion.TrySetResult(false);
        };
    }

    /// <summary>Completes when the user is done: true if they confirmed the fix, false if they cancelled.</summary>
    public Task<bool> Completion => completion.Task;

    // ------------------------------------------------------------------ VM attach/detach

    /// <param name="viewModel">Runs the scan and holds what it found.</param>
    public void AttachViewModel(MisalignmentCheckerViewModel viewModel)
    {
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
            case nameof(MisalignmentCheckerViewModel.ReportText):
                ReportBox.Text = vm.ReportText;
                break;

            case nameof(MisalignmentCheckerViewModel.StatusText):
                StatusText.Text = vm.StatusText;
                break;
        }
    }

    private void RefreshAllWidgets()
    {
        if (vm == null)
            return;

        ReportBox.Text = vm.ReportText;
        StatusText.Text = vm.StatusText;
    }

    // ------------------------------------------------------------------ widgets -> ViewModel

    private void ScanButton_Click(object? sender, RoutedEventArgs e) => vm?.Scan();

    private void FixButton_Click(object? sender, RoutedEventArgs e)
    {
        completion.TrySetResult(true);
        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        completion.TrySetResult(false);
        Close();
    }
}

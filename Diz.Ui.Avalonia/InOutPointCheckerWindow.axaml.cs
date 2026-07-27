using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia "rescan for in/out/end/read points" window: it explains what a rescan does and
/// takes a yes or a no.
///
/// NO ViewModel, on purpose -- the window has no state, no inputs and no backend calls of its
/// own. An empty ViewModel would be ceremony with nothing in it. Running the rescan
/// (ProjectController.RescanForInOut) belongs to whoever opened it, once
/// <see cref="Completion"/> reports true.
///
/// Layout is in InOutPointCheckerWindow.axaml; the x:Name fields and InitializeComponent come
/// from the Avalonia XAML source generator -- never hand-write them, or the generator suppresses
/// its own version and every named control is null at runtime.
///
/// Unlike a modal dialog there is no blocking call: the window completes a task when the user
/// finishes. Closing it any other way (the X, Escape) counts as cancel.
/// </summary>
internal sealed partial class InOutPointCheckerWindow : Window
{
    private readonly TaskCompletionSource<bool> completion = new();

    public InOutPointCheckerWindow()
    {
        InitializeComponent();

        // closing without pressing Rescan is a cancel; a no-op if Rescan already answered.
        Closed += (_, _) => completion.TrySetResult(false);
    }

    /// <summary>Completes when the user is done: true if they confirmed the rescan, false if they cancelled.</summary>
    public Task<bool> Completion => completion.Task;

    private void RescanButton_Click(object? sender, RoutedEventArgs e)
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

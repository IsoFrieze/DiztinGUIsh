using Avalonia.Threading;
using Diz.Controllers.interfaces;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia backend's IProgressView (new-ui plan step 6, Part C). Owns a separate
/// top-level <see cref="ProgressWindow"/> (plan decision 4 -- never embedded). The
/// long-running-task handler (MainWindow.RunLongRunningTaskAsync) sets IsMarquee/TextOverride,
/// calls Show(), runs the work on a background Task that calls Report(...), then Close().
///
/// Threading: Avalonia shares the WinForms UI thread in this process (Phase 0). Report(...) is
/// invoked from the background Task thread, so every window touch is marshalled onto the
/// Avalonia dispatcher via <see cref="RunOnUiThread"/> (synchronous when already on the UI
/// thread). Construction is inert -- the window and the Avalonia platform are only created in
/// Show(), so resolving this view in the DI container never initializes Avalonia (the Phase 0
/// timing constraint, asserted by LabelEditorBackendSwitchTests).
/// </summary>
public sealed class AvaloniaProgressView : IProgressView
{
    private ProgressWindow? window;
    private bool isMarquee = true;
    private string? textOverride;

    // declared to satisfy IFormViewer; raised when the popup is closed on completion.
    public event EventHandler? OnFormClosed;

    public bool IsMarquee
    {
        get => isMarquee;
        set
        {
            isMarquee = value;
            RunOnUiThread(() => window?.SetMarquee(value));
        }
    }

    public string? TextOverride
    {
        get => textOverride;
        set
        {
            textOverride = value;
            RunOnUiThread(() => window?.SetDescription(value));
        }
    }

    public void Show()
    {
        AvaloniaGuiHost.EnsureInitialized();
        RunOnUiThread(() =>
        {
            if (window == null)
            {
                window = new ProgressWindow();
                window.SetMarquee(isMarquee);
                window.SetDescription(textOverride);
            }

            window.Show();
            window.Activate();
        });
    }

    public void BringFormToTop() => RunOnUiThread(() => window?.Activate());

    public void Report(int value) => RunOnUiThread(() => window?.SetProgress(value));

    public void Close() => RunOnUiThread(() =>
    {
        var toClose = window;
        window = null;
        if (toClose == null)
            return;

        try
        {
            toClose.Close();
        }
        catch
        {
            // already closed (e.g. user clicked the X mid-task): closing again is a no-op.
        }

        OnFormClosed?.Invoke(this, EventArgs.Empty);
    });

    // synchronous when already on the UI thread (the common case -- Show()/Close() are called
    // from the UI thread; Report() from the background Task).
    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Dispatcher.UIThread;
        if (dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }
}

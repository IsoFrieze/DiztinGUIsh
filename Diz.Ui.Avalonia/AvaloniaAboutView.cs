using Avalonia.Threading;
using Diz.Controllers.interfaces;
using Diz.Core.Interfaces;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia "About Diz" window's view service.
///
/// No ViewModel comes in and none is built: the window displays two strings from the version
/// source and takes an OK (see <see cref="IAboutView"/>).
///
/// Construction is deliberately inert: nothing here touches Avalonia until <see cref="Show"/>
/// runs, so resolving this from the container can never start Avalonia too early (it must not
/// come up before the host toolkit has finished its own display setup).
///
/// ONE window for the whole run. The window hides rather than closes, and this service keeps it,
/// so picking Help -> About repeatedly brings the same window forward instead of stacking a new
/// copy behind the last one. That is why this seam is registered as a singleton.
///
/// The window is a free-standing top level with NO owner and NO modality: an owner would have to
/// be a window of the other toolkit, and mixing toolkits inside one window ownership chain is not
/// done here.
/// </summary>
public sealed class AvaloniaAboutView : IAboutView
{
    private readonly IAppVersionInfo appVersionInfo;

    private AboutWindow? window;

    /// <param name="appVersionInfo">Supplies the version string and the full build description.</param>
    public AvaloniaAboutView(IAppVersionInfo appVersionInfo)
    {
        ArgumentNullException.ThrowIfNull(appVersionInfo);
        this.appVersionInfo = appVersionInfo;
    }

    public void Show()
    {
        AvaloniaGuiHost.EnsureInitialized();

        // called on the shared UI thread in the normal case; Invoke keeps window creation on
        // that thread regardless, and runs inline when already there.
        RunOnUiThread(() =>
        {
            window ??= new AboutWindow(appVersionInfo);
            window.Show();
            window.Activate();
        });
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Dispatcher.UIThread;
        if (dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }
}

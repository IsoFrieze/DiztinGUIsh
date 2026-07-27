using Avalonia.Threading;
using Diz.Controllers.interfaces;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia in/out-point rescan confirmation window's view service.
///
/// No ViewModel comes in and none is built: the window has no state, no inputs and no backend
/// calls of its own (see <see cref="IInOutPointCheckerView"/>). It asks, and hands back the
/// answer.
///
/// Construction is deliberately inert: nothing here touches Avalonia until
/// <see cref="ConfirmAsync"/> runs, so resolving this from the container can never start
/// Avalonia too early (it must not come up before the host toolkit has finished its own display
/// setup).
///
/// The window is a free-standing top level with NO owner and NO modality. An owner would have
/// to be a window of the other toolkit, and mixing toolkits inside one window ownership chain
/// is not done here -- so there is no blocking call to make. Instead the window completes a
/// task when the user finishes, which is exactly what the async view contract exists for. The
/// visible consequence is that the main window stays interactive while this one is open,
/// unlike the toolkit-native version.
/// </summary>
public sealed class AvaloniaInOutPointCheckerView : IInOutPointCheckerView
{
    public Task<bool> ConfirmAsync()
    {
        AvaloniaGuiHost.EnsureInitialized();

        // called on the shared UI thread in the normal case; Invoke keeps window creation on
        // that thread regardless, and runs inline when already there.
        return RunOnUiThread(() =>
        {
            var window = new InOutPointCheckerWindow();
            window.Show();
            window.Activate();
            return window.Completion;
        });
    }

    private static T RunOnUiThread<T>(Func<T> func)
    {
        var dispatcher = Dispatcher.UIThread;
        return dispatcher.CheckAccess() ? func() : dispatcher.Invoke(func);
    }
}

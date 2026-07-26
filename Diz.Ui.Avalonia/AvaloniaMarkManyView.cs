using Avalonia.Threading;
using Diz.Controllers.interfaces;
using Diz.Cpu._65816;
using Diz.Ui.ViewModels.MarkMany;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia mark-many window's view service.
///
/// Construction is deliberately inert: nothing here touches Avalonia until
/// <see cref="EditAsync"/> runs, so resolving this from the container can never start Avalonia
/// too early (it must not come up before the host toolkit has finished its own display setup).
///
/// The window is a free-standing top level with NO owner and NO modality. An owner would have
/// to be a window of the other toolkit, and mixing toolkits inside one window ownership chain
/// is not done here -- so there is no blocking call to make. Instead the window completes a
/// task when the user finishes, which is exactly what the async view contract exists for. The
/// visible consequence is that the main window stays interactive while this one is open,
/// unlike the toolkit-native version.
/// </summary>
public sealed class AvaloniaMarkManyView : IMarkManyView
{
    public Task<bool> EditAsync(MarkManyViewModel<ISnesData> viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        AvaloniaGuiHost.EnsureInitialized();

        // called on the shared UI thread in the normal case; Invoke keeps window creation on
        // that thread regardless, and runs inline when already there.
        return RunOnUiThread(() =>
        {
            var window = new MarkManyWindow();
            window.AttachViewModel(viewModel);
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

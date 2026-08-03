using Avalonia.Threading;
using Diz.Controllers.interfaces;
using Diz.Ui.ViewModels.ImportRom;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia "new project from a SNES ROM" window's view service.
///
/// Construction is deliberately inert: nothing here touches Avalonia until
/// <see cref="EditAsync"/> runs, so resolving this from the container can never start Avalonia
/// too early (it must not come up before the host toolkit has finished its own display setup).
///
/// The window is a free-standing top level with NO owner and NO modality. An owner would have to
/// be a window of the other toolkit, and mixing toolkits inside one window ownership chain is not
/// done here -- so there is no blocking call to make. Instead the window completes a task when
/// the user finishes, which is exactly what the async view contract exists for. The visible
/// consequence is that the main window stays interactive while this one is open, unlike the
/// toolkit-native version.
///
/// A fresh window per call: the importer re-shows the same ViewModel when the user declines the
/// "are you sure?" question that follows a risky mapping, and this view must not assume it is the
/// only one that ever edited it.
/// </summary>
public sealed class AvaloniaSnesImportRomView : ISnesImportRomView
{
    public Task<bool> EditAsync(SnesImportRomViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        AvaloniaGuiHost.EnsureInitialized();

        // called on the shared UI thread in the normal case; Invoke keeps window creation on
        // that thread regardless, and runs inline when already there.
        return RunOnUiThread(() =>
        {
            var window = new SnesImportRomWindow();
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

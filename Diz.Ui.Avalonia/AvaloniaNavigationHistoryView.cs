using Diz.Controllers.interfaces;
using Diz.Ui.ViewModels.Navigation;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia backend's INavigationHistoryView: a thin host that owns a separate top-level
/// Avalonia window bound to the same NavigationHistoryViewModel the WinForms history grid uses.
///
/// Construction is deliberately trivial, and SO ARE BOTH SETTERS: MainWindow resolves this via
/// IViewFactory.GetNavigationHistoryView() in its constructor and assigns the ViewModel and the
/// overshoot right there -- all of which runs BEFORE the WinForms message loop starts, and Avalonia
/// must not initialize that early. Everything Avalonia-touching is deferred to Show(), which is only
/// ever reached from a running message loop. (Same constraint, same shape, as
/// <see cref="AvaloniaRegionListView"/> and AvaloniaLabelEditorView.)
///
/// UNLIKE the label and region editors, this host does NOT compose its own ViewModel. The history
/// is not project-scoped: it survives closing one project and opening another, back/forward work
/// with this window never opened, and the position in the history has to be the same object the
/// main window's menu commands move. So the host owns it and this view borrows it -- and never
/// disposes it.
/// </summary>
public sealed class AvaloniaNavigationHistoryView : INavigationHistoryView
{
    private NavigationHistoryWindow? window;
    private NavigationHistoryViewModel? viewModel;
    private int backForwardOvershoot = NavigationHistoryViewModel.NoOvershoot;

    // ------------------------------------------------------------------ INavigationHistoryView

    // declared, never raised: the window hides on close, so from the caller's perspective the
    // "form" never closes -- identical to the WinForms implementation.
    public event EventHandler? OnFormClosed;

    public NavigationHistoryViewModel? ViewModel
    {
        get => viewModel;
        set
        {
            if (ReferenceEquals(viewModel, value))
                return;

            viewModel = value;

            // only if the window already exists: before Show() there is nothing Avalonia-side to
            // rebind, and building one here would initialize Avalonia at the wrong moment.
            if (window == null)
                return;

            if (viewModel == null)
                window.DetachViewModel();
            else
                window.AttachViewModel(viewModel);
        }
    }

    public int BackForwardOvershoot
    {
        get => backForwardOvershoot;
        set
        {
            backForwardOvershoot = value;

            // a plain int, so this one is safe to forward whenever -- but the window may not exist.
            if (window != null)
                window.BackForwardOvershoot = value;
        }
    }

    public void Show()
    {
        AvaloniaGuiHost.EnsureInitialized();

        if (window == null)
        {
            window = new NavigationHistoryWindow { BackForwardOvershoot = backForwardOvershoot };

            if (viewModel != null)
                window.AttachViewModel(viewModel);
        }

        window.Show();
        window.Activate();
    }

    public void BringFormToTop() => window?.Activate();
}

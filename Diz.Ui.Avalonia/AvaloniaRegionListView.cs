using System.Collections.ObjectModel;
using Avalonia.Threading;
using Diz.Controllers.interfaces;
using Diz.Core.Interfaces;
using Diz.Core.model.snes;
using Diz.Ui.ViewModels.Regions;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia backend's IRegionListView: a thin host that owns a separate top-level Avalonia
/// window bound to the same RegionListViewModel the WinForms region grid uses.
///
/// Construction is deliberately trivial: MainWindow resolves this via
/// IViewFactory.GetRegionEditorView() in its constructor, which runs BEFORE the WinForms message
/// loop starts -- and Avalonia must not initialize that early. Everything Avalonia-touching is
/// deferred to Show(), which is only ever reached from a running message loop.
///
/// The ViewModel is project-scoped (it wraps the open project's regions), so this host composes
/// it itself rather than resolving it: a different project means a different ViewModel, not a
/// reconfigured one.
/// </summary>
public sealed class AvaloniaRegionListView : IRegionListView
{
    private IProjectController? projectController;
    private RegionListWindow? window;
    private IRegionListViewModel? viewModel;

    // what the current ViewModel was built over, so a rebind can tell "the same project again"
    // from "a different project" without rebuilding either way.
    private ObservableCollection<IRegion>? boundRegions;

    private Data? Data => projectController?.Project?.Data;

    // ------------------------------------------------------------------ IRegionListView

    // declared, never raised: the window hides on close, so from the caller's perspective the
    // "form" never closes -- identical to the WinForms implementation.
    public event EventHandler? OnFormClosed;

    public void SetProjectController(IProjectController? newProjectController)
    {
        projectController = newProjectController;
        RecreateViewModelIfWindowExists();
    }

    public void RebindProject() => RecreateViewModelIfWindowExists();

    public void Show()
    {
        AvaloniaGuiHost.EnsureInitialized();

        if (window == null)
        {
            window = new RegionListWindow();
            RecreateViewModel();
        }
        else if (viewModel == null)
        {
            RecreateViewModel();
        }

        window.Show();
        window.Activate();
    }

    public void BringFormToTop() => window?.Activate();

    // ------------------------------------------------------------------ VM lifecycle

    private void RecreateViewModelIfWindowExists()
    {
        if (window != null)
            RecreateViewModel();
        // no window yet: nothing to tear down -- Show() composes the ViewModel on first open.
    }

    private void RecreateViewModel()
    {
        var regionProvider = Data;

        // Every project change reaches here, including a plain Save. When the regions are still
        // the same collection, rebuilding would throw away the sort order and the selection for
        // nothing -- and it is unnecessary, because the rows follow the collection's own change
        // notification. So only rebuild when the regions really are a different collection.
        if (viewModel != null && regionProvider != null && ReferenceEquals(boundRegions, regionProvider.Regions))
            return;

        TearDownViewModel();

        if (regionProvider == null || window == null)
            return;

        boundRegions = regionProvider.Regions;
        viewModel = new RegionListViewModel(regionProvider, notificationMarshaller: RunOnUiThread);
        viewModel.RegionsChanged += ViewModel_RegionsChanged;
        window.AttachViewModel(viewModel);
    }

    private void TearDownViewModel()
    {
        boundRegions = null;

        if (viewModel == null)
            return;

        window?.DetachViewModel();
        viewModel.RegionsChanged -= ViewModel_RegionsChanged;
        viewModel.Dispose();
        viewModel = null;
    }

    // VM marshaller contract (see ViewModelNotifierBase): synchronous when already on the UI
    // thread. Avalonia shares the WinForms UI thread in this process.
    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Dispatcher.UIThread;
        if (dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }

    // ------------------------------------------------------------------ VM events

    // region data changed (an add, a delete, or a committed field edit), so the project has
    // unsaved work in it. Re-sorting and selecting deliberately do not raise this.
    private void ViewModel_RegionsChanged(object? sender, EventArgs e) =>
        projectController?.MarkChanged();
}

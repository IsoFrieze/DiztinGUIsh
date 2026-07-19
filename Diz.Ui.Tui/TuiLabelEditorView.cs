using System.Diagnostics;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core.model.snes;
using Diz.Cpu._65816;
using Diz.Ui.ViewModels.Labels;
using Terminal.Gui.App;

// Terminal.Gui v2.4 marks the static Application lifecycle (Init/Run/Invoke/Shutdown/RequestStop)
// [Obsolete] with "The legacy static Application object is going away" -- but it is still the
// documented, working entry point and is exactly the pattern this PoC's handoff specified. The
// growth path is to migrate to the instance API (Application.Create()/IApplication). Suppressing
// CS0618 here is a deliberate, localized choice for the PoC.
#pragma warning disable CS0618

namespace Diz.Ui.Tui;

/// <summary>
/// The TUI backend's ILabelEditorView: a thin host that pops a Terminal.Gui label VIEWER in a
/// console, bound to the SAME <see cref="ILabelEditorViewModel"/> the WinForms and Avalonia
/// editors use (Diz.Ui.ViewModels). This mirrors <c>AvaloniaLabelEditorView</c>: it composes the
/// VM from Data.Labels, wires <c>resolveRomOffsetToSnesIa</c> and <c>NavigationRequested -></c>
/// projectController.SelectOffset, and stays inert until Show().
///
/// PoC shape (allowed shortcut, per the handoff): the Terminal.Gui screen renders a ONE-SHOT
/// snapshot of the VM rows. Only the "jump to label" path is wired back to the WinForms UI; that
/// jump is marshalled to the WinForms thread through the captured
/// <see cref="SynchronizationContext"/> and then drives the exact same navigation the Avalonia
/// editor uses. Live two-way updates (WinForms VM change -> Terminal.Gui) are intentionally NOT
/// built, but the screen binds an ObservableCollection so they can be ADDED later without a
/// restructure.
///
/// Threading: Terminal.Gui's "main thread" is whichever thread calls Application.Init(), so a
/// dedicated background thread owns Init()+Run(). We never `await` on that thread expecting the
/// continuation back on it (open Terminal.Gui v2 bug #5579) -- the only cross-thread hop is
/// Application.Invoke (WinForms->TUI, for a clean stop) and SynchronizationContext.Post
/// (TUI->WinForms, for the jump).
/// </summary>
public sealed class TuiLabelEditorView : ILabelEditorView
{
    private readonly ConsoleHost consoleHost = new();

    private IProjectController? projectController;
    private ILabelEditorViewModel? viewModel;

    // captured on the WinForms UI thread in Show(), BEFORE the TUI thread starts.
    private SynchronizationContext? winformsContext;

    private Thread? tuiThread;
    private TuiLabelEditorScreen? screen;
    private volatile bool running;

    private Data? Data => projectController?.Project?.Data;

    // ------------------------------------------------------------------ ILabelEditorView

    // declared, never raised: like the WinForms/Avalonia hosts, the caller treats the editor as
    // a persistent view. Re-Show() after a quit simply re-inits a fresh TUI.
    public event EventHandler? OnFormClosed;

    public void SetProjectController(IProjectController? newProjectController)
    {
        projectController = newProjectController;
        RecreateViewModelIfExists();
    }

    public void RepopulateFromData() => RecreateViewModelIfExists();

    public void RebindProject() => RecreateViewModelIfExists();

    public void Show()
    {
        // already up: don't double-Init. Best-effort focus, then no-op.
        if (running)
        {
            BringFormToTop();
            return;
        }

        // capture the WinForms SynchronizationContext HERE, on the UI thread, before the TUI
        // thread exists. The jump path posts back to it.
        winformsContext = SynchronizationContext.Current;

        EnsureViewModel();

        var snapshot = BuildSnapshot();

        if (!consoleHost.EnsureConsole())
        {
            Debug.WriteLine("TuiLabelEditorView: could not allocate a console; not showing.");
            return;
        }

        // console-X / Ctrl-C -> ask the TUI loop to stop cleanly (best effort; ~5s grace).
        consoleHost.OnCloseRequested = () =>
        {
            try { Application.Invoke(() => screen?.RequestStop()); }
            catch { /* loop may already be gone */ }
        };

        running = true;
        tuiThread = new Thread(() => TuiThreadProc(snapshot))
        {
            IsBackground = true,
            Name = "diz-tui-label-viewer",
        };
        tuiThread.Start();
    }

    public void BringFormToTop()
    {
        if (running)
        {
            try { Application.Invoke(() => screen?.Window.SetFocus()); }
            catch { /* not up yet / already gone */ }
        }
    }

    public void FocusOrCreateLabelAtSelectedRomOffsetIa()
    {
        var selectedOffset = projectController?.ProjectView.SelectedOffset ?? -1;
        if (selectedOffset == -1)
            return;
        FocusOrCreateLabelAtRomOffsetIa(selectedOffset);
    }

    public void FocusOrCreateLabelAtRomOffsetIa(int selectedOffset)
    {
        EnsureViewModel();
        // PoC: this mutates/selects in the shared VM (correct model behavior). The one-shot TUI
        // snapshot won't visually reflect it until reopened -- the accepted viewer-first shortcut.
        viewModel?.FocusOrCreateAtRomOffsetIa(selectedOffset);
    }

    public void FocusOrCreateLabelAtSnesAddress(int snesAddress)
    {
        EnsureViewModel();
        viewModel?.FocusOrCreateAtSnesAddress(snesAddress);
    }

    // ------------------------------------------------------------------ TUI thread

    private void TuiThreadProc(IReadOnlyList<TuiLabelRow> snapshot)
    {
        try
        {
            Application.Init();

            screen = new TuiLabelEditorScreen(snapshot, JumpToAddressFromTui);
            Application.Run(screen.Window);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TuiLabelEditorView TUI thread failed: {ex}");
        }
        finally
        {
            try { Application.Shutdown(); } catch { /* best effort */ }
            screen = null;
            running = false;
            consoleHost.ReleaseConsole();
        }
    }

    /// <summary>Called on the TUI thread when the user jumps. Marshal to the WinForms thread and
    /// drive the SAME navigation path the Avalonia editor uses.</summary>
    private void JumpToAddressFromTui(int snesAddress)
    {
        var ctx = winformsContext;
        if (ctx != null)
            ctx.Post(_ => JumpOnWinformsThread(snesAddress), null);
        else
            JumpOnWinformsThread(snesAddress); // best effort (no context captured)
    }

    private void JumpOnWinformsThread(int snesAddress)
    {
        var vm = viewModel;
        if (vm == null)
            return;

        // resolve the snapshot's address back to the LIVE row (robust to snapshot staleness),
        // then jump exactly like Avalonia's "Go To" button: SelectedRow + JumpToSelectedInMainView.
        var row = vm.Rows.FirstOrDefault(r => r.SnesAddress == snesAddress);
        if (row == null)
            return;

        vm.SelectedRow = row;
        vm.JumpToSelectedInMainView();
    }

    // ------------------------------------------------------------------ VM lifecycle
    // (mirrors AvaloniaLabelEditorView.RecreateViewModel, minus the window.)

    private void EnsureViewModel()
    {
        if (viewModel == null)
            RecreateViewModel();
    }

    private void RecreateViewModelIfExists()
    {
        if (viewModel != null)
            RecreateViewModel();
    }

    private void RecreateViewModel()
    {
        TearDownViewModel();

        var labels = Data?.Labels;
        if (labels == null)
            return;

        viewModel = new LabelEditorViewModel(
            labels,
            notificationMarshaller: RunOnWinformsThread,
            resolveRomOffsetToSnesIa: romOffset =>
                Data?.GetSnesApi()?.GetIntermediateAddress(romOffset, resolve: true) ?? -1);

        viewModel.ErrorRaised += ViewModel_ErrorRaised;
        viewModel.NavigationRequested += ViewModel_NavigationRequested;
    }

    private void TearDownViewModel()
    {
        if (viewModel == null)
            return;

        viewModel.ErrorRaised -= ViewModel_ErrorRaised;
        viewModel.NavigationRequested -= ViewModel_NavigationRequested;
        viewModel.Dispose();
        viewModel = null;
    }

    private IReadOnlyList<TuiLabelRow> BuildSnapshot()
    {
        var vm = viewModel;
        if (vm == null)
            return [];

        return vm.Rows
            .Select(r => new TuiLabelRow(r.AddressText, r.Name, r.Comment, r.SnesAddress))
            .ToList();
    }

    // VM marshaller: run inline when already on the WinForms thread (or none captured yet),
    // otherwise post to the captured WinForms context. Mirrors AvaloniaLabelEditorView.RunOnUiThread.
    private void RunOnWinformsThread(Action action)
    {
        var ctx = winformsContext;
        if (ctx == null || SynchronizationContext.Current == ctx)
            action();
        else
            ctx.Post(_ => action(), null);
    }

    // ------------------------------------------------------------------ VM events

    private void ViewModel_ErrorRaised(object? sender, string message) =>
        // the TUI viewer has no dialog host in the PoC; surface to the debug log.
        Debug.WriteLine($"TuiLabelEditorView VM error: {message}");

    private void ViewModel_NavigationRequested(object? sender, int snesAddress)
    {
        if (projectController == null)
            return;

        var romOffset = Data?.ConvertSnesToPc(snesAddress) ?? -1;
        if (romOffset == -1)
            return;

        projectController.SelectOffset(romOffset,
            new ISnesNavigation.HistoryArgs { Description = "Jump To Label" });
    }
}

using Avalonia.Threading;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core.model.snes;
using Diz.Cpu._65816;
using Diz.Ui.ViewModels.Labels;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia backend's ILabelEditorView: a thin host that owns a separate top-level
/// Avalonia window (plan decision 3 -- never embedded) bound to the same
/// ILabelEditorViewModel the WinForms editor uses.
///
/// Construction is deliberately trivial: MainWindow resolves this via
/// IViewFactory.GetLabelEditorView() in its constructor, which runs BEFORE the WinForms
/// message loop starts -- and Avalonia must not initialize that early (Phase 0 timing
/// constraint). Everything Avalonia-touching is deferred to Show(), which is only ever
/// reached from a running message loop; by then the app layer's first-idle hook has
/// normally initialized Avalonia already (EnsureInitialized here is the safety net).
///
/// The VM is project-scoped (wraps the open project's label provider), so this host
/// composes it itself -- same pattern, same ports, and same port wiring as the WinForms
/// LabelsViewControl.RecreateViewModel.
/// </summary>
public sealed class AvaloniaLabelEditorView : ILabelEditorView
{
    // same filter strings the WinForms editor uses (seam contract is WinForms filter syntax)
    private const string LabelImportFilter =
        "Comma Separated Value Files|*.csv|BSNES Symbols Map|*.cpu.sym|Text Files|*.txt|All Files|*.*";
    private const string LabelExportFilter =
        "Comma Separated Value Files|*.csv|Text Files|*.txt|All Files|*.*";

    private readonly AvaloniaFileDialogService fileDialogService;
    private IProjectController? projectController;
    private LabelEditorWindow? window;
    private ILabelEditorViewModel? viewModel;

    public AvaloniaLabelEditorView(AvaloniaFileDialogService fileDialogService) =>
        this.fileDialogService = fileDialogService;

    private Data? Data => projectController?.Project?.Data;

    // ------------------------------------------------------------------ ILabelEditorView

    // declared, never raised: the window hides on close, so from the caller's perspective
    // the "form" never closes -- identical to the WinForms implementation.
    public event EventHandler? OnFormClosed;

    public void SetProjectController(IProjectController? newProjectController)
    {
        projectController = newProjectController;
        if (window != null)
            RecreateViewModel();
    }

    public void RepopulateFromData() => RecreateViewModelIfWindowExists();

    public void RebindProject() => RecreateViewModelIfWindowExists();

    public void Show()
    {
        AvaloniaGuiHost.EnsureInitialized();

        if (window == null)
        {
            window = new LabelEditorWindow(this);
            fileDialogService.DialogOwner = window;
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

    public void FocusOrCreateLabelAtSelectedRomOffsetIa()
    {
        var selectedOffset = projectController?.ProjectView.SelectedOffset ?? -1;
        if (selectedOffset == -1)
        {
            ShowErrorDialog("No offset selected in main form, or no project loaded."); // same text as WinForms
            return;
        }

        FocusOrCreateLabelAtRomOffsetIa(selectedOffset);
    }

    public void FocusOrCreateLabelAtRomOffsetIa(int selectedOffset) =>
        window?.FocusRowForEdit(viewModel?.FocusOrCreateAtRomOffsetIa(selectedOffset));

    public void FocusOrCreateLabelAtSnesAddress(int snesAddress) =>
        window?.FocusRowForEdit(viewModel?.FocusOrCreateAtSnesAddress(snesAddress));

    // ------------------------------------------------------------------ VM lifecycle

    private void RecreateViewModelIfWindowExists()
    {
        if (window != null)
            RecreateViewModel();
        // no window yet: nothing to tear down -- Show() composes the VM on first open.
    }

    private void RecreateViewModel()
    {
        TearDownViewModel();

        var labels = Data?.Labels;
        if (labels == null || window == null)
            return;

        viewModel = new LabelEditorViewModel(
            labels,
            notificationMarshaller: RunOnUiThread,
            resolveRomOffsetToSnesIa: romOffset =>
                Data?.GetSnesApi()?.GetIntermediateAddress(romOffset, resolve: true) ?? -1);

        viewModel.ErrorRaised += ViewModel_ErrorRaised;
        viewModel.NavigationRequested += ViewModel_NavigationRequested;
        window.AttachViewModel(viewModel);
    }

    private void TearDownViewModel()
    {
        if (viewModel == null)
            return;

        window?.DetachViewModel();
        viewModel.ErrorRaised -= ViewModel_ErrorRaised;
        viewModel.NavigationRequested -= ViewModel_NavigationRequested;
        viewModel.Dispose();
        viewModel = null;
    }

    // VM marshaller contract (see ViewModelNotifierBase): synchronous when already on the
    // UI thread. Avalonia shares the WinForms UI thread in this process (Phase 0).
    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Dispatcher.UIThread;
        if (dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }

    // ------------------------------------------------------------------ VM events

    private void ViewModel_ErrorRaised(object? sender, string message) => ShowErrorDialog(message);

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

    private void ShowErrorDialog(string message)
    {
        if (window == null)
            return;
        // fire-and-forget: matches the WinForms MessageBox call sites in spirit; the
        // Avalonia dialog is owner-modal but the caller doesn't need its result.
        _ = AvaloniaDialogs.ShowMessageAsync(window, "Error", message);
    }

    // ------------------------------------------------------------------ window commands
    // (called by LabelEditorWindow's toolbar; live here because they need the file-dialog
    // service and the project controller.)

    /// <summary>Import via vm.ImportLabelsAsync -- the path Step 4 explicitly reserved for
    /// the Avalonia/TUI backends (deviation 1 in the plan's Step 4 section). WinForms keeps
    /// the legacy ProjectController.ImportLabelsCsv route for bug-for-bug dialog parity;
    /// this backend has no legacy dialogs to preserve. Parse failures surface through the
    /// VM's ErrorRaised (which appends "(near line N)" when known).</summary>
    internal async Task ImportLabels(bool replaceAll)
    {
        if (viewModel == null || window == null)
            return;

        // same warning texts the WinForms menu items show
        var warning = replaceAll
            ? "Info: All list items will be deleted and replaced with the CSV file.\n" +
              "\n" +
              "Continue?\n"
            : "Info: Items in CSV will:\n" +
              "1) CSV items will be added if their address doesn't already exist in this list\n" +
              "2) CSV items will replace anything with the same address as items in the list\n" +
              "3) any unmatched addresses in the list will be left alone\n" +
              "\n" +
              "Continue?\n";

        if (!await AvaloniaDialogs.ConfirmAsync(window, "Warning", warning))
            return;

        // empty title = keep the OS default, like the WinForms call sites
        var path = await fileDialogService.PromptOpenFileAsync("", LabelImportFilter);
        if (string.IsNullOrEmpty(path))
            return;

        await viewModel.ImportLabelsAsync(path, replaceAll);
    }

    internal async Task ExportLabels()
    {
        if (viewModel == null || window == null)
            return;

        var path = await fileDialogService.PromptSaveFileAsync("", LabelExportFilter);
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            await viewModel.ExportLabelsAsync(path);
        }
        catch (Exception)
        {
            await AvaloniaDialogs.ShowMessageAsync(window, "Error",
                "An error occurred while saving the file."); // same text as WinForms
        }
    }

    /// <summary>Confirm + normalize. WinForms routes this through
    /// ProjectController.NormalizeWramLabels, whose confirm prompt is ICommonGui (a WinForms
    /// MessageBox); an Avalonia window must not pop WinForms dialogs (decision 4), so this
    /// backend asks with its own dialog (same text) and runs the same underlying operation
    /// through vm.NormalizeWramLabels (the Diz.Core implementation both routes share).</summary>
    internal async Task NormalizeWramLabels()
    {
        if (viewModel == null || window == null)
            return;

        if (!await AvaloniaDialogs.ConfirmAsync(window, "Confirm",
                "This converts all WRAM labels (where possible and non-overlapping) to the $7E/$7F range. Proceed?"))
            return;

        viewModel.NormalizeWramLabels();
    }

    internal void NewLabelFromSelectedIa() => FocusOrCreateLabelAtSelectedRomOffsetIa();
}

using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Diz.Ui.ViewModels.Regions;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia region editor window: a passive view over IRegionListViewModel -- the SAME
/// ViewModel the WinForms region grid binds, so per-field rules, the whole-list problem report,
/// the sort order and the add/delete commands are shared and cannot drift between backends.
///
/// Layout is a MASTER LIST plus a details pane (the two backends deliberately differ here: the
/// WinForms grid shows all thirteen columns, this one shows the eight scannable ones and moves
/// the asset descriptors and their free-form JSON into the pane).
///
/// READ-ONLY, for now. The grid and the pane display; nothing in this window writes to a region.
/// The editable pane is the next piece of work, and it is a separate one on purpose: the
/// DataGrid's control-owned edit pipeline commits a cell's value into the bound object and then
/// tells you about it, whereas the ViewModel must validate FIRST and refuse -- leaving the typed
/// text on screen and the model untouched. Those two are not reconcilable through cell binding,
/// which is why editing arrives with purpose-built editors instead.
///
/// Sorting belongs to the ViewModel. CanUserSortColumns is off and each column header's
/// pointer-press writes SortField/SortDescending and nothing else; the arrow in the header text
/// is drawn from that state. Letting the DataGrid sort would wrap the row collection in its own
/// ordered view, which would then disagree with the order the ViewModel hands out -- and with the
/// other backend.
///
/// The window hides on close (mirrors the WinForms host form), so its state survives reopening.
/// </summary>
internal sealed partial class RegionListWindow : Window
{
    private const string DeleteConfirmationCaption = "Confirm Delete";
    private const string DeleteConfirmationMessage = "Are you sure you want to delete this region?";

    // pale red, matching the WinForms bad-row tint: readable against the default row text.
    private static readonly IBrush InvalidRowBackground =
        new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0xE0));

    private static readonly IValueConverter ErrorToRowBackground =
        new FuncValueConverter<bool, IBrush?>(hasError => hasError ? InvalidRowBackground : null);

    // The tint alone is not enough: a selected row is painted with the selection brush, which
    // covers it, so the row a user is currently working in would show no marker at all. The row
    // header sits outside that fill, which is where the WinForms grid puts its marker too.
    private static readonly IValueConverter ErrorToRowHeaderGlyph =
        new FuncValueConverter<bool, string>(hasError => hasError ? "⚠" : "");

    private readonly Dictionary<RegionField, DataGridColumn> sortColumns = new();
    private readonly Dictionary<RegionField, string> headerCaptions = new();

    private readonly List<string> problemLines = [];

    private IRegionListViewModel? vm;
    private IRegionRowViewModel? subscribedRow;
    private bool syncingSelection;
    private bool problemsCollapsed;

    /// <summary>
    /// How the user is asked to confirm a delete. Deleting a region is destructive and the
    /// question belongs to the toolkit, never to the ViewModel, so the view asks it and only then
    /// calls the command. Replaceable so the wiring can be exercised without a dialog on screen;
    /// the default is the same question the WinForms window asks.
    /// </summary>
    internal Func<string, Task<bool>> ConfirmDelete { get; set; }

    public RegionListWindow()
    {
        InitializeComponent();

        ConfirmDelete = message => AvaloniaDialogs.ConfirmAsync(this, DeleteConfirmationCaption, message);

        // hide-on-close, exactly like the WinForms host form (the view instance and its ViewModel
        // survive; a later Show() re-shows the same window).
        Closing += (_, e) =>
        {
            e.Cancel = true;
            Hide();
        };

        WireSortableHeaders();

        ProblemsList.ItemsSource = problemLines;
        UpdateProblemsHeader();
        UpdateSortGlyphs();
        UpdateDetails();
    }

    /// <summary>
    /// Which ViewModel field each master column sorts on, IN THE ORDER THE COLUMNS ARE DECLARED
    /// in the markup. A DataGridColumn is not part of the window's name scope (it is not a
    /// control), so there are no generated fields to reach the columns by name. The pairing key is
    /// the column's DECLARED HEADER TEXT instead: each caption below must match one column's
    /// Header attribute in the markup exactly, and construction throws if it does not. Reordering
    /// or renaming a column therefore fails loudly here rather than quietly wiring a header to the
    /// wrong sort key.
    /// </summary>
    private static readonly (RegionField Field, string Caption)[] SortableColumns =
    [
        (RegionField.Start, "Start"),
        (RegionField.End, "End"),
        (RegionField.Length, "Length"),
        (RegionField.RegionName, "Region Name"),
        (RegionField.ContextToApply, "Label Context"),
        (RegionField.Priority, "Priority"),
        (RegionField.ExportSeparateFile, "Separate File"),
        (RegionField.ExportType, "Export Type"),
    ];

    private void WireSortableHeaders()
    {
        // secondary check: a column that gained no sort key at all would otherwise pass the
        // per-caption matching below and just be silently unsortable.
        if (RegionGrid.Columns.Count != SortableColumns.Length)
        {
            throw new InvalidOperationException(
                $"The region grid declares {RegionGrid.Columns.Count} columns but " +
                $"{SortableColumns.Length} of them are paired with a ViewModel field to sort on. " +
                "Every column must have exactly one sort key.");
        }

        foreach (var (field, caption) in SortableColumns)
        {
            var matches = RegionGrid.Columns.Where(c => (c.Header as string) == caption).ToList();
            if (matches.Count != 1)
            {
                throw new InvalidOperationException(
                    $"The region grid must declare exactly one column headed '{caption}', but " +
                    $"{matches.Count} were found. The header text declared in the markup is what " +
                    "pairs a column with the field it sorts on.");
            }

            var column = matches[0];
            sortColumns[field] = column;

            // read the caption back off the column rather than out of the table above: the markup
            // stays the one place the header text is written, and the sort glyph is the only thing
            // this window ever adds to it.
            headerCaptions[field] = (string)column.Header!;

            // the grid's own sorting is off, so a header press means exactly one thing: tell the
            // ViewModel to sort by this field. Left button only -- a right-click is a context
            // gesture, not a sort. (The WinForms grid sorts on any button; that difference is
            // deliberate and left alone there.)
            column.HeaderPointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    SortBy(field);
            };
        }
    }

    // ------------------------------------------------------------------ VM attach/detach

    public void AttachViewModel(IRegionListViewModel viewModel)
    {
        DetachViewModel();

        vm = viewModel;
        vm.PropertyChanged += Vm_PropertyChanged;
        ((INotifyCollectionChanged)vm.Problems).CollectionChanged += Problems_CollectionChanged;
        ((INotifyCollectionChanged)vm.Rows).CollectionChanged += Rows_CollectionChanged;

        // DataContext = the VM so the details pane's reflection bindings (SelectedRow.*) resolve.
        DataContext = vm;
        RegionGrid.ItemsSource = vm.Rows;

        SyncSelectionFromVm();
        UpdateStatusText();
        UpdateCounts();
        UpdateSortGlyphs();
        RefreshProblems();
        UpdateDetails();
    }

    public void DetachViewModel()
    {
        SubscribeToSelectedRow(null);

        if (vm == null)
            return;

        vm.PropertyChanged -= Vm_PropertyChanged;
        ((INotifyCollectionChanged)vm.Problems).CollectionChanged -= Problems_CollectionChanged;
        ((INotifyCollectionChanged)vm.Rows).CollectionChanged -= Rows_CollectionChanged;

        RegionGrid.ItemsSource = null;
        DataContext = null;
        vm = null;

        RefreshProblems();
        UpdateStatusText();
        UpdateCounts();
        UpdateDetails();
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IRegionListViewModel.StatusText):
                UpdateStatusText();
                break;
            case nameof(IRegionListViewModel.SelectedRow):
                SyncSelectionFromVm();
                UpdateDetails();
                break;
            case nameof(IRegionListViewModel.SortField):
            case nameof(IRegionListViewModel.SortDescending):
                UpdateSortGlyphs();
                break;
            case nameof(IRegionListViewModel.RegionCount):
                UpdateCounts();
                break;
        }
    }

    // persistent: the message stays until the next action replaces it.
    private void UpdateStatusText() => StatusText.Text = vm?.StatusText ?? "";

    private void UpdateCounts() =>
        CountsText.Text = vm == null
            ? ""
            : string.Format(CultureInfo.CurrentCulture, "{0:N0} regions", vm.RegionCount);

    // ------------------------------------------------------------------ selection

    private void RegionGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (syncingSelection || vm == null)
            return;

        var picked = RegionGrid.SelectedItem as IRegionRowViewModel;

        // SELECTION ONLY EVER TRAVELS VIEW -> VIEWMODEL WHEN IT NAMES A ROW.
        //
        // A single-selection grid gives the user no way to deselect by clicking, so a null here
        // is never a gesture: it is the grid dropping its own selection because the row
        // collection was restreamed underneath it. Re-sorting clears and refills that collection,
        // and the grid reacts to the clear while it is still empty -- so at this moment even the
        // row that is about to come straight back looks gone. Writing the null through would
        // destroy the selection the ViewModel owns and blank the details pane every time anyone
        // clicked a column header.
        //
        // Clearing the selection for real goes the other way: the ViewModel sets SelectedRow to
        // null (deleting the selected region does exactly that) and SyncSelectionFromVm pushes
        // that down.
        if (picked == null)
        {
            PostRestoreSelection();
            return;
        }

        syncingSelection = true;
        try
        {
            vm.SelectedRow = picked;
        }
        finally
        {
            syncingSelection = false;
        }
    }

    private void Rows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Same reset as above, seen from the other side: whichever of the two the grid gets to
        // first, the selection ends up back where the ViewModel says it is.
        if (e.Action == NotifyCollectionChangedAction.Reset)
            PostRestoreSelection();
    }

    /// <summary>
    /// Re-apply the ViewModel's selection to the grid, DEFERRED. The grid is mid-selection-update
    /// over a collection it has just been told to reset, and writing SelectedItem back into it
    /// from inside that update re-enters the same bookkeeping; running afterwards is safe.
    /// </summary>
    private void PostRestoreSelection() =>
        Dispatcher.UIThread.Post(SyncSelectionFromVm, DispatcherPriority.Background);

    private void SyncSelectionFromVm()
    {
        if (vm == null)
            return;

        // REENTRANT CALL. Writing vm.SelectedRow from the grid's own SelectionChanged reaches here
        // synchronously through the ViewModel's property notification, and pushing the value
        // straight back into the grid it just came from is pointless. Worse, this method's finally
        // would drop the guard flag while the caller's is still open, leaving the next edit
        // unguarded. Bail: the grid already shows what the ViewModel now holds.
        if (syncingSelection)
            return;

        var target = vm.SelectedRow;

        // Only a row the grid can actually select is worth pushing. A row that has left the list
        // would make SelectedItem stay null, and null coming back out of the grid triggers another
        // restore -- which would push the same absent row again, forever. Every writer of
        // SelectedRow today sets a member row or null, but the "a null from the grid is never a
        // gesture" rule below depends on that permanently, so it is checked rather than assumed.
        if (target != null && !vm.Rows.Contains(target))
            return;

        syncingSelection = true;
        try
        {
            RegionGrid.SelectedItem = target;
            if (target != null)
                RegionGrid.ScrollIntoView(target, null);
        }
        finally
        {
            syncingSelection = false;
        }
    }

    // ------------------------------------------------------------------ sorting

    private void SortBy(RegionField field)
    {
        if (vm == null)
            return;

        if (vm.SortField == field)
        {
            vm.SortDescending = !vm.SortDescending;
        }
        else
        {
            vm.SortField = field;
            vm.SortDescending = false;
        }
    }

    private void UpdateSortGlyphs()
    {
        foreach (var (field, column) in sortColumns)
        {
            var caption = headerCaptions[field];
            column.Header = vm != null && vm.SortField == field
                ? caption + (vm.SortDescending ? "  ▼" : "  ▲")
                : caption;
        }
    }

    // ------------------------------------------------------------------ bad-row marker

    private void RegionGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        // Bound rather than assigned, so the tint follows the row's error state for as long as
        // this container shows this row -- including after the container is recycled onto a
        // different one. A row is flagged while its stored values break a rule OR it is still
        // displaying text the model refused.
        e.Row[!BackgroundProperty] = new Binding(nameof(IRegionRowViewModel.HasError))
        {
            Mode = BindingMode.OneWay,
            Converter = ErrorToRowBackground,
        };
        e.Row[!DataGridRow.HeaderProperty] = new Binding(nameof(IRegionRowViewModel.HasError))
        {
            Mode = BindingMode.OneWay,
            Converter = ErrorToRowHeaderGlyph,
        };
        e.Row[!ToolTip.TipProperty] = new Binding(nameof(IRegionRowViewModel.ErrorText))
        {
            Mode = BindingMode.OneWay,
        };
    }

    // ------------------------------------------------------------------ details pane

    private void SubscribeToSelectedRow(IRegionRowViewModel? row)
    {
        if (ReferenceEquals(subscribedRow, row))
            return;

        if (subscribedRow != null)
            subscribedRow.PropertyChanged -= SelectedRow_PropertyChanged;

        subscribedRow = row;

        if (subscribedRow != null)
            subscribedRow.PropertyChanged += SelectedRow_PropertyChanged;
    }

    private void SelectedRow_PropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        UpdateDetails();

    private void UpdateDetails()
    {
        var row = vm?.SelectedRow;
        SubscribeToSelectedRow(row);

        // an unnamed region (or one whose name the user has just blanked) gets the bare caption
        // rather than a caption with a dangling separator.
        DetailsHeader.Text = string.IsNullOrWhiteSpace(row?.RegionNameText)
            ? "Region Details"
            : $"Region Details - {row.RegionNameText}";

        // greyed rather than hidden when the region's bytes are emitted as plain inline assembly:
        // hiding them would make it non-obvious that the feature exists, and the stored values
        // survive a round trip through Assembly and back.
        AssetFields.IsEnabled = row?.AssetFieldsEnabled ?? false;

        DetailsError.IsVisible = row is { HasError: true };
    }

    // ------------------------------------------------------------------ problem panel

    private void Problems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        RefreshProblems();

    private void RefreshProblems()
    {
        problemLines.Clear();
        if (vm != null)
        {
            foreach (var problem in vm.Problems)
                problemLines.Add(Describe(problem));
        }

        // plain List<string>: reassigning is how a non-observing source tells the ListBox to
        // re-read it, and the list is tiny.
        ProblemsList.ItemsSource = null;
        ProblemsList.ItemsSource = problemLines;

        UpdateProblemsHeader();
    }

    private static string Describe(RegionProblem problem) =>
        problem.Severity == RegionProblemSeverity.Warning
            ? $"Warning: {problem.Message}"
            : $"Error: {problem.Message}";

    private void ProblemsToggle_Click(object? sender, RoutedEventArgs e)
    {
        problemsCollapsed = !problemsCollapsed;
        ProblemsList.IsVisible = !problemsCollapsed;
        UpdateProblemsHeader();
    }

    private void UpdateProblemsHeader() =>
        ProblemsToggle.Content =
            $"{(problemsCollapsed ? "▶" : "▼")} Problems ({problemLines.Count})";

    // ------------------------------------------------------------------ commands

    private void BtnAdd_Click(object? sender, RoutedEventArgs e)
    {
        if (vm == null)
            return;

        // the new region is named and is already a legal one-byte range, so it is never a row the
        // user cannot leave. It lands wherever the current sort order puts it, which is why the
        // grid is told to select it rather than assuming it is at the bottom.
        vm.SelectedRow = vm.AddRegion();
    }

    private async void BtnDelete_Click(object? sender, RoutedEventArgs e)
    {
        // async void event handler: an exception must not tear down the shared message loop.
        try
        {
            await DeleteSelectedRegion();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RegionListWindow delete failed: {ex}");
        }
    }

    private async Task DeleteSelectedRegion()
    {
        var target = vm;
        var row = target?.SelectedRow;
        if (target == null || row == null)
            return;

        if (!await ConfirmDelete(DeleteConfirmationMessage))
            return;

        // BY ROW, never by row index: the grid shows regions in sort order, so a row index is not
        // an index into the stored collection and deleting by one removes the wrong region.
        // Re-read the ViewModel: the confirmation is awaited, so the world may have moved on.
        if (!ReferenceEquals(vm, target) || !target.Rows.Contains(row))
            return;

        target.DeleteRegion(row);
    }
}

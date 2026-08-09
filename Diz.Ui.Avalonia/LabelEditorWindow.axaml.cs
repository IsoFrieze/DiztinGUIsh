using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Diz.Core.Interfaces;
using Diz.Ui.ViewModels.Labels;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia label editor window: a passive view over ILabelEditorViewModel -- the SAME
/// ViewModel the WinForms editor binds (Diz.Ui.ViewModels; step 3 proved it bindable).
///
/// STEP 5b: layout converted from a code-built object tree to AXAML (LabelEditorWindow.axaml).
/// The window chrome (toolbar, sortable headers, list shell, splitter, Label Details pane +
/// Alternate Contexts editor, search bar, status bar) is declared in markup; this code-behind
/// keeps the parts that are genuinely imperative:
///  - the recycled row DataTemplate (per-cell edit tracking that must survive container
///    recycling -- see MakeCellEditor). Assigned to the ListBox.ItemTemplate below.
///  - VM attach/detach and the property-changed fan-out.
///  - commit/validate routing for both the grid cells and the details pane.
///
/// Widget choices, per the plan's perf gate (carried forward from the pre-AXAML version):
///  - ListBox + VirtualizingStackPanel. The Phase 0 spike measured 13 realized containers over
///    the 8,361-label CT corpus vs 8,361 (1.5 s) for a naive ItemsControl+StackPanel. A bare
///    ItemsControl must never be used for the label list.
///  - NOT Avalonia's DataGrid: separate package, long defect history, and its control-owned
///    edit/sort pipeline fights VM-owned editing (edits routed through vm.CommitEdit, not
///    two-way binding) and VM-owned sorting (header clicks). Plain TextBoxes in a recycled row
///    template keep the VM in charge.
///
/// Edits: cell TextBoxes bind ONE-WAY from the row VM; commits go through vm.CommitEdit on
/// Enter/LostFocus; Escape reverts. Invalid edits keep the typed text; vm.StatusText carries the
/// validator message to the status bar. The details-pane Name/Comment boxes use the SAME
/// discipline (one-way display, vm.CommitEdit on commit) so editing there and in the grid row
/// stay in sync through the shared VM. Alternate-context add/remove route through
/// vm.AddContextMapping/RemoveContextMapping; per-mapping Context/NameOverride edits two-way
/// bind to the context wrapper (whose setters write the model directly).
///
/// The window hides on close (mirrors the WinForms LabelEditorForm host behavior).
/// </summary>
internal sealed partial class LabelEditorWindow : Window
{
    // Shared column widths -- MUST match the header Grid in LabelEditorWindow.axaml.
    private const string RowColumnWidths = "110,240,*,150";

    private const string AddressCellTag = "address-cell";
    private const string NameCellTag = "name-cell";
    private const string CommentCellTag = "comment-cell";

    private readonly AvaloniaLabelEditorView host;
    private readonly Dictionary<LabelField, Button> sortHeaderButtons = new();

    private ILabelEditorViewModel? vm;
    private bool syncingSelection;
    private bool syncingSearch;

    // details-pane edit tracking: the row whose Name/Comment box currently has focus.
    private ILabelRowViewModel? editingDetailsRow;
    private bool committingDetails;

    /// <summary>Parameterless ctor required by the Avalonia XAML compiler (AVLN3000). Never
    /// used at runtime -- the window is always created via the host ctor. host stays null; no
    /// ctor-body code dereferences it, so tooling instantiation is still safe.</summary>
    public LabelEditorWindow() : this(null!) { }

    public LabelEditorWindow(AvaloniaLabelEditorView host)
    {
        this.host = host;

        InitializeComponent();

        // hide-on-close, exactly like the WinForms host form (the view instance and its
        // ViewModel survive; MainWindow's Show() call re-shows the same window).
        Closing += (_, e) =>
        {
            e.Cancel = true;
            Hide();
        };

        sortHeaderButtons[LabelField.Address] = HeaderAddress;
        sortHeaderButtons[LabelField.Name] = HeaderName;
        sortHeaderButtons[LabelField.Comment] = HeaderComment;

        // Row list: virtualized panel + recycled row template, both set in code (the perf gate).
        LabelList.ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel());
        LabelList.ItemTemplate = new FuncDataTemplate<ILabelRowViewModel>(
            (_, _) => BuildRowTemplate(), supportsRecycling: true);

        // Three of the four row cells are TextBoxes, which consume the pointer press themselves;
        // it never bubbles to the ListBoxItem, so a ListBox left to its own devices only selects
        // when the click lands on the one non-editable cell (Contexts). Select from a TUNNELING
        // handler instead: it runs before the TextBox sees the press, and deliberately leaves the
        // event unhandled so the click still lands the caret and starts an in-cell edit.
        LabelList.AddHandler(PointerPressedEvent, LabelList_PointerPressed, RoutingStrategies.Tunnel);
    }

    // ------------------------------------------------------------------ VM attach/detach

    public void AttachViewModel(ILabelEditorViewModel viewModel)
    {
        DetachViewModel();

        vm = viewModel;
        vm.PropertyChanged += Vm_PropertyChanged;

        // window DataContext = the VM so the details pane's reflection bindings
        // (SelectedRow.Name / .Comment / .ContextMappings) resolve.
        DataContext = vm;
        LabelList.ItemsSource = vm.Rows;

        SyncSearchBoxFromVm();
        SyncSelectionFromVm();
        UpdateStatusText();
        UpdateCounts();
        UpdateSortGlyphs();
        UpdateDetailsHeader();
        SyncConfidenceFromVm();
    }

    public void DetachViewModel()
    {
        if (vm == null)
            return;

        vm.PropertyChanged -= Vm_PropertyChanged;
        LabelList.ItemsSource = null;
        DataContext = null;
        vm = null;
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ILabelEditorViewModel.StatusText):
                UpdateStatusText();
                break;
            case nameof(ILabelEditorViewModel.SelectedRow):
                SyncSelectionFromVm();
                UpdateDetailsHeader();
                SyncConfidenceFromVm();
                break;
            case nameof(ILabelEditorViewModel.SearchTerm):
                SyncSearchBoxFromVm();
                break;
            case nameof(ILabelEditorViewModel.TotalLabelCount):
            case nameof(ILabelEditorViewModel.VisibleLabelCount):
                UpdateCounts();
                break;
            case nameof(ILabelEditorViewModel.SortField):
            case nameof(ILabelEditorViewModel.SortDescending):
                UpdateSortGlyphs();
                break;
        }
    }

    private void UpdateStatusText() => StatusText.Text = vm?.StatusText ?? "";

    private void UpdateCounts() =>
        CountsText.Text = vm == null
            ? ""
            : $"{vm.VisibleLabelCount:N0} / {vm.TotalLabelCount:N0} labels";

    private void UpdateDetailsHeader()
    {
        var row = vm?.SelectedRow;
        DetailsHeader.Text = row == null ? "Label Details" : $"Label Details - {row.AddressText}";
        DetailsBody.IsEnabled = row != null;
    }

    private void SyncSearchBoxFromVm()
    {
        if (vm == null || SearchBox.Text == vm.SearchTerm)
            return;
        syncingSearch = true;
        try
        {
            SearchBox.Text = vm.SearchTerm;
        }
        finally
        {
            syncingSearch = false;
        }
    }

    // ------------------------------------------------------------------ toolbar / search

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!syncingSearch && vm != null)
            vm.SearchTerm = SearchBox.Text ?? "";
    }

    private void BtnClearSearch_Click(object? sender, RoutedEventArgs e) => vm?.ClearSearch();
    private void BtnNewFromIa_Click(object? sender, RoutedEventArgs e) => host.NewLabelFromSelectedIa();
    private void BtnGoTo_Click(object? sender, RoutedEventArgs e) => vm?.JumpToSelectedInMainView();

    private async void BtnImportAppend_Click(object? sender, RoutedEventArgs e) =>
        await RunGuarded("Import CSV (Append)", () => host.ImportLabels(replaceAll: false));

    private async void BtnImportReplace_Click(object? sender, RoutedEventArgs e) =>
        await RunGuarded("Import CSV (Replace All)", () => host.ImportLabels(replaceAll: true));

    private async void BtnExport_Click(object? sender, RoutedEventArgs e) =>
        await RunGuarded("Export CSV", host.ExportLabels);

    private async void BtnNormalize_Click(object? sender, RoutedEventArgs e) =>
        await RunGuarded("Normalize WRAM Labels", host.NormalizeWramLabels);

    private static async Task RunGuarded(string what, Func<Task> op)
    {
        // async void event handlers: an exception must not tear down the shared message loop.
        try
        {
            await op();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LabelEditorWindow command '{what}' failed: {ex}");
        }
    }

    // ------------------------------------------------------------------ selection

    private void LabelList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (syncingSelection || vm == null)
            return;
        syncingSelection = true;
        try
        {
            vm.SelectedRow = LabelList.SelectedItem as ILabelRowViewModel;
        }
        finally
        {
            syncingSelection = false;
        }
    }

    /// <summary>Pressing anywhere in a row selects it -- including inside the cell TextBoxes,
    /// whose own pointer handling would otherwise keep the press from ever reaching the row
    /// container. Runs on the tunnel, so selection (and therefore the details pane) is already
    /// updated by the time the TextBox takes focus.</summary>
    private void LabelList_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (vm == null || e.Source is not global::Avalonia.Visual source)
            return;

        // includeSelf: the press may land on the container itself (the gaps between cells).
        if (source.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext
            is not ILabelRowViewModel row)
            return;

        // Assigning SelectedItem is the same path a click on the Contexts cell already takes:
        // SelectionChanged pushes it to vm.SelectedRow under the syncingSelection guard.
        if (!ReferenceEquals(LabelList.SelectedItem, row))
            LabelList.SelectedItem = row;
    }

    private void SyncSelectionFromVm()
    {
        if (syncingSelection || vm == null)
            return;
        syncingSelection = true;
        try
        {
            LabelList.SelectedItem = vm.SelectedRow;
            if (vm.SelectedRow != null)
                LabelList.ScrollIntoView(vm.SelectedRow);
        }
        finally
        {
            syncingSelection = false;
        }
    }

    private void LabelList_KeyDown(object? sender, KeyEventArgs e)
    {
        // only reaches here when a cell TextBox didn't consume the key (row selected, not
        // editing) -- same guard the WinForms grid needed explicitly.
        if (e.Key != Key.Delete || vm?.SelectedRow == null)
            return;
        vm.DeleteLabel(vm.SelectedRow.SnesAddress);
        e.Handled = true;
    }

    // ------------------------------------------------------------------ sorting

    private void Header_Click(object? sender, RoutedEventArgs e)
    {
        if (vm == null || sender is not Button { Tag: string tag } ||
            !Enum.TryParse<LabelField>(tag, out var field))
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

    private static readonly Dictionary<LabelField, string> HeaderCaptions = new()
    {
        [LabelField.Address] = "Address",
        [LabelField.Name] = "Name",
        [LabelField.Comment] = "Comment",
    };

    private void UpdateSortGlyphs()
    {
        foreach (var (field, button) in sortHeaderButtons)
        {
            var caption = HeaderCaptions[field];
            button.Content = vm != null && vm.SortField == field
                ? caption + (vm.SortDescending ? "  ▼" : "  ▲")
                : caption;
        }
    }

    // ------------------------------------------------------------------ row template

    private Control BuildRowTemplate()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(RowColumnWidths),
            Height = 20,
        };

        var addressCell = MakeCellEditor(LabelField.Address, nameof(ILabelRowViewModel.AddressText), AddressCellTag, monospace: true);
        var nameCell = MakeCellEditor(LabelField.Name, nameof(ILabelRowViewModel.Name), NameCellTag, monospace: true);
        var commentCell = MakeCellEditor(LabelField.Comment, nameof(ILabelRowViewModel.Comment), CommentCellTag);

        var contextsCell = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new global::Avalonia.Thickness(6, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            [!TextBlock.TextProperty] = new Binding(nameof(ILabelRowViewModel.ContextSummary)),
        };

        AddColumn(grid, 0, WrapInCellFrame(addressCell));
        AddColumn(grid, 1, WrapInCellFrame(nameCell));
        AddColumn(grid, 2, WrapInCellFrame(commentCell));
        AddColumn(grid, 3, contextsCell);

        // bottom hairline -> horizontal gridlines between rows
        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xED, 0xED, 0xED)),
            BorderThickness = new global::Avalonia.Thickness(0, 0, 0, 1),
            Child = grid,
        };
    }

    private static void AddColumn(Grid grid, int column, Control child)
    {
        Grid.SetColumn(child, column);
        grid.Children.Add(child);
    }

    // vertical gridline: a right hairline around each editable cell.
    private static Border WrapInCellFrame(Control child) => new()
    {
        BorderBrush = new SolidColorBrush(Color.FromRgb(0xE3, 0xE3, 0xE3)),
        BorderThickness = new global::Avalonia.Thickness(0, 0, 1, 0),
        Child = child,
        ClipToBounds = true,
    };

    private TextBox MakeCellEditor(LabelField field, string bindingPath, string tag, bool monospace = false)
    {
        var textBox = new TextBox
        {
            Tag = tag,
            Margin = new global::Avalonia.Thickness(0),
            Padding = new global::Avalonia.Thickness(4, 0),
            MinHeight = 0,
            BorderThickness = new global::Avalonia.Thickness(0),
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            // ONE-WAY: edits must flow through vm.CommitEdit, never the row's property setters.
            [!TextBox.TextProperty] = new Binding(bindingPath) { Mode = BindingMode.OneWay },
        };
        if (monospace)
            textBox.FontFamily = new FontFamily("Consolas, Courier New, monospace");

        // Recycling-safe edit tracking: capture the row when editing starts; a DataContext
        // change (container recycled mid-edit) discards the pending edit rather than committing
        // text against the wrong row.
        ILabelRowViewModel? editingRow = null;

        textBox.GotFocus += (_, _) => editingRow = textBox.DataContext as ILabelRowViewModel;
        textBox.DataContextChanged += (_, _) => editingRow = null;
        textBox.LostFocus += (_, _) => Commit();
        textBox.KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Enter:
                    Commit();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    Revert();
                    e.Handled = true;
                    break;
            }
        };

        return textBox;

        string CurrentValue(ILabelRowViewModel row) => field switch
        {
            LabelField.Address => row.AddressText,
            LabelField.Name => row.Name,
            _ => row.Comment,
        };

        void Commit()
        {
            var row = editingRow;
            if (row == null || vm == null)
                return;

            var proposed = textBox.Text ?? "";
            if (proposed == CurrentValue(row))
                return;

            vm.CommitEdit(row, field, proposed);
        }

        void Revert()
        {
            var row = editingRow ?? textBox.DataContext as ILabelRowViewModel;
            if (row != null)
                textBox.Text = CurrentValue(row);
        }
    }

    /// <summary>Select the row, scroll it into view, and (best-effort) focus its Name cell for
    /// editing -- the Avalonia equivalent of the WinForms BeginGridEditFor.</summary>
    public void FocusRowForEdit(ILabelRowViewModel? row)
    {
        if (row == null)
            return;

        LabelList.SelectedItem = row;
        LabelList.ScrollIntoView(row);

        Dispatcher.UIThread.Post(() =>
        {
            var container = LabelList.ContainerFromItem(row);
            var nameCell = container?
                .GetVisualDescendants()
                .OfType<TextBox>()
                .FirstOrDefault(tb => Equals(tb.Tag, NameCellTag));
            if (nameCell == null)
                return;
            nameCell.Focus();
            nameCell.SelectAll();
        }, DispatcherPriority.Background);
    }

    // ------------------------------------------------------------------ details pane: name/comment

    private void DetailsName_GotFocus(object? sender, RoutedEventArgs e) =>
        editingDetailsRow = vm?.SelectedRow;

    private void DetailsComment_GotFocus(object? sender, RoutedEventArgs e) =>
        editingDetailsRow = vm?.SelectedRow;

    private void DetailsName_LostFocus(object? sender, RoutedEventArgs e) =>
        CommitDetails(DetailsName, LabelField.Name);

    private void DetailsComment_LostFocus(object? sender, RoutedEventArgs e) =>
        CommitDetails(DetailsComment, LabelField.Comment);

    private void DetailsName_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                CommitDetails(DetailsName, LabelField.Name);
                e.Handled = true;
                break;
            case Key.Escape:
                DetailsName.Text = vm?.SelectedRow?.Name ?? "";
                e.Handled = true;
                break;
        }
    }

    private void DetailsComment_KeyDown(object? sender, KeyEventArgs e)
    {
        // Comment is multi-line (AcceptsReturn): Enter inserts a newline, so DON'T commit on
        // Enter -- commit happens on LostFocus. Escape reverts to the model value.
        if (e.Key == Key.Escape)
        {
            DetailsComment.Text = vm?.SelectedRow?.Comment ?? "";
            e.Handled = true;
        }
    }

    // ------------------------------------------------------------------ details pane: author

    private void DetailsAuthor_GotFocus(object? sender, RoutedEventArgs e) =>
        editingDetailsRow = vm?.SelectedRow;

    private void DetailsAuthor_LostFocus(object? sender, RoutedEventArgs e) =>
        CommitDetails(DetailsAuthor, LabelField.Author);

    private void DetailsAuthor_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                CommitDetails(DetailsAuthor, LabelField.Author);
                e.Handled = true;
                break;
            case Key.Escape:
                DetailsAuthor.Text = vm?.SelectedRow?.Author ?? "";
                e.Handled = true;
                break;
        }
    }

    // ------------------------------------------------------------------ details pane: confidence

    private void DetailsConfidence_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (committingDetails)
            return; // programmatic sync (SyncConfidenceFromVm) re-selects; don't re-commit.

        var row = vm?.SelectedRow;
        var target = vm;
        if (row == null || target == null)
            return;

        // items are DISPLAY strings; only commit a genuine user pick whose STORED value differs
        // from the row's current stored confidence.
        if (DetailsConfidence.SelectedItem is not string display)
            return;
        if (LabelEditorViewModel.ConfidenceDisplayToStored(display) == (row.Confidence ?? ""))
            return;

        // Same deferred-commit discipline as CommitDetails: the edited row is the selected row,
        // and CommitEdit's remove+add mutates the ListBox source while selection is still being
        // processed. Post past the current event. Confidence commits the display string (CommitEdit
        // maps it back to the stored value via ConfidenceDisplayToStored).
        committingDetails = true;
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                target.CommitEdit(row, LabelField.Confidence, display);
                editingDetailsRow = target.SelectedRow;
            }
            finally
            {
                committingDetails = false;
            }
        }, DispatcherPriority.Background);
    }

    // show the selected row's stored confidence as its display string WITHOUT committing. an
    // off-vocabulary value (not in ConfidenceOptions) can't be selected in a bound ComboBox, so it
    // shows blank; the stored value is never overwritten (only a user pick commits). The
    // committingDetails guard keeps the resulting SelectionChanged from re-committing.
    private void SyncConfidenceFromVm()
    {
        committingDetails = true;
        try
        {
            var row = vm?.SelectedRow;
            DetailsConfidence.SelectedItem = row == null
                ? null
                : LabelEditorViewModel.ConfidenceStoredToDisplay(row.Confidence ?? "");
        }
        finally
        {
            committingDetails = false;
        }
    }

    private void CommitDetails(TextBox box, LabelField field)
    {
        if (committingDetails)
            return; // Enter (KeyDown) + the LostFocus it triggers must not double-commit.

        var row = editingDetailsRow ?? vm?.SelectedRow;
        var target = vm;
        if (row == null || target == null)
            return;

        var proposed = box.Text ?? "";
        var current = field switch
        {
            LabelField.Name => row.Name,
            LabelField.Author => row.Author,
            _ => row.Comment,
        };
        if (proposed == current)
            return;

        // Same validate/commit discipline as the grid cells: invalid edits leave StatusText
        // set and the typed text in the box; a valid commit changes the row's identity
        // (remove+add) and moves SelectedRow to the new row; the one-way binding refreshes.
        //
        // DEFERRED (Dispatcher.Post): the edited row is the SELECTED row here, so CommitEdit's
        // remove+add mutates the ListBox source collection while Avalonia's SelectionModel is
        // still processing the selection change this very focus/key event kicked off. Avalonia
        // forbids that ("Source collection was modified during selection update"). Posting runs
        // the commit after the current event fully unwinds, which is safe.
        committingDetails = true;
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                target.CommitEdit(row, field, proposed);
                editingDetailsRow = target.SelectedRow;
            }
            finally
            {
                committingDetails = false;
            }
        }, DispatcherPriority.Background);
    }

    // ------------------------------------------------------------------ details pane: contexts

    private void BtnAddContext_Click(object? sender, RoutedEventArgs e)
    {
        var row = vm?.SelectedRow;
        if (row == null || vm == null)
            return;
        vm.AddContextMapping(row);
    }

    private void RemoveContext_Click(object? sender, RoutedEventArgs e)
    {
        var row = vm?.SelectedRow;
        if (row == null || vm == null)
            return;
        if (sender is Button { DataContext: IContextMappingViewModel mapping })
            vm.RemoveContextMapping(row, mapping);
    }
}

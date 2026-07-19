using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Diz.Ui.ViewModels.Labels;

namespace Diz.Ui.Avalonia;

/// <summary>
/// The Avalonia label editor window: a passive view over ILabelEditorViewModel -- the SAME
/// ViewModel the WinForms editor binds (Diz.Ui.ViewModels; step 3 proved it bindable).
///
/// Widget choices, per the plan's perf gate:
///  - ListBox + VirtualizingStackPanel (Avalonia 12's ListBox default; stated explicitly so
///    this file documents what it relies on). The Phase 0 spike measured 13 realized
///    containers over the 8,361-label CT corpus vs 8,361 (1.5 s) for a naive
///    ItemsControl+StackPanel. A bare ItemsControl must never be used here.
///  - NOT Avalonia's DataGrid: it lives in a separate package with a long defect history,
///    and everything the editor needs beyond a list -- in-place cell editing routed through
///    vm.ValidateEdit/CommitEdit rather than two-way binding, plus VM-owned sorting via
///    header clicks -- is exactly the part DataGrid would fight us on (its editing/sorting
///    pipeline is control-owned, like the WinForms grid's was). Three TextBoxes in a
///    recycled row template give in-place editing with the VM in charge.
///
/// Edits: cell TextBoxes bind ONE-WAY from the row VM (the row's property setters write the
/// model directly and bypass validation -- same reason the WinForms LabelGridRow setters
/// are no-ops). Commits go through vm.CommitEdit on Enter/LostFocus; Escape reverts.
/// Invalid edits keep the typed text so the user can correct it; vm.StatusText carries the
/// validator message to the status bar.
///
/// The window hides on close (mirrors the WinForms LabelEditorForm host behavior).
/// </summary>
internal sealed class LabelEditorWindow : Window
{
    private const string RowColumnWidths = "100,280,320,*";

    /// <summary>Tag marking the Name cell so FocusRowForEdit can find it in a container.</summary>
    private const string NameCellTag = "name-cell";

    private readonly AvaloniaLabelEditorView host;
    private readonly ListBox listBox;
    private readonly TextBox searchBox;
    private readonly TextBlock statusTextBlock;
    private readonly TextBlock countsTextBlock;
    private readonly Dictionary<LabelField, Button> sortHeaderButtons = new();

    private ILabelEditorViewModel? vm;
    private bool syncingSelection;
    private bool syncingSearch;

    public LabelEditorWindow(AvaloniaLabelEditorView host)
    {
        this.host = host;

        Title = "Label List"; // same window title the WinForms host form uses
        Width = 1050;
        Height = 640;

        // hide-on-close, exactly like the WinForms host form (the view instance and its
        // ViewModel survive; MainWindow's Show() call re-shows the same window).
        Closing += (_, e) =>
        {
            e.Cancel = true;
            Hide();
        };

        // ---------------- toolbar ----------------
        searchBox = new TextBox
        {
            Watermark = "Search...",
            MinWidth = 240,
            VerticalAlignment = VerticalAlignment.Center,
        };
        searchBox.TextChanged += (_, _) =>
        {
            if (!syncingSearch && vm != null)
                vm.SearchTerm = searchBox.Text ?? "";
        };

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new global::Avalonia.Thickness(8, 8, 8, 4),
            Children =
            {
                searchBox,
                MakeButton("Clear Search", () => vm?.ClearSearch()),
                MakeButton("New Label From IA", host.NewLabelFromSelectedIa),
                MakeButton("Go To", () => vm?.JumpToSelectedInMainView()),
                MakeAsyncButton("Import CSV (Append)...", () => host.ImportLabels(replaceAll: false)),
                MakeAsyncButton("Import CSV (Replace All)...", () => host.ImportLabels(replaceAll: true)),
                MakeAsyncButton("Export CSV...", host.ExportLabels),
                MakeAsyncButton("Normalize WRAM Labels", host.NormalizeWramLabels),
            },
        };

        // ---------------- sortable column headers ----------------
        var headerRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(RowColumnWidths),
            Margin = new global::Avalonia.Thickness(8, 0, 8, 0),
        };
        AddHeader(headerRow, 0, LabelField.Address, "Address");
        AddHeader(headerRow, 1, LabelField.Name, "Name");
        AddHeader(headerRow, 2, LabelField.Comment, "Comment");
        var contextsHeader = new TextBlock
        {
            Text = "Contexts",
            FontWeight = FontWeight.Bold,
            Margin = new global::Avalonia.Thickness(8, 6),
        };
        Grid.SetColumn(contextsHeader, 3);
        headerRow.Children.Add(contextsHeader);

        // ---------------- the list ----------------
        listBox = new ListBox
        {
            // Avalonia 12's ListBox already defaults to VirtualizingStackPanel; set it
            // explicitly so the perf-gate requirement is visible and pinned in code.
            ItemsPanel = new FuncTemplate<Panel>(() => new VirtualizingStackPanel()),
            ItemTemplate = new FuncDataTemplate<ILabelRowViewModel>(
                (_, _) => BuildRowTemplate(), supportsRecycling: true),
            SelectionMode = SelectionMode.Single,
            Margin = new global::Avalonia.Thickness(8, 0, 8, 0),
        };
        listBox.SelectionChanged += ListBox_SelectionChanged;
        listBox.KeyDown += ListBox_KeyDown;

        // ---------------- status bar ----------------
        statusTextBlock = new TextBlock { Margin = new global::Avalonia.Thickness(8, 4) };
        countsTextBlock = new TextBlock
        {
            Margin = new global::Avalonia.Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var statusBar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };
        Grid.SetColumn(statusTextBlock, 0);
        Grid.SetColumn(countsTextBlock, 1);
        statusBar.Children.Add(statusTextBlock);
        statusBar.Children.Add(countsTextBlock);

        // ---------------- layout ----------------
        var root = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(headerRow, Dock.Top);
        DockPanel.SetDock(statusBar, Dock.Bottom);
        root.Children.Add(toolbar);
        root.Children.Add(headerRow);
        root.Children.Add(statusBar);
        root.Children.Add(listBox); // fills remaining space
        Content = root;
    }

    // ------------------------------------------------------------------ VM attach/detach

    public void AttachViewModel(ILabelEditorViewModel viewModel)
    {
        DetachViewModel();

        vm = viewModel;
        vm.PropertyChanged += Vm_PropertyChanged;
        listBox.ItemsSource = vm.Rows;

        SyncSearchBoxFromVm();
        SyncSelectionFromVm();
        UpdateStatusText();
        UpdateCounts();
        UpdateSortGlyphs();
    }

    public void DetachViewModel()
    {
        if (vm == null)
            return;

        vm.PropertyChanged -= Vm_PropertyChanged;
        listBox.ItemsSource = null;
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
                break;
            case nameof(ILabelEditorViewModel.SearchTerm):
                // VM-side clears (FocusOrCreate*, ClearSearch) must reach the box too
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

    private void UpdateStatusText() => statusTextBlock.Text = vm?.StatusText ?? "";

    private void UpdateCounts() =>
        countsTextBlock.Text = vm == null
            ? ""
            : $"{vm.VisibleLabelCount:N0} / {vm.TotalLabelCount:N0} labels";

    private void SyncSearchBoxFromVm()
    {
        if (vm == null || searchBox.Text == vm.SearchTerm)
            return;
        syncingSearch = true;
        try
        {
            searchBox.Text = vm.SearchTerm;
        }
        finally
        {
            syncingSearch = false;
        }
    }

    // ------------------------------------------------------------------ selection

    private void ListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (syncingSelection || vm == null)
            return;
        syncingSelection = true;
        try
        {
            vm.SelectedRow = listBox.SelectedItem as ILabelRowViewModel;
        }
        finally
        {
            syncingSelection = false;
        }
    }

    private void SyncSelectionFromVm()
    {
        if (syncingSelection || vm == null)
            return;
        syncingSelection = true;
        try
        {
            listBox.SelectedItem = vm.SelectedRow;
            if (vm.SelectedRow != null)
                listBox.ScrollIntoView(vm.SelectedRow);
        }
        finally
        {
            syncingSelection = false;
        }
    }

    private void ListBox_KeyDown(object? sender, KeyEventArgs e)
    {
        // only reaches here when a cell TextBox didn't consume the key (i.e. row selected,
        // not editing) -- same guard the WinForms grid needed explicitly.
        if (e.Key != Key.Delete || vm?.SelectedRow == null)
            return;
        vm.DeleteLabel(vm.SelectedRow.SnesAddress);
        e.Handled = true;
    }

    // ------------------------------------------------------------------ sorting

    private void AddHeader(Grid headerRow, int column, LabelField field, string text)
    {
        var button = new Button
        {
            Content = text,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            FontWeight = FontWeight.Bold,
            Background = Brushes.Transparent,
        };
        button.Click += (_, _) =>
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
        };
        sortHeaderButtons[field] = button;
        Grid.SetColumn(button, column);
        headerRow.Children.Add(button);
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
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(RowColumnWidths) };

        var addressCell = MakeCellEditor(LabelField.Address, nameof(ILabelRowViewModel.AddressText), monospace: true);
        var nameCell = MakeCellEditor(LabelField.Name, nameof(ILabelRowViewModel.Name), monospace: true);
        nameCell.Tag = NameCellTag;
        var commentCell = MakeCellEditor(LabelField.Comment, nameof(ILabelRowViewModel.Comment));
        var contextsCell = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new global::Avalonia.Thickness(6, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            [!TextBlock.TextProperty] = new Binding(nameof(ILabelRowViewModel.ContextSummary)),
        };

        Grid.SetColumn(addressCell, 0);
        Grid.SetColumn(nameCell, 1);
        Grid.SetColumn(commentCell, 2);
        Grid.SetColumn(contextsCell, 3);
        grid.Children.Add(addressCell);
        grid.Children.Add(nameCell);
        grid.Children.Add(commentCell);
        grid.Children.Add(contextsCell);
        return grid;
    }

    private TextBox MakeCellEditor(LabelField field, string bindingPath, bool monospace = false)
    {
        var textBox = new TextBox
        {
            Margin = new global::Avalonia.Thickness(1),
            BorderThickness = new global::Avalonia.Thickness(0),
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
            // ONE-WAY: edits must flow through vm.CommitEdit, never through the row's
            // property setters (which write the model unvalidated).
            [!TextBox.TextProperty] = new Binding(bindingPath) { Mode = BindingMode.OneWay },
        };
        if (monospace)
            textBox.FontFamily = new FontFamily("Consolas, Courier New, monospace");

        // Recycling-safe edit tracking: capture the row when editing starts; a DataContext
        // change (container recycled to another row mid-edit) discards the pending edit
        // rather than committing text against the wrong row.
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

            // On invalid: vm.CommitEdit set StatusText to the validator message (shown in
            // the status bar); the typed text stays in the box for correction.
            // On valid name/comment: the row raises INPC and the one-way binding refreshes.
            // On valid address: the row is remove+re-added (identity change) and the VM
            // moves SelectedRow to the new row; this container gets recycled away.
            vm.CommitEdit(row, field, proposed);
        }

        void Revert()
        {
            var row = editingRow ?? textBox.DataContext as ILabelRowViewModel;
            if (row != null)
                textBox.Text = CurrentValue(row);
        }
    }

    /// <summary>Select the row, scroll it into view, and (best-effort) focus its Name cell
    /// for editing -- the Avalonia equivalent of the WinForms BeginGridEditFor.</summary>
    public void FocusRowForEdit(ILabelRowViewModel? row)
    {
        if (row == null)
            return;

        listBox.SelectedItem = row;
        listBox.ScrollIntoView(row);

        // container realization happens in a layout pass; focus after it.
        Dispatcher.UIThread.Post(() =>
        {
            var container = listBox.ContainerFromItem(row);
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

    // ------------------------------------------------------------------ helpers

    private static Button MakeButton(string text, Action onClick)
    {
        var button = new Button { Content = text };
        button.Click += (_, _) => onClick();
        return button;
    }

    private static Button MakeAsyncButton(string text, Func<Task> onClick)
    {
        var button = new Button { Content = text };
        // async void equivalent: exceptions must not tear down the shared message loop
        button.Click += async (_, _) =>
        {
            try
            {
                await onClick();
            }
            catch (Exception ex)
            {
                statusFallback(ex);
            }
        };
        return button;

        void statusFallback(Exception ex) =>
            System.Diagnostics.Debug.WriteLine($"LabelEditorWindow command '{text}' failed: {ex}");
    }
}

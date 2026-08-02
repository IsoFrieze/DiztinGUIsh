using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Diz.Core.Interfaces;
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
/// ALL EDITING HAPPENS IN THE PANE. The master grid is read-only, deliberately: the DataGrid's
/// control-owned edit pipeline commits a cell's value into the bound object and then tells you
/// about it, whereas the ViewModel must validate FIRST and refuse -- leaving the typed text on
/// screen and the model untouched. Those two are not reconcilable through two-way cell binding,
/// so the pane carries purpose-built editors and the grid displays the result.
///
/// The pane's editors are NOT bound. Values are written into them by hand (see WriteText) and
/// read back out on Enter or focus loss; a binding would fight the caret, because the text a box
/// holds mid-edit is exactly the text the ViewModel has not accepted yet.
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

    /// <summary>Every free-text editor in the details pane, in the order the pane shows them.</summary>
    private readonly List<DetailEditor> detailEditors = [];

    /// <summary>The same editors, keyed by the row property whose change means "rewrite this box".</summary>
    private readonly Dictionary<string, DetailEditor> editorsByRowProperty = new(StringComparer.Ordinal);

    /// <summary>
    /// The pane's CLOSED-VALUE editors, by the field each one edits. Tracked rather than just
    /// wired, so the completeness check below can prove every such field has a widget: a field
    /// whose value space is closed cannot be given a text box, and one added to the ViewModel and
    /// forgotten here would sit on screen doing nothing.
    /// </summary>
    private readonly Dictionary<RegionField, Control> closedValueEditors = new();

    /// <summary>
    /// Fields whose box currently holds text the USER put there, i.e. an edit that has not been
    /// offered to the ViewModel yet. Only these are committed, so an untouched box can never
    /// write its own displayed value back into the model, and swapping rows can never carry one
    /// region's half-typed text onto another.
    /// </summary>
    private readonly HashSet<RegionField> typedFields = [];

    private IRegionListViewModel? vm;
    private IRegionRowViewModel? subscribedRow;
    private bool syncingSelection;
    private bool problemsCollapsed;

    /// <summary>
    /// Which region the pane's editors are currently editing. It is not simply "whatever is
    /// selected now": a commit is decided after the fact (see CommitEditor), and by then the
    /// selection may have moved on -- so the row the text was typed AGAINST has to be remembered
    /// while it still is the selected one. Pane-wide rather than per box, because every editor in
    /// the pane always shows the same region.
    /// </summary>
    private IRegionRowViewModel? detailEditingRow;

    // true while widget values are being written FROM the ViewModel; the input handlers below
    // bail out then, so a ViewModel-driven refresh can't be mistaken for the user typing.
    // Only catches handlers that run inside the write itself -- TextChanged does not (see
    // PushText), so the text boxes need the value comparison there as well as this flag.
    private bool updatingWidgets;

    /// <summary>
    /// One free-text field of the details pane: the box that edits it, the label that names it
    /// (and carries its bad-field marker), and the row property whose change means the box is out
    /// of date. <see cref="Caption"/> is read off the label at construction so the MARKUP stays
    /// the one place a field's name is written.
    /// </summary>
    private sealed record DetailEditor(
        RegionField Field, TextBox Box, TextBlock Label, string Caption, string RowProperty);

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
        WireDetailEditors();

        ProblemsList.ItemsSource = problemLines;
        UpdateProblemsHeader();
        UpdateSortGlyphs();
        SwapDetailRow();
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

    // ------------------------------------------------------------------ details pane wiring

    /// <summary>
    /// Give every editor in the details pane the same treatment, from one place, so a field
    /// cannot be added to the markup and quietly left without the commit discipline the others
    /// have.
    /// </summary>
    private void WireDetailEditors()
    {
        // the export types the model actually defines, so a new one appears here for free.
        CboExportType.ItemsSource = Enum.GetNames<RegionExportType>();

        Register(RegionField.Start, BoxStart, LblStart, nameof(IRegionRowViewModel.StartText));
        Register(RegionField.End, BoxEnd, LblEnd, nameof(IRegionRowViewModel.EndText));
        Register(RegionField.Length, BoxLength, LblLength, nameof(IRegionRowViewModel.LengthText));
        Register(RegionField.RegionName, BoxName, LblName, nameof(IRegionRowViewModel.RegionNameText));
        Register(RegionField.ContextToApply, BoxContext, LblContext,
            nameof(IRegionRowViewModel.ContextToApplyText));
        Register(RegionField.Priority, BoxPriority, LblPriority, nameof(IRegionRowViewModel.PriorityText));
        Register(RegionField.AssetType, BoxAssetType, LblAssetType, nameof(IRegionRowViewModel.AssetTypeText));
        Register(RegionField.AssetVersion, BoxAssetVersion, LblAssetVersion,
            nameof(IRegionRowViewModel.AssetVersionText));
        Register(RegionField.AssetName, BoxAssetName, LblAssetName, nameof(IRegionRowViewModel.AssetNameText));
        Register(RegionField.AssetOptions, BoxAssetOptions, LblAssetOptions,
            nameof(IRegionRowViewModel.AssetOptionsText));

        // The two CLOSED-VALUE editors. A checkbox and a combo can only ever show a legal value,
        // so a refused edit leaves no typed text behind: the widget is put back to what the model
        // holds and the row gains no marker. Both report synchronously from inside the write that
        // caused them, so the updatingWidgets flag alone is enough to tell a ViewModel-driven
        // refresh from a user's pick -- unlike TextChanged (see PushText).
        closedValueEditors[RegionField.ExportSeparateFile] = ChkSeparateFile;
        closedValueEditors[RegionField.ExportType] = CboExportType;

        ChkSeparateFile.IsCheckedChanged += (_, _) =>
            CommitClosedValue(RegionField.ExportSeparateFile,
                (ChkSeparateFile.IsChecked == true).ToString());

        CboExportType.SelectionChanged += (_, _) =>
            CommitClosedValue(RegionField.ExportType, CboExportType.SelectedItem as string ?? "");

        VerifyEveryFieldHasAnEditor();
        return;

        void Register(RegionField field, TextBox box, TextBlock label, string rowProperty)
        {
            var editor = new DetailEditor(field, box, label, label.Text ?? field.ToString(), rowProperty);
            detailEditors.Add(editor);
            editorsByRowProperty[rowProperty] = editor;

            box.GotFocus += (_, _) => detailEditingRow = vm?.SelectedRow;
            box.TextChanged += (_, _) => PushText(box, field);
            box.LostFocus += (_, _) => CommitEditor(editor);
            box.KeyDown += (_, e) =>
            {
                switch (e.Key)
                {
                    case Key.Enter:
                        CommitEditor(editor);
                        e.Handled = true;
                        break;
                    case Key.Escape:
                        RevertEditor(editor);
                        e.Handled = true;
                        break;
                }
            };
        }
    }

    /// <summary>
    /// Prove, at construction, that the pane really does edit every field a region has -- and
    /// that the markup contains no editor this wiring never claimed.
    ///
    /// Both halves fail SILENTLY at runtime otherwise, which is the worst way for an editor to be
    /// wrong: a field with no editor is simply not editable and nobody notices until someone needs
    /// it, and a text box that was never registered accepts typing and commits nothing. Growing
    /// the ViewModel's field set, or the markup, therefore breaks the window loudly here instead.
    /// </summary>
    private void VerifyEveryFieldHasAnEditor()
    {
        var fields = Enum.GetValues<RegionField>();

        var missing = fields
            .Where(f => f.DisplaysTypedText()
                ? detailEditors.All(e => e.Field != f)
                : !closedValueEditors.ContainsKey(f))
            .ToList();
        if (missing.Count != 0)
        {
            throw new InvalidOperationException(
                "The region details pane has no editor for these region field(s): " +
                $"{string.Join(", ", missing)}. Every field must be editable somewhere in the " +
                "pane -- free-text fields through a text box that can keep refused text on " +
                "screen, closed-value fields through a widget that can only show a legal value.");
        }

        // catches the other direction: a field wired twice, or an editor registered for something
        // that is no longer a field.
        if (detailEditors.Count + closedValueEditors.Count != fields.Length)
        {
            throw new InvalidOperationException(
                $"The region details pane wires {detailEditors.Count} text editor(s) and " +
                $"{closedValueEditors.Count} closed-value editor(s) for {fields.Length} region " +
                "field(s). Each field must be wired exactly once.");
        }

        var unwired = DetailsPane.GetLogicalDescendants().OfType<TextBox>()
            .Where(box => detailEditors.All(e => !ReferenceEquals(e.Box, box)))
            .Select(box => box.Tag as string ?? box.Name ?? "<unnamed>")
            .ToList();
        if (unwired.Count != 0)
        {
            throw new InvalidOperationException(
                "The region details pane declares text box(es) that are not paired with a region " +
                $"field: {string.Join(", ", unwired)}. Every editor in the pane must be registered " +
                "in the wiring, or it accepts typing and commits nothing.");
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
        SwapDetailRow();
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
        SwapDetailRow();
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
                SwapDetailRow();
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

    private void RegionGrid_KeyDown(object? sender, KeyEventArgs e)
    {
        // Delete on the grid is the toolbar button's gesture, not a second delete path: same
        // confirmation, same by-identity removal, asked exactly once.
        if (e.Key != Key.Delete || vm?.SelectedRow == null)
            return;

        e.Handled = true;
        BeginDeleteSelectedRegion();
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

    /// <summary>
    /// A different region is on screen: re-stock every editor from it and forget anything the
    /// user had half-typed for the previous one. Nothing is committed on the way past -- text a
    /// region never accepted belongs to the region it was typed against, and carrying it onto the
    /// next one would write a value the user never meant into a region they only glanced at.
    /// </summary>
    private void SwapDetailRow()
    {
        var row = vm?.SelectedRow;
        SubscribeToSelectedRow(row);

        typedFields.Clear();

        // Anything typed from here on belongs to the region now on screen -- including into a box
        // that already had the caret when the selection moved (adding a region selects it, and the
        // user may well be mid-field when a rebind or an import re-points the pane). Leaving the
        // previous region recorded here would make every such edit look like it was typed against
        // a region that is no longer showing, and CommitEditor would correctly drop it.
        detailEditingRow = row;

        WriteWidgets(() =>
        {
            foreach (var editor in detailEditors)
                editor.Box.Text = row?.TextFor(editor.Field) ?? "";

            ChkSeparateFile.IsChecked = row?.ExportSeparateFile ?? false;
            CboExportType.SelectedItem = row?.ExportType.ToString();
        });

        UpdateDetailChrome();
    }

    /// <summary>
    /// One field of the shown region moved (or its error state did). Rewrite exactly that box and
    /// nothing else: rewriting the whole pane would throw away text the user is part-way through
    /// typing into some OTHER field.
    /// </summary>
    private void SelectedRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var row = vm?.SelectedRow;
        if (row == null || !ReferenceEquals(sender, row))
            return;

        var property = e.PropertyName ?? "";

        if (editorsByRowProperty.TryGetValue(property, out var editor))
        {
            // The ViewModel's answer for this field changed, so what is in the box is out of date
            // -- INCLUDING when the box is the one being typed into. That case is the point: a
            // refused edit comes back as the typed text (keep showing it), an accepted one comes
            // back canonicalised (pasting the label "CODE_C012AB" leaves "C012AB"), and moving
            // Start restates the Length that was derived from it.
            //
            // A change this window did NOT cause -- an import, a migration, another view editing
            // the same region -- lands here too, and also wins over half-typed text. That is the
            // intended trade: the region really did move, and leaving a stale edit on screen over
            // it would let the user commit it back on top without ever seeing what happened.
            typedFields.Remove(editor.Field);
            WriteText(editor.Box, row.TextFor(editor.Field));
        }
        else if (property is nameof(IRegionRowViewModel.ExportSeparateFile)
                 or nameof(IRegionRowViewModel.ExportType))
        {
            SyncClosedValueEditors(row);
        }

        UpdateDetailChrome();
    }

    /// <summary>Everything about the pane that is not a value: the caption, what is enabled, and
    /// which fields are wearing the "the model refused this" marker.</summary>
    private void UpdateDetailChrome()
    {
        var row = vm?.SelectedRow;

        // an unnamed region (or one whose name the user has just blanked) gets the bare caption
        // rather than a caption with a dangling separator.
        DetailsHeader.Text = string.IsNullOrWhiteSpace(row?.RegionNameText)
            ? "Region Details"
            : $"Region Details - {row.RegionNameText}";

        // nothing selected: there is no region to edit, so the editors are dead rather than
        // showing an empty region that does not exist.
        DetailsPane.IsEnabled = row != null;

        // greyed rather than hidden when the region's bytes are emitted as plain inline assembly:
        // hiding them would make it non-obvious that the feature exists, and the stored values
        // survive a round trip through Assembly and back.
        AssetFields.IsEnabled = row?.AssetFieldsEnabled ?? false;

        DetailsError.IsVisible = row is { HasError: true };

        foreach (var editor in detailEditors)
        {
            // Which field is wearing the error: it is displaying text the model would not take.
            // A field can also hold text the model merely IGNORED -- an emptied numeric box is not
            // a mistake -- and that must not be marked, which is why the row's own error state has
            // to agree before a field is flagged.
            var bad = row is { HasError: true } && row.HasPendingTextFor(editor.Field);

            editor.Label.Text = bad ? "⚠ " + editor.Caption : editor.Caption;
            editor.Label.Classes.Set("field-bad", bad);
            editor.Box.Classes.Set("field-bad", bad);
        }
    }

    // ------------------------------------------------------------------ details pane: commits

    /// <summary>
    /// Offer one box's text to the ViewModel. Runs on Enter and on focus leaving the box; the
    /// ViewModel validates, and either writes the one field or refuses and hands the typed text
    /// back for the box to keep showing.
    ///
    /// DEFERRED (Dispatcher.Post) and re-checked when it runs. Focus leaves a box for two very
    /// different reasons: the user moved to the next field, or they clicked a different region in
    /// the master grid. In the second case the pane is about to belong to another region, and
    /// committing would either write the old region behind the user's back or -- worse -- land
    /// the text on whichever row is selected by then. Posting lets the selection settle first;
    /// the commit then happens only if the row it was typed against is still the one on screen.
    /// It also keeps the write out of the grid's own selection bookkeeping, which forbids the row
    /// collection moving underneath it (a commit can re-sort).
    /// </summary>
    private void CommitEditor(DetailEditor editor)
    {
        var target = vm;
        var row = detailEditingRow ?? target?.SelectedRow;
        if (target == null || row == null)
            return;

        // Nothing the user typed, nothing to say. Also stops Enter and the focus change it can
        // cause from each committing the same text.
        if (!typedFields.Remove(editor.Field))
            return;

        var proposed = editor.Box.Text ?? "";
        if (string.Equals(proposed, row.TextFor(editor.Field), StringComparison.Ordinal))
            return;

        var field = editor.Field;
        Dispatcher.UIThread.Post(() =>
        {
            var live = vm;
            if (live == null || !ReferenceEquals(live.SelectedRow, row) || !live.Rows.Contains(row))
                return;

            live.CommitField(row, field, proposed);
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Escape: give up on this field's edit. The ViewModel drops the typed text and any refusal
    /// that came with it, and the box goes back to the value the region holds.
    ///
    /// The ViewModel has to be told, not just the box. If only the box were rewritten, the
    /// ViewModel would still be holding the abandoned text: the field would keep its marker while
    /// displaying a perfectly good value, and the box's own value would read as a fresh edit and
    /// be committed again the moment focus left -- which on a row whose STORED values already
    /// break a rule comes straight back as a refusal, pinning the marker on a field the user had
    /// just backed out of.
    /// </summary>
    private void RevertEditor(DetailEditor editor)
    {
        var target = vm;
        var row = target?.SelectedRow;
        if (target == null || row == null)
            return;

        typedFields.Remove(editor.Field);
        target.RevertField(row, editor.Field);

        // After the revert the box and the ViewModel agree, so the write's own TextChanged echo
        // compares equal and does not re-arm a commit. (Belt and braces: the revert itself
        // notifies, and this window rewrites the box from that -- but only when there WAS text to
        // drop, and Escape out of an uncommitted edit is the case where there was not.)
        WriteText(editor.Box, row.TextFor(editor.Field));
    }

    /// <summary>
    /// Commit a closed-value field (the checkbox, the combo). These commit the moment the user
    /// picks, because there is nothing to finish typing -- and on a refusal the widget is put back
    /// to the committed value, since it has no way to display the value the model rejected.
    /// </summary>
    private void CommitClosedValue(RegionField field, string text)
    {
        if (updatingWidgets)
            return;

        var target = vm;
        var row = target?.SelectedRow;
        if (target == null || row == null)
            return;

        var result = target.CommitField(row, field, text);
        if (!result.IsValid)
            SyncClosedValueEditors(row);
    }

    private void SyncClosedValueEditors(IRegionRowViewModel? row) =>
        WriteWidgets(() =>
        {
            ChkSeparateFile.IsChecked = row?.ExportSeparateFile ?? false;
            CboExportType.SelectedItem = row?.ExportType.ToString();
        });

    // ------------------------------------------------------------------ details pane: plumbing

    /// <summary>
    /// Note that a box holds text the user typed -- unless the box is only reporting back what
    /// this window itself just put there.
    ///
    /// The updatingWidgets flag cannot decide that on its own. Avalonia raises TextChanged on a
    /// LATER dispatcher turn, not inside the Text setter, so by the time the event arrives the
    /// flag is down again and a write this window made is indistinguishable from a keystroke.
    /// Comparing the text instead does not depend on when the event fires.
    ///
    /// Letting an echo through is not harmless. A box holding the ViewModel's own value has
    /// nothing new to say, but recording it as an edit arms a commit: the next Enter or focus
    /// change writes that value back, and the answer need not be the text on screen. Start, End
    /// and Length are three views of one range, so committing one restates the others, and an
    /// address is canonicalised on the way in -- so an echo taken for a keystroke retypes the
    /// user's field under their caret, mid-word, and can drag a neighbouring field with it.
    ///
    /// The comparison is against what the VIEWMODEL says the field shows, and every write this
    /// window makes puts exactly that in the box (WriteText, and the revert path first tells the
    /// ViewModel so the two still agree) -- so the rule holds with no exceptions: if the box
    /// differs from the ViewModel, a person put it there.
    /// </summary>
    private void PushText(TextBox box, RegionField field)
    {
        if (updatingWidgets)
            return;

        var row = vm?.SelectedRow;
        if (row == null)
            return;

        var text = box.Text ?? "";
        if (string.Equals(text, row.TextFor(field), StringComparison.Ordinal))
            return;

        typedFields.Add(field);
    }

    /// <summary>Write widget state without the input handlers treating it as user input.</summary>
    private void WriteWidgets(Action write)
    {
        var previous = updatingWidgets;
        updatingWidgets = true;
        try
        {
            write();
        }
        finally
        {
            updatingWidgets = previous;
        }
    }

    /// <summary>
    /// Push text into a box, INCLUDING the one being typed into. That is deliberate: this is only
    /// reached when the ViewModel's own text for the field changed, and that change has to be
    /// visible -- it is either the value the model settled on or the very text it refused.
    /// </summary>
    private void WriteText(TextBox textBox, string text) => WriteWidgets(() => textBox.Text = text);

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

    private void BtnDelete_Click(object? sender, RoutedEventArgs e) => BeginDeleteSelectedRegion();

    private async void BeginDeleteSelectedRegion()
    {
        // async void: this is the end of an event handler's call chain, and an exception escaping
        // it must not tear down the shared message loop.
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

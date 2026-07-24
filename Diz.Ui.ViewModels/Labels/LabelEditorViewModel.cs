using System.Collections.ObjectModel;
using System.Globalization;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.util;
using Diz.Import;
using Diz.Import.bsnes;

namespace Diz.Ui.ViewModels.Labels;

/// <summary>
/// The label editor's logic, extracted from the 946-line WinForms code-behind
/// (Diz.Ui.Winforms/usercontrols/LabelsView.cs) into pure, toolkit-free, testable state.
///
/// Sources rows from the label provider and applies INCREMENTAL updates from the payloaded
/// LabelsChanged event (Step 0): Added/Removed/Replaced touch one row; only BulkReset
/// rebuilds. Known provider quirk, handled idempotently here: AddLabel with overwrite:false
/// on an existing address still reports Replaced (over-reports, never under-reports).
///
/// PORT NOTES -- differences from the WinForms original, each deliberate and documented in
/// docs/diz/new-ui-plan.md Step 2:
///  1. Address-edit onto an occupied address is REJECTED ("This address already has a
///     label."). The WinForms guard with that exact message was dead code -- its condition
///     `existingSnesAddress == -1` was unreachable, because int.TryParse writes 0 (not -1) on
///     failure and every grid row had a parseable address -- so re-addressing silently
///     OVERWROTE the occupant's label. Reviving the guard is a fix, not an oversight.
///  2. Name edits are validated with LabelNameValidator.Strict. The WinForms editor validated
///     nothing (`// todo (validate for valid label characters)`), which let you type a name
///     the importer would later reject.
///  3. FocusOrCreateAtSnesAddress creates the label in the PROVIDER immediately. WinForms
///     added a phantom DataTable row that only reached the model once the cell edit was
///     committed; rows here always mirror the provider, so a phantom row cannot exist.
///  4. Preserved from WinForms: every commit builds a FRESH Label (carrying over the context
///     mappings of the label previously at that address) and does RemoveLabel + AddLabel
///     (overwrite:true) -- even for a name/comment-only edit. Label object identity changes on
///     every commit, exactly as before.
/// </summary>
public sealed class LabelEditorViewModel : ViewModelNotifierBase, ILabelEditorViewModel
{
    private readonly ILabelProvider labels;
    private readonly NormalizeWramLabelsPort normalizeWramLabels;
    private readonly ResolveRomOffsetToSnesIaPort? resolveRomOffsetToSnesIa;

    // master set: every label, keyed by address. 'visibleRows' is the filtered+sorted subset.
    private readonly Dictionary<int, LabelRowViewModel> rowsByAddress = new();
    private readonly ObservableCollection<ILabelRowViewModel> visibleRows = [];

    private LabelSearchTerms searchMatcher = new("");
    private string searchTerm = "";
    private LabelField sortField = LabelField.Address;
    private bool sortDescending;
    private ILabelRowViewModel? selectedRow;
    private string statusText = "";
    private bool isBusy;
    private bool disposed;

    /// <param name="labelProvider">the model. Mutations go through it; its LabelsChanged
    /// event drives the row pipeline.</param>
    /// <param name="notificationMarshaller">runs every notification; a real host passes
    /// "execute on the UI thread" (send-if-off-thread semantics -- see
    /// <see cref="ViewModelNotifierBase"/>). null (unit tests) = invoke inline.</param>
    /// <param name="normalizeWramLabels">optional override; defaults to Diz.Core's
    /// LabelProviderExtensions.NormalizeWramLabels on <paramref name="labelProvider"/>.</param>
    /// <param name="resolveRomOffsetToSnesIa">PROVISIONAL port, see <see cref="ResolveRomOffsetToSnesIaPort"/>.</param>
    /// <param name="confidenceLevels">the project's confidence vocabulary (worst -> best), used to
    /// build <see cref="ConfidenceOptions"/>. null (tests / no project) uses
    /// ProjectSettings.DefaultConfidenceLevels.
    /// TODO: source ConfidenceLevels from the loaded project's ProjectSettings at the construction seam.</param>
    public LabelEditorViewModel(
        ILabelProvider labelProvider,
        Action<Action>? notificationMarshaller = null,
        NormalizeWramLabelsPort? normalizeWramLabels = null,
        ResolveRomOffsetToSnesIaPort? resolveRomOffsetToSnesIa = null,
        IEnumerable<string>? confidenceLevels = null)
        : base(notificationMarshaller)
    {
        labels = labelProvider;
        // default = the real Core implementation (extension method group -> delegate)
        this.normalizeWramLabels = normalizeWramLabels ?? labelProvider.NormalizeWramLabels;
        this.resolveRomOffsetToSnesIa = resolveRomOffsetToSnesIa;

        // dropdown vocabulary = "(unspecified)" followed by the project's confidence levels, in order.
        var vocabulary = confidenceLevels?.ToList() ?? ProjectSettings.DefaultConfidenceLevels.ToList();
        ConfidenceOptions = new[] { UnspecifiedDisplay }.Concat(vocabulary).ToList();

        Rows = new ReadOnlyObservableCollection<ILabelRowViewModel>(visibleRows);

        labels.LabelsChanged += OnProviderLabelsChanged;
        RebuildAllRows();
    }

    // ---------------------------------------------------------------- STATE

    public ReadOnlyObservableCollection<ILabelRowViewModel> Rows { get; }

    public ILabelRowViewModel? SelectedRow
    {
        get => selectedRow;
        set => this.SetField(ref selectedRow, value, compareRefOnly: true);
    }

    public string SearchTerm
    {
        get => searchTerm;
        set
        {
            if (!this.SetField(ref searchTerm, value ?? ""))
                return;
            searchMatcher = new LabelSearchTerms(searchTerm);
            RebuildVisible();
        }
    }

    public LabelField SortField
    {
        get => sortField;
        set
        {
            if (this.SetField(ref sortField, value))
                RebuildVisible();
        }
    }

    public bool SortDescending
    {
        get => sortDescending;
        set
        {
            if (this.SetField(ref sortDescending, value))
                RebuildVisible();
        }
    }

    public string StatusText
    {
        get => statusText;
        private set => this.SetField(ref statusText, value, propertyName: nameof(StatusText));
    }

    public bool IsBusy
    {
        get => isBusy;
        private set => this.SetField(ref isBusy, value, propertyName: nameof(IsBusy));
    }

    public int TotalLabelCount => rowsByAddress.Count;
    public int VisibleLabelCount => visibleRows.Count;

    // the display string shown for the "unspecified" ("") confidence value in the dropdown.
    public const string UnspecifiedDisplay = "(unspecified)";

    // "(unspecified)" + the project's confidence vocabulary, in order; constant for the VM's lifetime.
    public IReadOnlyList<string> ConfidenceOptions { get; }

    // map a dropdown DISPLAY string to the STORED confidence value: "(unspecified)" <-> "".
    public static string ConfidenceDisplayToStored(string display) =>
        display == UnspecifiedDisplay ? "" : display ?? "";

    // map a STORED confidence value to its dropdown DISPLAY string ("" -> "(unspecified)").
    public static string ConfidenceStoredToDisplay(string stored) =>
        string.IsNullOrEmpty(stored) ? UnspecifiedDisplay : stored;

    // ---------------------------------------------------------------- EVENTS OUT

    public event EventHandler<string>? ErrorRaised;
    public event EventHandler<int>? NavigationRequested;

    private void RaiseError(string message) =>
        Marshal(() => ErrorRaised?.Invoke(this, message));

    // ---------------------------------------------------------------- ROW PIPELINE

    private void OnProviderLabelsChanged(object? sender, LabelChangedEventArgs e) =>
        // may arrive from any thread (e.g. import on a pool thread): marshal the whole
        // application of the change, so collection mutations + notifications happen where
        // the host wants them.
        Marshal(() => ApplyLabelChange(e));

    private void ApplyLabelChange(LabelChangedEventArgs e)
    {
        if (disposed)
            return;

        switch (e.Kind)
        {
            case LabelChangeKind.Added:
            case LabelChangeKind.Replaced:
                // both handled by re-reading the provider: idempotent against the known
                // over-report and against our own inline bookkeeping in commands.
                AddOrRefreshRow(e.SnesAddress);
                break;
            case LabelChangeKind.Removed:
                RemoveRowCore(e.SnesAddress);
                break;
            case LabelChangeKind.BulkReset:
            default:
                RebuildAllRows();
                break;
        }

        RaiseCountsChanged();
    }

    private void AddOrRefreshRow(int snesAddress)
    {
        var current = labels.GetLabel(snesAddress);
        if (current == null)
        {
            // defensive: event said added/replaced, provider disagrees. trust the provider.
            RemoveRowCore(snesAddress);
            return;
        }

        if (rowsByAddress.TryGetValue(snesAddress, out var row))
        {
            if (!ReferenceEquals(row.UnderlyingLabel, current))
                row.Rebind(current);
            RefreshVisibleFor(row); // content may have changed: re-filter / re-position
        }
        else
        {
            row = new LabelRowViewModel(snesAddress, current, NotificationMarshaller);
            rowsByAddress[snesAddress] = row;
            if (PassesFilter(row))
                InsertVisibleSorted(row);
        }
    }

    private void RemoveRowCore(int snesAddress)
    {
        if (!rowsByAddress.Remove(snesAddress, out var row))
            return;

        visibleRows.Remove(row);
        if (ReferenceEquals(selectedRow, row))
            SelectedRow = null;
        row.Dispose();
    }

    private void RebuildAllRows()
    {
        var previouslySelectedAddress = (selectedRow as LabelRowViewModel)?.SnesAddress;

        foreach (var row in rowsByAddress.Values)
            row.Dispose();
        rowsByAddress.Clear();

        // snapshot: provider enumerable may be lazy over live collections
        foreach (var (snesAddress, label) in labels.Labels.ToList())
            rowsByAddress[snesAddress] = new LabelRowViewModel(snesAddress, label, NotificationMarshaller);

        RebuildVisible();

        SelectedRow = previouslySelectedAddress is { } addr && rowsByAddress.TryGetValue(addr, out var reselect)
            ? reselect
            : null;
    }

    private void RebuildVisible()
    {
        var wanted = rowsByAddress.Values
            .Where(PassesFilter)
            .ToList();
        wanted.Sort(CompareRows);

        visibleRows.Clear();
        foreach (var row in wanted)
            visibleRows.Add(row);

        RaiseCountsChanged();
    }

    private bool PassesFilter(LabelRowViewModel row) =>
        searchMatcher.DoesLabelMatch(row.SnesAddress, row.UnderlyingLabel);

    private void RefreshVisibleFor(LabelRowViewModel row)
    {
        var index = visibleRows.IndexOf(row);
        if (index >= 0)
            visibleRows.RemoveAt(index);
        if (PassesFilter(row))
            InsertVisibleSorted(row);
    }

    private void InsertVisibleSorted(LabelRowViewModel row)
    {
        // binary search for the insertion point under the current comparer
        int lo = 0, hi = visibleRows.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (CompareRows(visibleRows[mid], row) <= 0)
                lo = mid + 1;
            else
                hi = mid;
        }

        visibleRows.Insert(lo, row);
    }

    /// <summary>
    /// Sort semantics: Address compares numerically (the old grid compared the 6-digit hex
    /// strings, which orders identically for these values). Name/Comment compare
    /// case-insensitively, matching the DataView's default string sort. Ties always break by
    /// ascending address so ordering is deterministic.
    /// </summary>
    private int CompareRows(ILabelRowViewModel a, ILabelRowViewModel b)
    {
        var primary = sortField switch
        {
            LabelField.Name => string.Compare(a.Name, b.Name, StringComparison.InvariantCultureIgnoreCase),
            LabelField.Comment => string.Compare(a.Comment, b.Comment, StringComparison.InvariantCultureIgnoreCase),
            _ => a.SnesAddress.CompareTo(b.SnesAddress),
        };

        if (sortDescending)
            primary = -primary;

        return primary != 0 ? primary : a.SnesAddress.CompareTo(b.SnesAddress);
    }

    private void RaiseCountsChanged()
    {
        OnPropertyChanged(nameof(TotalLabelCount));
        OnPropertyChanged(nameof(VisibleLabelCount));
    }

    /// <summary>Inline, idempotent bookkeeping after this VM itself mutates the provider:
    /// with an inline marshaller the provider event already did the work; with a deferring
    /// marshaller this guarantees commands can still return the row synchronously.</summary>
    private LabelRowViewModel? EnsureRow(int snesAddress)
    {
        AddOrRefreshRow(snesAddress);
        RaiseCountsChanged();
        return rowsByAddress.GetValueOrDefault(snesAddress);
    }

    // ---------------------------------------------------------------- VALIDATION / EDITS

    public ValidationResult ValidateEdit(ILabelRowViewModel row, LabelField field, string proposed)
    {
        switch (field)
        {
            case LabelField.Address:
            {
                if (!TryParseSnesAddress(proposed, out var newAddress))
                    return ValidationResult.Fail("Must enter a valid hex address."); // exact old message

                // see PORT NOTE 1 on this class: revived (previously dead) duplicate guard.
                if (newAddress != row.SnesAddress && labels.GetLabel(newAddress) != null)
                    return ValidationResult.Fail("This address already has a label."); // exact old message

                return ValidationResult.Ok;
            }
            case LabelField.Name:
                // Strict (see PORT NOTE 2): the importer's Legacy rule stays untouched.
                return LabelNameValidator.Validate(proposed);
            case LabelField.Confidence:
                // Confidence is free-form now (any level string, on- or off-vocabulary), so any
                // value is accepted. The dropdown offers the vocabulary; hand-set values are kept.
                return ValidationResult.Ok;
            case LabelField.Author:
            case LabelField.Comment:
            default:
                // Author + Comment: no rules today (Comment matches the old
                // `// todo (validate for valid comment characters, if any)`; Author is freeform).
                return ValidationResult.Ok;
        }
    }

    public ValidationResult CommitEdit(ILabelRowViewModel row, LabelField field, string proposed)
    {
        var result = ValidateEdit(row, field, proposed);
        if (!result.IsValid)
        {
            StatusText = result.Error ?? "";
            return result;
        }

        StatusText = "";

        var oldAddress = row.SnesAddress;
        var newAddress = oldAddress;
        if (field == LabelField.Address)
            TryParseSnesAddress(proposed, out newAddress); // validated above

        // PORT NOTE 4: fresh Label each commit, context mappings carried over by reference --
        // byte-for-byte what CellValidating did.
        var existingLabelAtOldAddress = labels.GetLabel(oldAddress);
        var newLabel = BuildEditedLabel(row, field, proposed, existingLabelAtOldAddress);

        var wasSelected = ReferenceEquals(selectedRow, row);

        labels.RemoveLabel(oldAddress);
        labels.AddLabel(newAddress, newLabel, overwrite: true);

        var newRow = EnsureRow(newAddress);
        if (wasSelected)
            SelectedRow = newRow;

        return result;
    }

    /// <summary>
    /// The single place a commit's fresh Label is built. Every field is carried across (the
    /// edited one from <paramref name="proposed"/>, the rest from the row), so no commit path
    /// can silently drop a field -- adding a future label field means touching only here.
    /// Context mappings are carried by reference, exactly as the old CellValidating did.
    /// </summary>
    private static Label BuildEditedLabel(
        ILabelRowViewModel row, LabelField field, string proposed, IAnnotationLabel? existingLabelAtOldAddress) =>
        new()
        {
            Name = field == LabelField.Name ? proposed : row.Name,
            Comment = field == LabelField.Comment ? proposed : row.Comment,
            Author = field == LabelField.Author ? proposed : row.Author,
            Confidence = field == LabelField.Confidence ? ConfidenceDisplayToStored(proposed) : row.Confidence,
            ContextMappings = existingLabelAtOldAddress?.ContextMappings ?? [],
        };

    private static bool TryParseSnesAddress(string? text, out int snesAddress) =>
        int.TryParse(text ?? "", NumberStyles.HexNumber, null, out snesAddress);

    // ---------------------------------------------------------------- COMMANDS

    public ILabelRowViewModel AddLabel(int snesAddress, string name = "New Label")
    {
        // overwrite:false -- an existing label at this address is kept, and (provider quirk)
        // the event still reports Replaced; EnsureRow/AddOrRefreshRow are idempotent to it.
        labels.AddLabel(snesAddress, new Label { Name = name }, overwrite: false);
        return EnsureRow(snesAddress)!;
    }

    public void DeleteLabel(int snesAddress)
    {
        labels.RemoveLabel(snesAddress);
        RemoveRowCore(snesAddress); // idempotent with the provider event
        RaiseCountsChanged();
    }

    public IContextMappingViewModel AddContextMapping(
        ILabelRowViewModel row, string context = "", string nameOverride = "")
    {
        var target = (LabelRowViewModel)row;

        // In-place append to the model's own collection (byte-for-byte what the WinForms
        // details grid's ListChanged handler did). The row observes CollectionChanged and
        // rebuilds its wrappers in order, so the wrapper for THIS mapping is the last one.
        var mapping = new ContextMapping { Context = context, NameOverride = nameOverride };
        target.UnderlyingLabel.ContextMappings.Add(mapping);

        return target.ContextMappings
                   .OfType<ContextMappingViewModel>()
                   .LastOrDefault(w => ReferenceEquals(w.Model, mapping))
               ?? target.ContextMappings[^1];
    }

    public void RemoveContextMapping(ILabelRowViewModel row, IContextMappingViewModel mapping)
    {
        var target = (LabelRowViewModel)row;
        if (mapping is ContextMappingViewModel wrapper)
            target.UnderlyingLabel.ContextMappings.Remove(wrapper.Model);
    }

    public void ClearSearch() => SearchTerm = "";

    public void NormalizeWramLabels()
    {
        // default routing is Diz.Core's LabelProviderExtensions.NormalizeWramLabels (plan
        // review finding 2, RESOLVED: moved to Core). the operation mutates the provider
        // label-by-label; each mutation raises a provider event and the row pipeline applies
        // it incrementally.
        normalizeWramLabels();
    }

    public ILabelRowViewModel? FocusOrCreateAtSnesAddress(int snesAddress)
    {
        // convert mirrored WRAM addresses (e.g. $00xxxx) to the canonical $7E bank,
        // exactly as the old editor did.
        var address = RomUtil.NormalizeSnesWramAddress(snesAddress);

        // old editor cleared the filter so the target row could not be hidden; same here.
        if (SearchTerm.Length != 0)
            SearchTerm = "";

        // PORT NOTE 3: create in the provider, not as a phantom row.
        var row = labels.GetLabel(address) != null
            ? EnsureRow(address)
            : AddLabel(address);

        SelectedRow = row;
        return row;
    }

    public ILabelRowViewModel? FocusOrCreateAtRomOffsetIa(int romOffset)
    {
        if (resolveRomOffsetToSnesIa == null)
        {
            RaiseError("Intermediate-address resolution is not wired up in this host.");
            return null;
        }

        var snesIa = resolveRomOffsetToSnesIa(romOffset);
        if (snesIa == -1)
        {
            // old message: "You have selected a row in the main grid that has no IA
            // (Intermediate Address). Can't proceed"
            RaiseError("The selected row has no IA (Intermediate Address). Can't proceed.");
            return null;
        }

        return FocusOrCreateAtSnesAddress(snesIa);
    }

    public void JumpToSelectedInMainView()
    {
        var selected = selectedRow;
        if (selected == null)
            return;

        // the host routes this to main-view navigation (the old code converted to a ROM
        // offset itself via Diz.Cpu.65816, which this assembly cannot reference).
        var address = selected.SnesAddress;
        Marshal(() => NavigationRequested?.Invoke(this, address));
    }

    // ---------------------------------------------------------------- IMPORT / EXPORT

    public async Task<ImportResult> ImportLabelsAsync(
        string path, bool replaceAll, IProgress<int>? p = null, CancellationToken ct = default)
    {
        IsBusy = true;
        try
        {
            p?.Report(0);

            var result = await Task.Run(() => ImportLabelsBlocking(path, replaceAll, p, ct), ct);

            p?.Report(100);

            if (!result.Success)
                RaiseError(result.ErrorLineNumber > 0
                    ? $"{result.ErrorMessage} (near line {result.ErrorLineNumber})"
                    : result.ErrorMessage ?? "Label import failed.");

            return result;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private ImportResult ImportLabelsBlocking(
        string path, bool replaceAll, IProgress<int>? p, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // same importer selection as the existing LabelImporterUtils.ImportLabelsFromCsv path;
        // done here instead so we can (a) count what was read, (b) report progress between
        // the parse and apply phases, (c) honor cancellation before mutating the provider.
        LabelImporter? importer = null;
        if (BsnesSymbolLabelImporter.IsFileCompatible(path))
            importer = new BsnesSymbolLabelImporter();
        else if (LabelImporterCsv.IsFileCompatible(path))
            importer = new LabelImporterCsv();

        if (importer == null)
            return new ImportResult(false, 0,
                ErrorMessage: $"No importer available for a file named: '{path}'");

        Dictionary<int, IAnnotationLabel> labelsFromFile;
        try
        {
            labelsFromFile = importer.ReadLabelsFromFile(path);
        }
        catch (Exception ex)
        {
            return new ImportResult(false, 0, importer.LastErrorLineNumber, ex.Message);
        }

        ct.ThrowIfCancellationRequested();
        p?.Report(50);

        // apply. smartMerge:true matches the only current caller (ProjectController).
        if (replaceAll)
            labels.DeleteAllLabels();
        labels.AppendLabels(labelsFromFile, smartMerge: true);

        return new ImportResult(true, labelsFromFile.Count);
    }

    public async Task<LabelExportResult> ExportLabelsAsync(string path, CancellationToken ct = default)
    {
        IsBusy = true;
        try
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                return new LabelExporterCsv().ExportLabelsToFile(path, labels);
            }, ct);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ---------------------------------------------------------------- LIFECYCLE

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        labels.LabelsChanged -= OnProviderLabelsChanged;
        foreach (var row in rowsByAddress.Values)
            row.Dispose();
        rowsByAddress.Clear();
        visibleRows.Clear();
    }
}

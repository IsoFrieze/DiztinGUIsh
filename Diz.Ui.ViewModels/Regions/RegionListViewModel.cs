using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Diz.Core.Interfaces;
using Diz.Core.model.snes;
using Diz.Core.util;

namespace Diz.Ui.ViewModels.Regions;

/// <summary>
/// The region editor's logic, as toolkit-free testable state.
///
/// WHAT IT OWNS
///  - a row per region, in DISPLAY order (sorted), while the region collection keeps its own
///    order -- that stored order is what gets serialized and exported, and sorting must never
///    disturb it;
///  - per-field validation, so an invalid value is refused before it can reach the model;
///  - the whole-list problem report: the relationships BETWEEN regions, which cannot be computed
///    one row at a time, PLUS the rows whose own stored values break a rule, so that nothing can
///    be flagged in the grid and missing from the report.
///
/// WHAT IT DOES NOT OWN
///  - asking the user anything. A delete confirmation is obtained by the host BEFORE it calls
///    DeleteRegion;
///  - marking the project dirty. It reports data changes via RegionsChanged and the host decides
///    what that means.
///
/// VALIDATION IS NON-BLOCKING BUT STRICT: a refused edit never reaches the model and never traps
/// the user in a row -- the row is flagged, the typed text is kept on screen, and the user is
/// free to go elsewhere and come back. The rules are row-scoped, so while a row is invalid an
/// edit to any of its OTHER fields is refused with the same message until the offending field is
/// fixed.
///
/// THREADING. Two different paths, deliberately:
///   - changes that arrive FROM the model (the region collection's own change notification) may
///     come from any thread -- an import synthesizes regions on a worker -- so applying them is
///     routed through the notification marshaller;
///   - commands (add, delete, commit, re-sort) are contracted to be called on the UI thread and
///     mutate the row and problem collections SYNCHRONOUSLY, so a command can return the row it
///     just created and a view can rely on the collection being current when the call returns.
/// That is the contract stated on ViewModelNotifierBase, and the same split the label editor
/// uses. Individual notifications raised along the way still go through the marshaller.
/// </summary>
public sealed class RegionListViewModel : ViewModelNotifierBase, IRegionListViewModel
{
    /// <summary>Name given to a region created from the editor, so a brand-new row is valid
    /// immediately instead of being unleaveable until named.</summary>
    public const string DefaultRegionName = "New Region";

    private readonly IRegionProvider provider;
    private readonly ObservableCollection<IRegion> regions;

    private readonly Dictionary<IRegion, RegionRowViewModel> rowsByRegion = new(RegionIdentity.Comparer);
    private readonly ObservableCollection<IRegionRowViewModel> sortedRows = [];
    private readonly ObservableCollection<RegionProblem> problems = [];

    private RegionField sortField = RegionField.Start;
    private bool sortDescending;
    private IRegionRowViewModel? selectedRow;
    private string statusText = "";
    private long nextSequence;
    private bool disposed;

    /// <param name="regionProvider">the model. Mutations go through its region collection, and
    /// that collection's own change notification drives the row pipeline.</param>
    /// <param name="notificationMarshaller">runs every notification; a real host passes "execute
    /// on the UI thread" (send-if-off-thread semantics -- see <see cref="ViewModelNotifierBase"/>).
    /// null (unit tests) = invoke inline.</param>
    public RegionListViewModel(IRegionProvider regionProvider, Action<Action>? notificationMarshaller = null)
        : base(notificationMarshaller)
    {
        provider = regionProvider;
        regions = regionProvider.Regions;

        Rows = new ReadOnlyObservableCollection<IRegionRowViewModel>(sortedRows);
        Problems = new ReadOnlyObservableCollection<RegionProblem>(problems);

        regions.CollectionChanged += OnRegionsCollectionChanged;
        SyncRowsToModel();
        RevalidateAll();
    }

    // ---------------------------------------------------------------- STATE

    public ReadOnlyObservableCollection<IRegionRowViewModel> Rows { get; }
    public ReadOnlyObservableCollection<RegionProblem> Problems { get; }

    public IRegionRowViewModel? SelectedRow
    {
        get => selectedRow;
        set => this.SetField(ref selectedRow, value, compareRefOnly: true);
    }

    public RegionField SortField
    {
        get => sortField;
        set
        {
            if (this.SetField(ref sortField, value))
                RebuildSortedRows();
        }
    }

    public bool SortDescending
    {
        get => sortDescending;
        set
        {
            if (this.SetField(ref sortDescending, value))
                RebuildSortedRows();
        }
    }

    public string StatusText
    {
        get => statusText;
        private set => this.SetField(ref statusText, value ?? "", propertyName: nameof(StatusText));
    }

    public int RegionCount => regions.Count;

    // ---------------------------------------------------------------- EVENTS OUT

    public event EventHandler? RegionsChanged;

    private void RaiseRegionsChanged() =>
        Marshal(() => RegionsChanged?.Invoke(this, EventArgs.Empty));

    // ---------------------------------------------------------------- ROW PIPELINE

    private void OnRegionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        // may arrive from any thread (an import synthesizes bank regions): marshal the whole
        // application of the change, so collection mutations + notifications happen where the
        // host wants them.
        Marshal(() =>
        {
            if (disposed)
                return;
            SyncRowsToModel();
            RevalidateAll();
        });

    /// <summary>
    /// Reconcile the row set against the region collection: create rows for regions that gained
    /// one, drop rows whose region left. Existing row instances are KEPT, so selection, typed
    /// text and error state survive an unrelated add or remove.
    /// </summary>
    private void SyncRowsToModel()
    {
        foreach (var region in regions)
        {
            if (!rowsByRegion.ContainsKey(region))
                AddRowCore(region);
        }

        var departed = rowsByRegion.Keys.Where(r => !ContainsRegion(r)).ToList();
        foreach (var region in departed)
            RemoveRowCore(region);

        OnPropertyChanged(nameof(RegionCount));
    }

    private bool ContainsRegion(IRegion region) =>
        regions.Any(r => ReferenceEquals(r, region));

    // Row/problem collection mutations happen on the calling thread. From the model-driven path
    // that thread is already the marshaller's; from a command it is the UI thread by contract.
    // See the threading note on this class.
    private RegionRowViewModel AddRowCore(IRegion region)
    {
        var row = new RegionRowViewModel(region, nextSequence++, NotificationMarshaller);
        row.PropertyChanged += OnRowPropertyChanged;
        rowsByRegion[region] = row;
        sortedRows.Insert(SortedInsertIndex(row), row);
        return row;
    }

    private void RemoveRowCore(IRegion region)
    {
        if (!rowsByRegion.Remove(region, out var row))
            return;

        if (ReferenceEquals(selectedRow, row))
            SelectedRow = null;

        sortedRows.Remove(row);
        row.PropertyChanged -= OnRowPropertyChanged;
        row.Dispose();
    }

    /// <summary>Inline, idempotent bookkeeping after this ViewModel itself mutates the region
    /// collection: with an inline marshaller the collection event already did the work; with a
    /// deferring marshaller this guarantees commands can still return the row synchronously.</summary>
    private RegionRowViewModel EnsureRow(IRegion region) =>
        rowsByRegion.TryGetValue(region, out var existing) ? existing : AddRowCore(region);

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not RegionRowViewModel row)
            return;

        // only a change to the field currently being sorted on can move a row.
        if (e.PropertyName == RegionRowViewModel.PropertyNameOf(sortField))
            RepositionRow(row);
    }

    // ---------------------------------------------------------------- SORTING (DISPLAY ONLY)

    // Reached only from the sort setters, i.e. from a command: mutates the row collection on the
    // calling thread, which the contract says is the UI thread. See the threading note on this class.
    private void RebuildSortedRows()
    {
        var ordered = rowsByRegion.Values.ToList();
        ordered.Sort(CompareRows);

        sortedRows.Clear();
        foreach (var row in ordered)
            sortedRows.Add(row);
    }

    private int SortedInsertIndex(RegionRowViewModel row)
    {
        var index = 0;
        while (index < sortedRows.Count && CompareRows((RegionRowViewModel)sortedRows[index], row) <= 0)
            index++;
        return index;
    }

    private void RepositionRow(RegionRowViewModel row)
    {
        var from = sortedRows.IndexOf(row);
        if (from < 0)
            return;

        // where it belongs among the OTHER rows, which is exactly the index to move it to.
        var to = 0;
        for (var i = 0; i < sortedRows.Count; i++)
        {
            if (i == from)
                continue;
            if (CompareRows((RegionRowViewModel)sortedRows[i], row) > 0)
                break;
            to++;
        }

        if (to != from)
            sortedRows.Move(from, to);
    }

    /// <summary>
    /// Display ordering. Numeric fields compare numerically (not as their hex text); text fields
    /// compare case-insensitively under the invariant culture, so the order is the same on every
    /// machine. Ties always break by row creation order, which keeps the ordering deterministic
    /// and stops equal rows shuffling when the direction flips.
    /// </summary>
    private int CompareRows(RegionRowViewModel a, RegionRowViewModel b)
    {
        var x = a.UnderlyingRegion;
        var y = b.UnderlyingRegion;

        var primary = sortField switch
        {
            RegionField.Start => x.StartSnesAddress.CompareTo(y.StartSnesAddress),
            RegionField.End => x.EndSnesAddress.CompareTo(y.EndSnesAddress),
            RegionField.Length => a.RegionLength.CompareTo(b.RegionLength),
            RegionField.Priority => x.Priority.CompareTo(y.Priority),
            RegionField.ExportSeparateFile => x.ExportSeparateFile.CompareTo(y.ExportSeparateFile),
            RegionField.ExportType => ((int)x.ExportType).CompareTo((int)y.ExportType),
            RegionField.RegionName => CompareText(x.RegionName, y.RegionName),
            RegionField.ContextToApply => CompareText(x.ContextToApply, y.ContextToApply),
            RegionField.AssetType => CompareText(x.AssetType, y.AssetType),
            RegionField.AssetVersion => CompareText(x.AssetVersion, y.AssetVersion),
            RegionField.AssetName => CompareText(x.AssetName, y.AssetName),
            RegionField.AssetOptions => CompareText(x.AssetOptions, y.AssetOptions),
            _ => 0,
        };

        if (sortDescending)
            primary = -primary;

        return primary != 0 ? primary : a.Sequence.CompareTo(b.Sequence);
    }

    private static int CompareText(string? a, string? b) =>
        string.Compare(a ?? "", b ?? "", StringComparison.InvariantCultureIgnoreCase);

    // ---------------------------------------------------------------- VALIDATION

    public ValidationResult ValidateField(IRegionRowViewModel row, RegionField field, string proposedText)
    {
        var target = RowOf(row);
        return PrepareEdit(target, field, proposedText, out _, out var result) == EditOutcome.Invalid
            ? result
            : ValidationResult.Ok;
    }

    public ValidationResult CommitField(IRegionRowViewModel row, RegionField field, string proposedText)
    {
        var target = RowOf(row);
        var text = proposedText ?? "";
        var outcome = PrepareEdit(target, field, text, out var write, out var result);

        if (outcome == EditOutcome.Invalid)
        {
            // The model is not touched.
            if (field.DisplaysTypedText())
            {
                // The row keeps its committed value AND the text the user typed, and stays
                // flagged until that text is dealt with -- successfully editing some OTHER field
                // must not un-flag a row that is still displaying a value the model refused.
                target.SetPendingText(field, text, result.Error ?? "");
            }
            else
            {
                // A closed-value field cannot display text the model refused: its widget snaps
                // back to the committed value. Parking the refusal here would leave the row
                // marked forever over a value that is neither on screen nor in the model, and
                // would make a later, identical attempt look like "no change" to a view that
                // compares against what it is showing.
                target.ClearPendingText(field);
            }

            StatusText = result.Error ?? "";
            return result;
        }

        if (outcome == EditOutcome.Ignored)
        {
            // blank in a numeric field is "no input", not zero: keep the text, leave the model,
            // and do not flag the row -- an empty box is not a mistake.
            target.SetPendingText(field, text);
            return ValidationResult.Ok;
        }

        var before = target.LastGoodTextFor(field);
        write!(target.UnderlyingRegion);
        var changed = target.LastGoodTextFor(field) != before;

        target.ClearPendingText(field);
        StatusText = "";
        RevalidateAll();

        if (changed)
            RaiseRegionsChanged();

        return ValidationResult.Ok;
    }

    /// <summary>
    /// Give up on an edit: the field stops displaying what the user typed and shows the stored
    /// value again, and any refusal attached to it goes with it.
    ///
    /// Deliberately NOT a validation pass. The row's error state is recomputed only from what is
    /// left -- other fields' refusals, and whatever the stored values themselves were already
    /// failing -- so abandoning an edit can clear a marker but can never invent one.
    ///
    /// StatusText is left alone: it records the last thing that happened, and one field's revert
    /// says nothing about a message some other field's refusal put there.
    /// </summary>
    public void RevertField(IRegionRowViewModel row, RegionField field) =>
        RowOf(row).ClearPendingText(field);

    public void RevalidateAll()
    {
        foreach (var row in rowsByRegion.Values)
            row.ApplyValidationResult(RegionRowValidation.ValidateRow(RegionRowValues.From(row.UnderlyingRegion)));

        RebuildProblems();
    }

    private enum EditOutcome
    {
        /// <summary>The edit is valid and <c>write</c> will apply it.</summary>
        Ok,

        /// <summary>There is no usable input here; the model must not move.</summary>
        Ignored,

        /// <summary>The edit would leave the region in a state a rule rejects.</summary>
        Invalid,
    }

    /// <summary>
    /// Work out what a proposed edit would do, without doing any of it. Produces the region as
    /// it WOULD look, runs every per-region rule against that, and hands back a writer that
    /// touches only the one property the edit is about.
    /// </summary>
    private EditOutcome PrepareEdit(
        RegionRowViewModel row, RegionField field, string text,
        out Action<IRegion>? write, out ValidationResult result)
    {
        write = null;
        result = ValidationResult.Ok;

        var region = row.UnderlyingRegion;
        var candidate = RegionRowValues.From(region);
        var value = text ?? "";

        switch (field)
        {
            case RegionField.Start:
            {
                if (IsNoInput(value))
                    return EditOutcome.Ignored;
                if (!TryParseAddressText(value, out var start))
                {
                    result = ValidationResult.Fail("Start SNES address must be valid number");
                    return EditOutcome.Invalid;
                }

                candidate = candidate with { StartSnesAddress = start };
                write = r => r.StartSnesAddress = start;
                break;
            }
            case RegionField.End:
            {
                if (IsNoInput(value))
                    return EditOutcome.Ignored;
                if (!TryParseAddressText(value, out var end))
                {
                    result = ValidationResult.Fail("End SNES address must be valid number");
                    return EditOutcome.Invalid;
                }

                candidate = candidate with { EndSnesAddress = end };
                write = r => r.EndSnesAddress = end;
                break;
            }
            case RegionField.Length:
            {
                if (IsNoInput(value))
                    return EditOutcome.Ignored;
                if (!TryParseAddressText(value, out var length))
                {
                    result = ValidationResult.Fail(
                        $"Invalid length: '{value}'. Please enter a valid hexadecimal number.");
                    return EditOutcome.Invalid;
                }

                if (length < 1)
                {
                    result = ValidationResult.Fail(
                        "Length must be at least 1 (zero-length regions are not allowed).");
                    return EditOutcome.Invalid;
                }

                // the end address is inclusive, so a length of 1 means end == start. the start
                // stays put and the end moves; there is no stored length to go stale.
                var newEnd = region.StartSnesAddress + length - 1;
                candidate = candidate with { EndSnesAddress = newEnd };
                write = r => r.EndSnesAddress = newEnd;
                break;
            }
            case RegionField.RegionName:
                candidate = candidate with { RegionName = value };
                write = r => r.RegionName = value;
                break;

            case RegionField.ContextToApply:
                // no rule of its own: any string names a label context.
                write = r => r.ContextToApply = value;
                break;

            case RegionField.Priority:
            {
                if (IsNoInput(value))
                    return EditOutcome.Ignored;
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var priority))
                {
                    result = ValidationResult.Fail("Priority must be a valid number.");
                    return EditOutcome.Invalid;
                }

                // any int is legal: priority only breaks ties between overlapping regions.
                write = r => r.Priority = priority;
                break;
            }
            case RegionField.ExportSeparateFile:
            {
                if (IsNoInput(value))
                    return EditOutcome.Ignored;
                if (!bool.TryParse(value, out var separate))
                {
                    result = ValidationResult.Fail("Export Separate File must be true or false.");
                    return EditOutcome.Invalid;
                }

                candidate = candidate with { ExportSeparateFile = separate };
                write = r => r.ExportSeparateFile = separate;
                break;
            }
            case RegionField.ExportType:
            {
                if (IsNoInput(value))
                    return EditOutcome.Ignored;
                if (!Enum.TryParse<RegionExportType>(value, ignoreCase: true, out var exportType)
                    || !Enum.IsDefined(exportType))
                {
                    var known = string.Join(", ", Enum.GetNames<RegionExportType>());
                    result = ValidationResult.Fail($"Export Type must be one of: {known}.");
                    return EditOutcome.Invalid;
                }

                candidate = candidate with { ExportType = exportType };
                write = r => r.ExportType = exportType;
                break;
            }
            case RegionField.AssetType:
                candidate = candidate with { AssetType = value };
                write = r => r.AssetType = value;
                break;

            case RegionField.AssetVersion:
                // codec version is free text; an unknown one is a hard error at build time, not here.
                write = r => r.AssetVersion = value;
                break;

            case RegionField.AssetName:
                candidate = candidate with { AssetName = value };
                write = r => r.AssetName = value;
                break;

            case RegionField.AssetOptions:
                candidate = candidate with { AssetOptions = value };
                write = r => r.AssetOptions = value;
                break;

            default:
                return EditOutcome.Ignored;
        }

        result = RegionRowValidation.ValidateRow(candidate);
        if (result.IsValid)
            return EditOutcome.Ok;

        write = null;
        return EditOutcome.Invalid;
    }

    // whitespace-only is "the user has not entered anything", NOT the number zero.
    private static bool IsNoInput(string text) => string.IsNullOrWhiteSpace(text);

    /// <summary>
    /// Read an address the way the rest of Diz does: a bare hex number, but also tolerating a
    /// pasted label ("CODE_C012AB"), punctuation ("$C6/BBBB") and separators. The number itself
    /// is then read under the invariant culture, so an address means the same thing on every
    /// machine.
    /// </summary>
    private static bool TryParseAddressText(string text, out int value)
    {
        value = 0;

        var accepted = text;
        if (!ByteUtil.TryParseNum_Stripped(ref accepted, NumberStyles.HexNumber, out _))
            return false;

        return int.TryParse(accepted, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    // ---------------------------------------------------------------- WHOLE-LIST PROBLEMS

    // Same threading rule as the row collection: applied on the calling thread. See the
    // threading note on this class.
    private void RebuildProblems()
    {
        var found = new List<RegionProblem>();

        // relationships between regions: role exclusivity, the laminar file-producing family,
        // and non-overlapping asset regions. Reported all at once, never thrown.
        foreach (var message in RegionValidation.ValidateNonCrossing(regions))
            found.Add(new RegionProblem(RegionProblemSeverity.Error, message));

        found.AddRange(RowProblems());
        found.AddRange(DuplicateNameProblems());

        if (found.Count == problems.Count && !found.Where((p, i) => p != problems[i]).Any())
            return; // unchanged: don't churn anything bound to the collection

        problems.Clear();
        foreach (var problem in found)
            problems.Add(problem);
    }

    /// <summary>
    /// Rows whose STORED values break a per-region rule. Without these a region could sit in the
    /// list flagged red while the report underneath it claimed there was nothing wrong -- and a
    /// project loaded with bad data on disk (an asset type no descriptor owns, an options blob
    /// that is not a JSON object) has exactly that shape until someone opens the offending row.
    ///
    /// ONLY THE STORED VALUES COUNT. A field currently displaying text the model refused is NOT
    /// reported here, deliberately: that text is not in the data, it is one keystroke or one
    /// revert away from being gone, and a report that flickered while the user typed would be
    /// unreadable. The row itself carries the refusal, which is where an in-flight problem
    /// belongs.
    ///
    /// ORDER IS BY ADDRESS, not by row-dictionary order (which is unspecified) and not by display
    /// order (which the user can re-sort at will): the report has to come out the same every time
    /// it is rebuilt, or the no-churn comparison below would rewrite the collection for nothing.
    /// </summary>
    private IEnumerable<RegionProblem> RowProblems() =>
        rowsByRegion.Values
            .Where(row => row.ModelErrorText.Length != 0)
            .OrderBy(row => row.UnderlyingRegion.StartSnesAddress)
            .ThenBy(row => row.UnderlyingRegion.EndSnesAddress)
            .ThenBy(row => row.Sequence)
            .Select(row => new RegionProblem(
                RegionProblemSeverity.Error,
                $"{DescribeRow(row)}: {row.ModelErrorText}",
                row.UnderlyingRegion));

    /// <summary>
    /// How a row is named in the problem report. The rule messages are row-local ("Region Name is
    /// required."), which says nothing at all in a list of hundreds of regions, so every one of
    /// them is prefixed with the region's name and range. A blank name is the subject of one of
    /// the rules, so it needs a stand-in rather than an empty prefix.
    /// </summary>
    private static string DescribeRow(RegionRowViewModel row)
    {
        var region = row.UnderlyingRegion;
        var name = string.IsNullOrWhiteSpace(region.RegionName) ? "(unnamed region)" : region.RegionName;
        return $"{name} (${region.StartSnesAddress:X6}-${region.EndSnesAddress:X6})";
    }

    /// <summary>
    /// Region names are supposed to be unique, and an asset with no asset name falls back to its
    /// region name for its filename, so duplicates collide on disk. A WARNING, never a refusal:
    /// projects that already contain duplicates must keep loading and stay editable.
    /// </summary>
    private IEnumerable<RegionProblem> DuplicateNameProblems() =>
        regions
            .Where(r => !string.IsNullOrWhiteSpace(r.RegionName))
            .GroupBy(r => r.RegionName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => new RegionProblem(
                RegionProblemSeverity.Warning,
                $"Region name '{g.Key}' is used by {g.Count()} regions; region names are supposed to be " +
                "unique, and an asset with no Asset Name is written to a file named after its region."));

    // ---------------------------------------------------------------- COMMANDS

    public IRegionRowViewModel AddRegion()
    {
        var region = provider.CreateNewRegion()
                     ?? throw new InvalidOperationException("The project could not create a region.");

        // A blank region is already a legal one-byte range (start == end == 0, and the end
        // address is inclusive); it only lacks a name, which is the one thing a row rule
        // requires. Seed that and nothing else, so the ViewModel never writes a field it was
        // not asked to.
        region.RegionName = DefaultRegionName;

        regions.Add(region);

        var row = EnsureRow(region);
        StatusText = "";
        OnPropertyChanged(nameof(RegionCount));
        RevalidateAll();
        RaiseRegionsChanged();
        return row;
    }

    public void DeleteRegion(IRegionRowViewModel row)
    {
        // deliberately tolerant where CommitField is strict: deleting is safe by construction,
        // because the region is only ever looked for in THIS list. A row that is stale (its
        // region already left, e.g. removed by an import while the view still held the row) or
        // foreign simply matches nothing, and a no-op beats throwing at a user who clicked
        // delete.
        if (row is not RegionRowViewModel target)
            throw new ArgumentException("Row does not belong to this region list.", nameof(row));

        var region = target.UnderlyingRegion;

        // BY IDENTITY. Rows are in display order, so a row index is not a collection index --
        // removing by index would delete a different region as soon as anything is sorted.
        var index = IndexOfRegion(region);
        if (index < 0)
            return;

        regions.RemoveAt(index);

        RemoveRowCore(region);
        StatusText = "";
        OnPropertyChanged(nameof(RegionCount));
        RevalidateAll();
        RaiseRegionsChanged();
    }

    private int IndexOfRegion(IRegion region)
    {
        for (var i = 0; i < regions.Count; i++)
        {
            if (ReferenceEquals(regions[i], region))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Resolve a row this ViewModel is allowed to edit. A row from a DIFFERENT region list is
    /// the same type and wraps a perfectly valid region, so a type check alone would let an edit
    /// through and quietly mutate another project's data: the row has to be one of ours by
    /// identity.
    /// </summary>
    private RegionRowViewModel RowOf(IRegionRowViewModel row)
    {
        if (row is RegionRowViewModel candidate
            && rowsByRegion.TryGetValue(candidate.UnderlyingRegion, out var known)
            && ReferenceEquals(known, candidate))
            return candidate;

        throw new ArgumentException("Row does not belong to this region list.", nameof(row));
    }

    // ---------------------------------------------------------------- LIFECYCLE

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        regions.CollectionChanged -= OnRegionsCollectionChanged;

        foreach (var row in rowsByRegion.Values)
        {
            row.PropertyChanged -= OnRowPropertyChanged;
            row.Dispose();
        }

        rowsByRegion.Clear();
        sortedRows.Clear();
        problems.Clear();
    }

    /// <summary>
    /// Regions are keyed by object identity, never by value: two regions with identical fields
    /// are still two different regions, and the whole point of row identity is telling them
    /// apart.
    /// </summary>
    private static class RegionIdentity
    {
        public static IEqualityComparer<IRegion> Comparer { get; } = new Impl();

        private sealed class Impl : IEqualityComparer<IRegion>
        {
            public bool Equals(IRegion? a, IRegion? b) => ReferenceEquals(a, b);
            public int GetHashCode(IRegion region) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(region);
        }
    }
}

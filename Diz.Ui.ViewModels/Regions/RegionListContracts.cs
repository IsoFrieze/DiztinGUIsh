using System.Collections.ObjectModel;
using System.ComponentModel;
using Diz.Core.Interfaces;
using Diz.Core.util;

namespace Diz.Ui.ViewModels.Regions;

// =============================================================================================
// The region-editor ViewModel contract.
//
// HARD RULES for this assembly (enforced by tests in Diz.Test/Tests/Labels/ and
// Diz.Test/Tests/Regions/):
//   - references ONLY Diz.Core, Diz.Core.Interfaces, Diz.Import. No Diz.Controllers, no
//     Diz.Cpu.65816, no UI toolkit of any kind.
//   - no member may be named with the UI-verb words banned by the MVVM rules. Confirmations
//     are asked by the host BEFORE it calls in here; errors leave as return values and state.
//   - every notification (PropertyChanged / CollectionChanged / events) is raised through the
//     notification marshaller injected at construction (see ViewModelNotifierBase).
//
// The validation rules themselves are NOT here: per-region rules live in Diz.Core
// (RegionRowValidation) and whole-collection rules in Diz.Core (RegionValidation), so an
// exporter, a batch check, or any backend can run exactly the same rules.
// =============================================================================================

/// <summary>
/// One editable field of a region row. Doubles as the sort key
/// (<see cref="IRegionListViewModel.SortField"/>) and the edit key
/// (<see cref="IRegionListViewModel.CommitField"/>), so a view's column-click handler and its
/// cell-commit handler speak the same vocabulary.
///
/// <see cref="Length"/> is not stored on a region: it is derived from the two addresses, and
/// editing it moves the END address while the start stays put.
/// </summary>
public enum RegionField
{
    Start,
    End,
    Length,
    RegionName,
    ContextToApply,
    Priority,
    ExportSeparateFile,
    ExportType,
    AssetType,
    AssetVersion,
    AssetName,
    AssetOptions,
}

/// <summary>
/// Which fields a view can leave TYPED TEXT sitting in.
///
/// Ten of the twelve are free text: a box can hold anything the user types, including something
/// the model refused, and it stays on screen until they deal with it. The other two carry a
/// CLOSED value space -- a bool and an enum -- so whatever widget shows them (a checkbox, a
/// combo, a pair of radio buttons) can only ever display a legal value. When an edit to one of
/// those is refused the widget snaps back to what the model holds, and there is no typed text
/// left over: nothing to keep, nothing to flag the row about, and nothing for a view to compare
/// a later attempt against except the committed value.
///
/// Both halves of the editing path depend on that distinction, which is why it is stated once
/// here rather than re-derived per backend.
/// </summary>
public static class RegionFieldExtensions
{
    public static bool DisplaysTypedText(this RegionField field) =>
        field is not (RegionField.ExportSeparateFile or RegionField.ExportType);
}

/// <summary>
/// How much a whole-list problem matters. Errors describe combinations the exporter cannot
/// resolve; warnings describe data that still exports but is probably a mistake -- existing
/// projects may already contain them and must keep loading.
/// </summary>
public enum RegionProblemSeverity
{
    Error,
    Warning,
}

/// <summary>
/// One entry of the whole-list problem report.
///
/// <paramref name="Region"/> is reserved for the region a problem is about when exactly one is
/// implicated, so a view can eventually offer "take me there". It is NOT POPULATED YET and is
/// null on every problem produced today: the whole-collection checks return prose, not region
/// handles, and re-deriving which region each message meant would mean matching on message text.
/// A view must therefore not offer navigation from this list until the checks hand back the
/// regions themselves.
/// </summary>
public sealed record RegionProblem(RegionProblemSeverity Severity, string Message, IRegion? Region = null);

/// <summary>
/// One row of the region list, over one live region.
///
/// IDENTITY IS THE REGION INSTANCE (<see cref="UnderlyingRegion"/>), never a row index. Rows
/// are displayed in sort order, which is not the order the regions are stored (and stored order
/// is what gets serialized and exported), so an index means nothing outside the view.
///
/// Every displayable value is exposed as TEXT and is read-only here: writes go through
/// <see cref="IRegionListViewModel.CommitField"/>, which validates first and refuses to write
/// invalid data to the model. A rejected edit leaves the region untouched and parks the text
/// the user typed on the row (see <see cref="HasPendingTextFor"/>), so the view can keep
/// showing it without the model ever having held it.
/// </summary>
public interface IRegionRowViewModel : INotifyPropertyChanged
{
    /// <summary>Identity: the live region this row is a view of. Read it freely; write it only
    /// through the list ViewModel, which is where validation lives.</summary>
    IRegion UnderlyingRegion { get; }

    /// <summary>What the view should display for a field: the text the user typed if a rejected
    /// or ignored edit is outstanding, otherwise the committed value.</summary>
    string TextFor(RegionField field);

    /// <summary>The committed value as text, ignoring anything the user has typed since.</summary>
    string LastGoodTextFor(RegionField field);

    /// <summary>True while this field is displaying typed text the model never accepted.</summary>
    bool HasPendingTextFor(RegionField field);

    // Per-field convenience accessors, for backends that bind to named properties. Each is
    // exactly TextFor(the matching field).
    string StartText { get; }
    string EndText { get; }
    string LengthText { get; }
    string RegionNameText { get; }
    string ContextToApplyText { get; }
    string PriorityText { get; }
    string ExportSeparateFileText { get; }
    string ExportTypeText { get; }
    string AssetTypeText { get; }
    string AssetVersionText { get; }
    string AssetNameText { get; }
    string AssetOptionsText { get; }

    /// <summary>Committed value, typed. Handy for a checkbox column that cannot bind to text.</summary>
    bool ExportSeparateFile { get; }

    /// <summary>Committed value, typed. Handy for a combo column that cannot bind to text.</summary>
    RegionExportType ExportType { get; }

    /// <summary>
    /// False when the asset fields mean nothing for this row, i.e. the region's bytes are
    /// emitted as plain inline assembly. Backends grey the asset fields out rather than hiding
    /// them, which would make it non-obvious that the feature exists. Nothing clears the stored
    /// asset values while disabled: flipping the export type back restores whatever was typed.
    /// </summary>
    bool AssetFieldsEnabled { get; }

    /// <summary>True when this row currently fails a per-region rule. Validation never blocks:
    /// the row is marked, focus is never trapped, and the invalid value was never written.</summary>
    bool HasError { get; }

    /// <summary>The message behind <see cref="HasError"/>; "" when there is none.</summary>
    string ErrorText { get; }
}

/// <summary>
/// The region list, as state + commands. No UI verbs: the view renders state and calls
/// commands; confirmations are obtained by the host BEFORE calling in here.
///
/// Both backends drive this one object -- a flat grid of every column, or a master list plus a
/// detail pane -- so the rules, the sort order and the commands cannot drift apart.
/// </summary>
public interface IRegionListViewModel : INotifyPropertyChanged, IDisposable
{
    // ---------------- STATE ----------------

    /// <summary>Already sorted per <see cref="SortField"/>/<see cref="SortDescending"/>.
    /// Sorting is a DISPLAY concern only: the underlying region collection keeps its own order,
    /// which is the order that gets serialized and exported.</summary>
    ReadOnlyObservableCollection<IRegionRowViewModel> Rows { get; }

    /// <summary>The row a detail pane edits and the row a delete command acts on. Selection is
    /// ViewModel state, not view state, so both backends mean the same thing by it.</summary>
    IRegionRowViewModel? SelectedRow { get; set; }

    /// <summary>Setting either re-sorts <see cref="Rows"/>; a view's column-click handler
    /// writes these and nothing else. Default: Start ascending.</summary>
    RegionField SortField { get; set; }

    bool SortDescending { get; set; }

    /// <summary>Last validation/status message. Persistent: it stays until the next action
    /// replaces it, and is cleared to "" by a successful commit.</summary>
    string StatusText { get; }

    /// <summary>Problems that only exist BETWEEN regions -- crossing file-producing regions,
    /// overlapping asset regions, a region claiming two output roles, duplicate names. Recomputed
    /// by every mutation and by <see cref="RevalidateAll"/>.</summary>
    ReadOnlyObservableCollection<RegionProblem> Problems { get; }

    int RegionCount { get; }

    // ---------------- COMMANDS ----------------

    /// <summary>Validate a proposed edit without applying it. Never mutates anything.</summary>
    ValidationResult ValidateField(IRegionRowViewModel row, RegionField field, string proposedText);

    /// <summary>
    /// Validate, and if valid, write just that one field to the region. On failure NOTHING is
    /// written: the row keeps the typed text, gets <see cref="IRegionRowViewModel.HasError"/>,
    /// and the message lands in <see cref="StatusText"/>.
    ///
    /// Blank text in a numeric field means "no input": it is ignored rather than treated as
    /// zero, the model does not move, and the typed text is kept.
    ///
    /// Non-text fields are addressed as text too (bool as "True"/"False", the export type by
    /// its name) so that one method covers every column of a grid.
    /// </summary>
    ValidationResult CommitField(IRegionRowViewModel row, RegionField field, string proposedText);

    /// <summary>
    /// Abandon whatever a field is displaying that the region does not hold -- the text the user
    /// typed and, if it was refused, the refusal -- so the field shows the stored value again.
    /// This is the way out of a value the model will not take.
    ///
    /// Nothing is written and NOTHING IS RE-CHECKED. That matters for rows whose stored values
    /// already break a rule (existing projects carry them: an asset type no descriptor owns, an
    /// options blob that is not a JSON object, a separate-file region straddling a bank): giving
    /// up on an edit must leave such a row exactly the errors it already had, and must not
    /// re-attribute them to the field being abandoned.
    /// </summary>
    void RevertField(IRegionRowViewModel row, RegionField field);

    /// <summary>Append a region with sane defaults -- a name, and a legal one-byte range -- and
    /// return its row. A half-filled new row is never a trap: validation does not block.</summary>
    IRegionRowViewModel AddRegion();

    /// <summary>Remove the region this row is a view of, BY OBJECT IDENTITY. Never by index:
    /// rows are sorted for display, so a row index is not a collection index.</summary>
    void DeleteRegion(IRegionRowViewModel row);

    /// <summary>Re-run every per-region rule and rebuild <see cref="Problems"/>.</summary>
    void RevalidateAll();

    // ---------------- OUTBOUND EVENTS ----------------

    /// <summary>Raised whenever region DATA changed: an add, a delete, or a committed field
    /// edit. Hosts use it to mark the project as having unsaved changes. Deliberately NOT
    /// raised by re-sorting, by selection, or by a rejected edit -- none of those change data.</summary>
    event EventHandler? RegionsChanged;
}

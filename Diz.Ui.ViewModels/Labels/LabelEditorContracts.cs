using System.Collections.ObjectModel;
using System.ComponentModel;
using Diz.Core.util;
using Diz.Import;

namespace Diz.Ui.ViewModels.Labels;

// =============================================================================================
// Step 2 of the new_ui plan: the label-editor ViewModel contract.
// See docs/diz/new-ui-plan.md ("Target label-editor contract (sketch)") for provenance.
//
// HARD RULES for this assembly (enforced by tests in Diz.Test/Tests/Labels/):
//   - references ONLY Diz.Core, Diz.Core.Interfaces, Diz.Import. No Diz.Controllers, no
//     Diz.Cpu.65816, no UI toolkit of any kind.
//   - no member may be named with the UI-verb words banned by the plan's enforceable rule.
//     File paths arrive as plain parameters; errors leave via ErrorRaised; navigation leaves
//     via NavigationRequested. The host/view layer owns all interaction with the user.
//   - every notification (PropertyChanged / CollectionChanged / events) is raised through the
//     notification marshaller injected at construction (see LabelEditorViewModel ctor).
// =============================================================================================

/// <summary>
/// Which editable field of a label row an edit applies to. Also used as the sort key
/// (<see cref="ILabelEditorViewModel.SortField"/>).
/// NOTE: the old WinForms grid also allowed sorting on its read-only "Context" column; that is
/// deliberately not carried over -- the sketch's contract has exactly these three fields.
/// </summary>
public enum LabelField
{
    Address,
    Name,
    Comment,
}

// NOTE -- DELIBERATE DEVIATION from the plan sketch: the sketch declared its own
//   public readonly record struct ValidationResult(bool IsValid, string? Error);
// but an IDENTICAL struct already exists in Diz.Core (Diz.Core/util/LabelNameValidator.cs),
// and ValidateEdit delegates name validation to LabelNameValidator, which returns that type.
// Declaring a second identical struct here would force every caller to convert between the
// two. We reuse Diz.Core.util.ValidationResult instead. (Documented in new-ui-plan.md.)

/// <summary>
/// One label-to-context mapping, reshaped for display. Mirrors what the model's
/// ContextMapping actually exposes (Context + NameOverride, INPC) -- nothing invented.
/// Property writes pass straight through to the underlying model object.
/// </summary>
public interface IContextMappingViewModel : INotifyPropertyChanged
{
    string Context { get; set; }
    string NameOverride { get; set; }
}

/// <summary>
/// One row of the label editor. Replaces the old WinForms DataTable row: identity is the real
/// SNES address as an int, never a hex string parsed back out of a display cell.
/// </summary>
public interface ILabelRowViewModel : INotifyPropertyChanged
{
    /// <summary>Identity. Never changes for the lifetime of a row instance; an address edit
    /// is a remove+add on the provider and produces a NEW row (see CommitEdit).</summary>
    int SnesAddress { get; }

    /// <summary>Display only: the address as 6-digit uppercase hex (e.g. "7E0100").</summary>
    string AddressText { get; }

    string Name { get; set; }
    string Comment { get; set; }

    /// <summary>Read-only one-line summary of the context mappings, formatted exactly as the
    /// old WinForms "Context" column did: "ctx1: override1, ctx2: override2" (mappings with a
    /// whitespace-only Context are skipped).</summary>
    string ContextSummary { get; }

    ObservableCollection<IContextMappingViewModel> ContextMappings { get; }
}

/// <summary>
/// Outcome of <see cref="ILabelEditorViewModel.ImportLabelsAsync"/>.
/// The underlying LabelImporter returns a plain dictionary plus a LastErrorLineNumber int;
/// this wraps that shape into a value the caller can branch on without exception plumbing.
/// </summary>
/// <param name="Success">true if the file parsed and was applied.</param>
/// <param name="LabelsReadFromFile">how many labels the file contained (0 on failure). This is
/// NOT "how many changed": replaceAll=false merges into existing labels.</param>
/// <param name="ErrorLineNumber">1-based line near the failure, when known; -1 otherwise.
/// (Same convention as LabelImporter.LastErrorLineNumber.)</param>
/// <param name="ErrorMessage">failure description, null on success. Also raised via ErrorRaised.</param>
public sealed record ImportResult(
    bool Success,
    int LabelsReadFromFile,
    int ErrorLineNumber = -1,
    string? ErrorMessage = null);

/// <summary>
/// Outbound port for WRAM-label normalization. RESOLVED (plan review finding 2): the
/// operation moved into Diz.Core (LabelProviderExtensions.NormalizeWramLabels), so the VM
/// defaults to calling it directly on its label provider -- no wiring needed. The port stays
/// injectable so tests (or an unusual host) can substitute their own routing.
/// </summary>
public delegate void NormalizeWramLabelsPort();

/// <summary>
/// PROVISIONAL outbound port, same reasoning as <see cref="NormalizeWramLabelsPort"/>:
/// resolving a ROM offset to its intermediate SNES address requires
/// SnesData.GetIntermediateAddress(offset, resolve: true) in Diz.Cpu.65816. The composition
/// layer wires this. Contract: return the resolved SNES address, or -1 if there is none.
/// </summary>
public delegate int ResolveRomOffsetToSnesIaPort(int romOffset);

/// <summary>
/// The label editor, as state + commands. No UI verbs: the view renders state and calls
/// commands; paths and confirmations are obtained by the host BEFORE calling in here.
/// </summary>
public interface ILabelEditorViewModel : INotifyPropertyChanged, IDisposable
{
    // ---------------- STATE ----------------

    /// <summary>Already filtered (by SearchTerm) and sorted (by SortField/SortDescending).</summary>
    ReadOnlyObservableCollection<ILabelRowViewModel> Rows { get; }

    ILabelRowViewModel? SelectedRow { get; set; }

    /// <summary>Setting it re-filters Rows. Semantics are those of Diz.Core's LabelSearchTerms
    /// (space-separated all-must-match terms over "ADDRHEX Name Comment contextOverrides",
    /// case-insensitive, plus the special terms "is:ram" and address comparisons like
    /// "&gt;7E0000" / "&lt;=$7FFFFF").</summary>
    string SearchTerm { get; set; }

    /// <summary>Setting either re-sorts Rows; the view's column-click handler writes these,
    /// nothing else. Default: Address ascending (matches the old grid's "Address ASC").</summary>
    LabelField SortField { get; set; }

    bool SortDescending { get; set; }

    /// <summary>Last validation/status message (mirrors the old status-strip text). Cleared
    /// to "" on a successful commit.</summary>
    string StatusText { get; }

    bool IsBusy { get; }

    int TotalLabelCount { get; }
    int VisibleLabelCount { get; }

    // ---------------- COMMANDS ----------------

    /// <summary>Validate a proposed edit without applying it. Never mutates anything.</summary>
    ValidationResult ValidateEdit(ILabelRowViewModel row, LabelField field, string proposed);

    /// <summary>Validate, and if valid, apply the edit to the label provider. An Address edit
    /// changes row identity: the label is removed at the old address and re-added at the new
    /// one, so the ILabelRowViewModel instance for the old address becomes stale and a new row
    /// appears (SelectedRow is moved to it if the edited row was selected).</summary>
    ValidationResult CommitEdit(ILabelRowViewModel row, LabelField field, string proposed);

    /// <summary>Add a label at the given address. If one already exists there, the existing
    /// label is kept (provider overwrite:false semantics) and its row is returned.</summary>
    ILabelRowViewModel AddLabel(int snesAddress, string name = "New Label");

    void DeleteLabel(int snesAddress);

    /// <summary>Add a new alternate-context mapping to the given row's label and return its VM
    /// wrapper. The mapping is appended to the model's ContextMappings in place (the same
    /// in-place mutation the WinForms details grid did); the row relays the collection change,
    /// so ContextSummary and the row's ContextMappings wrappers refresh. A whitespace-only
    /// context is allowed here (mirrors the old editor's editable new-row) but is skipped by
    /// ContextSummary until named. The label object carrying these mappings is the SAME
    /// instance a later name/comment CommitEdit carries over by reference, so context edits
    /// survive those commits.</summary>
    IContextMappingViewModel AddContextMapping(ILabelRowViewModel row, string context = "", string nameOverride = "");

    /// <summary>Remove an alternate-context mapping from the given row's label (in place).
    /// No-op if the mapping does not belong to this row.</summary>
    void RemoveContextMapping(ILabelRowViewModel row, IContextMappingViewModel mapping);

    void ClearSearch();

    /// <summary>Normalize mirrored WRAM label addresses to the canonical $7E bank. Defaults
    /// to Diz.Core's LabelProviderExtensions.NormalizeWramLabels on this VM's provider;
    /// override via the injected <see cref="NormalizeWramLabelsPort"/>.</summary>
    void NormalizeWramLabels();

    /// <summary>Select the row at this SNES address, creating a "New Label" there first if
    /// none exists. Mirrors the old editor: the address is WRAM-normalized
    /// (RomUtil.NormalizeSnesWramAddress) and the search filter is cleared first so the row
    /// cannot be hidden. Returns the row.</summary>
    ILabelRowViewModel? FocusOrCreateAtSnesAddress(int snesAddress);

    /// <summary>Like <see cref="FocusOrCreateAtSnesAddress"/>, but starting from a ROM offset
    /// whose intermediate address is resolved via the injected
    /// <see cref="ResolveRomOffsetToSnesIaPort"/>. Raises ErrorRaised and returns null if the
    /// offset has no intermediate address (or no port was supplied).</summary>
    ILabelRowViewModel? FocusOrCreateAtRomOffsetIa(int romOffset);

    /// <summary>Raises <see cref="NavigationRequested"/> with the selected row's SNES address.
    /// The VM must NOT reference ISnesNavigation (it lives in Diz.Controllers, which this
    /// assembly is forbidden to reference); the host wires the event to it.</summary>
    void JumpToSelectedInMainView();

    // ---------------- ASYNC; paths supplied by the caller ----------------

    /// <summary>Import labels from a file (.csv, or a bsnes .sym -- same auto-detection as the
    /// existing import path). replaceAll=true deletes all labels first; false merges
    /// (smartMerge, matching the current controller behavior). Progress is coarse: 0 at start,
    /// 100 at completion. Cancellation is honored between phases, not mid-parse.
    /// Failures return ImportResult.Success=false AND raise ErrorRaised; cancellation throws
    /// OperationCanceledException.</summary>
    Task<ImportResult> ImportLabelsAsync(string path, bool replaceAll,
        IProgress<int>? p = null, CancellationToken ct = default);

    /// <summary>Export all labels to a .csv at the given path.
    /// DELIBERATE DEVIATION from the plan sketch (which returned plain Task): returns the
    /// existing LabelExportResult so its Sanitizations list is not silently dropped --
    /// Step 1's contract is that every lossy alteration is reported. (Documented in
    /// new-ui-plan.md.)</summary>
    Task<LabelExportResult> ExportLabelsAsync(string path, CancellationToken ct = default);

    // ---------------- OUTBOUND EVENTS ----------------

    /// <summary>An error the user should see. The host decides how to present it.</summary>
    event EventHandler<string>? ErrorRaised;

    /// <summary>Carries a SNES address; the host routes it to main-view navigation.</summary>
    event EventHandler<int>? NavigationRequested;
}

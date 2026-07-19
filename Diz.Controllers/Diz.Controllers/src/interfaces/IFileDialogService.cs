#nullable enable

using System.Threading.Tasks;

namespace Diz.Controllers.interfaces;

/// <summary>
/// File/folder picker dialogs, implemented and registered once per UI toolkit
/// (WinForms today; Avalonia and TUI backends supply their own).
///
/// Step 4 of the new-ui plan (docs/diz/new-ui-plan.md). Design rules:
/// <list type="bullet">
/// <item>Consumed by the view/host layer ONLY. NEVER injected into a ViewModel --
/// ViewModels receive plain values (paths, bools) obtained by the host layer, and the
/// Diz.Ui.ViewModels assembly is structurally unable to reference this interface (its
/// reference allowlist excludes Diz.Controllers, enforced by test).</item>
/// <item>Async on purpose: Avalonia's file API (IStorageProvider) is async-only, so a sync
/// signature would force blocking the UI thread in exactly the backend this seam exists
/// for. Toolkits with synchronous dialogs (WinForms, TUI) block inside the call and return
/// an already-completed task via Task.FromResult -- cheap in the easy backends, correct in
/// the hard one.</item>
/// <item>All methods return the selected path, or null if the user cancelled.</item>
/// <item>An empty <paramref name="title"/> means "keep the toolkit's default dialog title"
/// (e.g. WinForms' "Open"/"Save As") -- used where a pre-existing dialog had no custom
/// title, so migrating to this service changes nothing the user sees.</item>
/// </list>
/// </summary>
public interface IFileDialogService
{
    /// <param name="title">Dialog title; "" keeps the toolkit default.</param>
    /// <param name="filter">File-type filter, WinForms syntax ("Name|*.ext|Name2|*.ext2").
    /// Non-WinForms implementations translate this to their native filter shape.</param>
    Task<string?> PromptOpenFileAsync(string title, string filter);

    /// <param name="title">Dialog title; "" keeps the toolkit default.</param>
    /// <param name="filter">File-type filter, WinForms syntax (see <see cref="PromptOpenFileAsync"/>).</param>
    /// <param name="suggestedName">Optional pre-filled filename.</param>
    Task<string?> PromptSaveFileAsync(string title, string filter, string? suggestedName = null);

    /// <param name="title">Dialog title/description; "" keeps the toolkit default.</param>
    /// <param name="initialPath">Optional folder the dialog starts in.</param>
    Task<string?> PromptSelectFolderAsync(string title, string? initialPath = null);
}

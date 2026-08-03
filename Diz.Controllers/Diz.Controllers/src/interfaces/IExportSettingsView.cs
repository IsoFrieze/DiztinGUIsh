#nullable enable

using System.Threading.Tasks;
using Diz.Ui.ViewModels.ExportSettings;

namespace Diz.Controllers.interfaces;

/// <summary>
/// The "Export Disassembly" settings window: implemented and registered once per UI toolkit, so the
/// caller can let the user shape an export without knowing which toolkit draws it.
///
/// Contract:
/// <list type="bullet">
/// <item>The caller builds the <see cref="ExportSettingsViewModel"/> -- including the two delegates
/// that validate a line template and render the sample, which come from the assembly-writing layer
/// -- the view edits it, and the caller reads the result back with
/// <see cref="ExportSettingsViewModel.BuildSettings"/> afterwards. The view never exports anything
/// and never writes the settings back into the project.</item>
/// <item>The returned task completes when the user is done: <c>true</c> if they asked to start the
/// export, <c>false</c> if they cancelled or closed the window.</item>
/// <item>Async on purpose. Toolkits with a blocking modal call (WinForms) do the whole thing inside
/// the method and hand back an already-completed task, so awaiting it continues synchronously and
/// behaves exactly like the old blocking call. Toolkits whose window cannot be owned by, or made
/// modal against, the caller's window have no blocking call to make -- they show a free-standing
/// window and complete the task when it closes.</item>
/// <item>One instance per invocation: resolve, edit, discard. The caller may resolve a second one
/// for the SAME ViewModel -- it does that to re-open the window when what the user chose still is
/// not exportable -- so a view must not assume it is the only one that ever edited it, and must not
/// carry state that outlives one edit.</item>
/// </list>
///
/// TWO QUESTIONS BELONG TO THE VIEW, not to the ViewModel. Whether to create an output directory
/// that does not exist yet is asked by whoever hosts this window, using its own toolkit's message
/// box, and answered by calling <see cref="ExportSettingsViewModel.CreateOutputDirectory"/>; and
/// choosing a path with a file/folder picker is likewise the host's, through
/// <see cref="IFileDialogService"/>. The ViewModel holds the policy and the state for both, and
/// asks nothing.
///
/// The output-directory check reads the disk, so the host must call
/// <see cref="ExportSettingsViewModel.RefreshOutputPathStatus"/> when the path settles -- on commit
/// or focus loss -- and never on every keystroke.
/// </summary>
public interface IExportSettingsView
{
    /// <param name="viewModel">The settings to edit, already seeded by the caller.</param>
    /// <returns>true if the user asked to start the export; false if they cancelled or closed.</returns>
    Task<bool> EditAsync(ExportSettingsViewModel viewModel);
}

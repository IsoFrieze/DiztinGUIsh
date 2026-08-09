#nullable enable

using System.Threading.Tasks;
using Diz.Ui.ViewModels.ImportRom;

namespace Diz.Controllers.interfaces;

/// <summary>
/// The "new project from a SNES ROM" window: implemented and registered once per UI toolkit, so
/// the importer can ask the user how to read a ROM without knowing which toolkit draws it.
///
/// Contract:
/// <list type="bullet">
/// <item>The caller builds the <see cref="SnesImportRomViewModel"/> (seeding it from its own
/// analysis of the ROM, including the delegate that re-reads the ROM when the map mode
/// changes), the view edits it, and the caller reads the choices back off the same ViewModel
/// afterwards. The view never reads the ROM and never creates a project.</item>
/// <item>The returned task completes when the user is done: <c>true</c> if they confirmed,
/// <c>false</c> if they cancelled or closed the window.</item>
/// <item>Async on purpose. Toolkits with a blocking modal call (WinForms) do the whole thing
/// inside the method and hand back an already-completed task, so awaiting it continues
/// synchronously and behaves exactly like the old blocking call. Toolkits whose window
/// cannot be owned by, or made modal against, the caller's window have no blocking call to
/// make -- they show a free-standing window and complete the task when it closes.</item>
/// <item>One instance per invocation: resolve, edit, discard. The caller may resolve a second
/// one for the same ViewModel -- it does that to re-open the window when the user declines a
/// confirmation prompt -- so a view must not assume it is the only one that ever edited it.</item>
/// </list>
///
/// The window asks nothing. When the choices on screen warrant a warning the ViewModel says so
/// through <see cref="SnesImportRomViewModel.RequiresConfirmation"/>, and the CALLER puts that
/// question, after this task completes.
/// </summary>
public interface ISnesImportRomView
{
    /// <param name="viewModel">The ViewModel to edit, already seeded by the caller.</param>
    /// <returns>true if the user confirmed; false if they cancelled or closed the window.</returns>
    Task<bool> EditAsync(SnesImportRomViewModel viewModel);
}

#nullable enable

using System.Threading.Tasks;
using Diz.Ui.ViewModels.MisalignmentChecker;

namespace Diz.Controllers.interfaces;

/// <summary>
/// The "check for misaligned flags" window: implemented and registered once per UI toolkit, so
/// the caller offers the user a misalignment report, and the chance to fix what it lists,
/// without knowing which toolkit draws it.
///
/// Contract:
/// <list type="bullet">
/// <item>The caller builds the <see cref="MisalignmentCheckerViewModel"/> (seeding it with a
/// delegate that runs the actual sweep), the view drives it, and the caller applies the fix
/// itself afterwards if the user asked for one. The view never scans and never fixes: it calls
/// <see cref="MisalignmentCheckerViewModel.Scan"/> and renders what comes back.</item>
/// <item>The returned task completes when the user is done: <c>true</c> if they confirmed the
/// fix, <c>false</c> if they cancelled or closed the window.</item>
/// <item>Confirming does NOT require having scanned first. Scanning is a preview; fixing is
/// allowed on its own, which is what the legacy window did deliberately.</item>
/// <item>Async on purpose. Toolkits with a blocking modal call (WinForms) do the whole thing
/// inside the method and hand back an already-completed task, so awaiting it continues
/// synchronously and behaves exactly like the old blocking call. Toolkits whose window
/// cannot be owned by, or made modal against, the caller's window have no blocking call to
/// make -- they show a free-standing window and complete the task when it closes.</item>
/// <item>One instance per invocation: resolve, run, discard.</item>
/// </list>
///
/// Named Run rather than Edit: nothing on this ViewModel is user-editable. The user's whole
/// input is "scan", "fix", or "go away".
/// </summary>
public interface IMisalignmentCheckerView
{
    /// <param name="viewModel">The ViewModel to drive, already seeded by the caller.</param>
    /// <returns>true if the user confirmed the fix; false if they cancelled or closed the window.</returns>
    Task<bool> RunAsync(MisalignmentCheckerViewModel viewModel);
}

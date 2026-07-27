#nullable enable

using System.Threading.Tasks;

namespace Diz.Controllers.interfaces;

/// <summary>
/// The "rescan for in/out points" window: implemented and registered once per UI toolkit, so the
/// caller asks the user to confirm a full rescan without knowing which toolkit draws it.
///
/// Contract:
/// <list type="bullet">
/// <item>NO ViewModel, on purpose. This window has no state, no inputs, and no backend calls of
/// its own -- it explains what a rescan does and takes a yes or a no. An empty ViewModel would
/// be ceremony with nothing in it.</item>
/// <item>The returned task completes when the user is done: <c>true</c> if they confirmed the
/// rescan, <c>false</c> if they cancelled or closed the window. The rescan itself
/// (ProjectController.RescanForInOut) is the caller's job, as it always was.</item>
/// <item>Async on purpose. Toolkits with a blocking modal call (WinForms) do the whole thing
/// inside the method and hand back an already-completed task, so awaiting it continues
/// synchronously and behaves exactly like the old blocking call. Toolkits whose window
/// cannot be owned by, or made modal against, the caller's window have no blocking call to
/// make -- they show a free-standing window and complete the task when it closes.</item>
/// <item>One instance per invocation: resolve, ask, discard.</item>
/// </list>
/// </summary>
public interface IInOutPointCheckerView
{
    /// <returns>true if the user confirmed the rescan; false if they cancelled or closed the window.</returns>
    Task<bool> ConfirmAsync();
}

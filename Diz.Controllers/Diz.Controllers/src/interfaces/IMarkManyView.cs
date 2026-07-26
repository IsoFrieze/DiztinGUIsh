#nullable enable

using System.Threading.Tasks;
using Diz.Cpu._65816;
using Diz.Ui.ViewModels.MarkMany;

namespace Diz.Controllers.interfaces;

/// <summary>
/// The "mark many" window: implemented and registered once per UI toolkit, so the caller
/// picks a run of bytes and a property to mark without knowing which toolkit draws it.
///
/// Contract:
/// <list type="bullet">
/// <item>The caller builds the <see cref="MarkManyViewModel{TDataSource}"/> (seeding it from
/// its own selection), the view edits it, and the caller reads the result off the same
/// ViewModel afterwards. The view never applies anything and never returns a command.</item>
/// <item>The returned task completes when the user is done: <c>true</c> if they confirmed,
/// <c>false</c> if they cancelled or closed the window.</item>
/// <item>Async on purpose. Toolkits with a blocking modal call (WinForms) do the whole thing
/// inside the method and hand back an already-completed task, so awaiting it continues
/// synchronously and behaves exactly like the old blocking call. Toolkits whose window
/// cannot be owned by, or made modal against, the caller's window have no blocking call to
/// make -- they show a free-standing window and complete the task when it closes.</item>
/// <item>One instance per invocation: resolve, edit, discard.</item>
/// </list>
/// </summary>
public interface IMarkManyView
{
    /// <param name="viewModel">The ViewModel to edit, already seeded by the caller.</param>
    /// <returns>true if the user confirmed; false if they cancelled or closed the window.</returns>
    Task<bool> EditAsync(MarkManyViewModel<ISnesData> viewModel);
}

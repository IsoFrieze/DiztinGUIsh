#nullable enable

using System.Threading.Tasks;
using Diz.Ui.ViewModels.HarshAutoStep;

namespace Diz.Controllers.interfaces;

/// <summary>
/// The "harsh auto step" window: implemented and registered once per UI toolkit, so the caller
/// asks the user which run of bytes to decode as instructions without knowing which toolkit
/// draws it.
///
/// Contract:
/// <list type="bullet">
/// <item>The caller builds the <see cref="HarshAutoStepViewModel"/> (seeding it from its own
/// current position), the view edits it, and the caller reads
/// <see cref="HarshAutoStepViewModel.BuildAutoStepHarshCommand"/> off the same ViewModel
/// afterwards. The view never steps anything and never navigates.</item>
/// <item>The returned task completes when the user is done: <c>true</c> if they confirmed,
/// <c>false</c> if they cancelled or closed the window.</item>
/// <item>Async on purpose. Toolkits with a blocking modal call (WinForms) do the whole thing
/// inside the method and hand back an already-completed task, so awaiting it continues
/// synchronously and behaves exactly like the old blocking call. Toolkits whose window
/// cannot be owned by, or made modal against, the caller's window have no blocking call to
/// make -- they show a free-standing window and complete the task when it closes.</item>
/// <item>One instance per invocation: resolve, edit, discard.</item>
/// </list>
///
/// One parameter only: unlike the goto window there is no initial-focus choice to carry, because
/// this window has three fields and no reason to prefer any one of them.
/// </summary>
public interface IHarshAutoStepView
{
    /// <param name="viewModel">The ViewModel to edit, already seeded by the caller.</param>
    /// <returns>true if the user confirmed; false if they cancelled or closed the window.</returns>
    Task<bool> EditAsync(HarshAutoStepViewModel viewModel);
}

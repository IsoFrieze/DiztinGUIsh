#nullable enable

using System.Threading.Tasks;
using Diz.Ui.ViewModels.Goto;

namespace Diz.Controllers.interfaces;

/// <summary>
/// The "go to a location in the ROM" window: implemented and registered once per UI toolkit,
/// so the caller asks the user where to go without knowing which toolkit draws it.
///
/// Contract:
/// <list type="bullet">
/// <item>The caller builds the <see cref="GotoViewModel"/> (seeding it from its own current
/// position), the view edits it, and the caller reads <see cref="GotoViewModel.ResultPcOffset"/>
/// off the same ViewModel afterwards. The view never navigates anywhere itself.</item>
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
public interface IGotoView
{
    /// <param name="viewModel">The ViewModel to edit, already seeded by the caller.</param>
    /// <param name="initiallySelectSnesAddr">
    /// Which box's text starts out selected, so typing replaces it rather than appending to it.
    /// This is a view concern only -- nothing about it belongs in the ViewModel, which does not
    /// know that boxes or carets exist.
    ///
    /// True selects the SNES ADDRESS box; false selects the ROM FILE OFFSET box -- the name
    /// describes the behavior. The legacy dialog had this inverted (true selected the ROM file
    /// offset box); un-inverted as a deliberate behavior fix (Dom, 2026-07-26), so that with the
    /// caller's <c>!DisplayOffsetsInGrid</c> argument, the selected box is the one showing the
    /// same address form the grid is displaying.
    /// </param>
    /// <returns>true if the user confirmed; false if they cancelled or closed the window.</returns>
    Task<bool> EditAsync(GotoViewModel viewModel, bool initiallySelectSnesAddr);
}

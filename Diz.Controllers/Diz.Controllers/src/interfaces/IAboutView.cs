#nullable enable

namespace Diz.Controllers.interfaces;

/// <summary>
/// The "About Diz" window: implemented and registered once per UI toolkit, so the caller can show
/// it without knowing which toolkit draws it.
///
/// Contract:
/// <list type="bullet">
/// <item>NO ViewModel, on purpose. The window has no mutable state and no input beyond dismissing
/// it -- it displays the running build's version and description, which come straight from
/// <see cref="Diz.Core.Interfaces.IAppVersionInfo"/>. An empty ViewModel would be ceremony with
/// nothing in it.</item>
/// <item>ONE window, re-shown. <see cref="Show"/> may be called any number of times and must
/// surface the same window each time rather than stacking a new one behind the old. That is why
/// this seam is registered as a singleton and takes its version source by constructor
/// injection.</item>
/// <item>Not modal, and nothing is returned: showing it is the whole interaction. Whether the
/// user closes it, and when, is of no interest to the caller.</item>
/// </list>
/// </summary>
public interface IAboutView
{
    /// <summary>
    /// Show the About window, bringing the existing one forward if it is already open. Safe to
    /// call repeatedly.
    /// </summary>
    void Show();
}

using Avalonia;

namespace Diz.Ui.Avalonia;

/// <summary>
/// One-time, idempotent Avalonia bootstrap for hosting Avalonia windows inside a process
/// whose message loop is owned by another toolkit (today: WinForms).
///
/// The bootstrap is exactly the one the Phase 0 spike proved on Avalonia 12.1.0:
/// SetupWithoutStarting() builds the platform + dispatcher but never enters Avalonia's own
/// message loop -- the host toolkit's loop (WinForms Application.Run) pumps the shared
/// UI thread, and the spike verified Dispatcher.UIThread jobs execute under it.
///
/// TIMING CONSTRAINT (Phase 0, observed crash): in the WinForms app this must never run
/// before WinformsGuiUtil.SetupDpiStuff(). The app layer therefore calls
/// <see cref="EnsureInitialized"/> from a first-Application.Idle hook (which fires only
/// once the WinForms loop is pumping, i.e. after DPI setup); <see cref="EnsureInitialized"/>
/// is also called lazily from AvaloniaLabelEditorView.Show() as a safety net.
/// </summary>
public static class AvaloniaGuiHost
{
    private static bool initialized;

    public static bool IsInitialized => initialized;

    /// <summary>Must be called on the UI thread. Safe to call repeatedly.</summary>
    public static void EnsureInitialized()
    {
        if (initialized)
            return;

        // If some other code in the process already set up an Avalonia Application (e.g.
        // the throwaway Phase 0 spike under --avalonia-spike), don't double-initialize --
        // AppBuilder throws on a second setup. We can host our windows on any live
        // Avalonia platform, though the theme policy will then be that App's, not ours.
        if (global::Avalonia.Application.Current != null)
        {
            initialized = true;
            return;
        }

        AppBuilder.Configure<DizAvaloniaApp>()
            .UsePlatformDetect()
            .LogToTrace()
            .SetupWithoutStarting();

        initialized = true;
    }
}

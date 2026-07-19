using System.Runtime.InteropServices;

namespace Diz.Ui.Tui;

/// <summary>
/// P/Invoke helper that gives the (GUI-subsystem) WinForms process a real console for
/// Terminal.Gui to draw into, and tears it back down cleanly.
///
/// Diz.App.Winforms is a WinExe: it has NO console by default, so Terminal.Gui has nothing to
/// render to until we <see cref="EnsureConsole"/> (kernel32 AllocConsole). If a console already
/// exists (e.g. the app was launched from a terminal), we reuse it and must NOT free it on exit.
///
/// This is deliberately plain P/Invoke (works on the plain net10.0 TFM); no WinForms dependency.
/// </summary>
internal sealed class ConsoleHost
{
    // ---- kernel32 ------------------------------------------------------------------------

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    private delegate bool ConsoleCtrlDelegate(uint ctrlType);

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate? handler, bool add);

    // console control signals we care about (wincon.h)
    private const uint CTRL_C_EVENT = 0;
    private const uint CTRL_BREAK_EVENT = 1;
    private const uint CTRL_CLOSE_EVENT = 2;

    // ---- state ---------------------------------------------------------------------------

    /// <summary>True only when *we* allocated the console (so only then may we free it).</summary>
    private bool weAllocated;

    // Kept as a field so the GC cannot collect the delegate the OS still holds a pointer to.
    private ConsoleCtrlDelegate? ctrlHandler;

    /// <summary>Invoked (best effort) when the user clicks the console's X / sends Ctrl-C. The
    /// host wires this to a clean Terminal.Gui stop. CTRL_CLOSE gives a ~5s un-vetoable grace
    /// window before the OS kills the process; a fast clean return is all we attempt.</summary>
    public Action? OnCloseRequested { get; set; }

    /// <summary>
    /// Ensure the process has a console and that <see cref="Console"/>'s cached stream handles
    /// point at it. Returns true if a console is available afterwards.
    /// </summary>
    public bool EnsureConsole()
    {
        if (GetConsoleWindow() != IntPtr.Zero)
        {
            // A console already exists (launched from a terminal). Reuse it; don't free it later.
            weAllocated = false;
        }
        else
        {
            if (!AllocConsole())
                return false;
            weAllocated = true;
        }

        FixCachedConsoleHandles();
        InstallCtrlHandler();
        return true;
    }

    /// <summary>
    /// The app wrote to Console during project load, so Console.Out/Error/In cached handles that
    /// are dead after AllocConsole. Rebuild them against the freshly-opened standard streams.
    /// </summary>
    private static void FixCachedConsoleHandles()
    {
        var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        Console.SetOut(stdout);

        var stderr = new StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
        Console.SetError(stderr);

        var stdin = new StreamReader(Console.OpenStandardInput());
        Console.SetIn(stdin);
    }

    private void InstallCtrlHandler()
    {
        ctrlHandler = ctrlType =>
        {
            switch (ctrlType)
            {
                case CTRL_C_EVENT:
                case CTRL_BREAK_EVENT:
                case CTRL_CLOSE_EVENT:
                    try { OnCloseRequested?.Invoke(); } catch { /* best effort */ }
                    return true; // signal handled
                default:
                    return false;
            }
        };
        SetConsoleCtrlHandler(ctrlHandler, add: true);
    }

    /// <summary>
    /// Remove the ctrl handler and, only if we allocated the console ourselves, free it. Safe to
    /// call more than once.
    /// </summary>
    public void ReleaseConsole()
    {
        if (ctrlHandler != null)
        {
            SetConsoleCtrlHandler(ctrlHandler, add: false);
            ctrlHandler = null;
        }

        if (weAllocated)
        {
            FreeConsole();
            weAllocated = false;
        }
    }
}

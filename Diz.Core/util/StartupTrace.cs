// Lightweight startup/load timing trace. Records a few coarse milestones (window/config init,
// project-open start/finish, etc.) to a log file in the temp dir so slow-startup issues can be
// diagnosed after the fact. Deliberately dependency-free and always safe: every call swallows its
// own exceptions, so tracing can never throw into (or slow down) the code it observes.
#nullable enable
using System;
using System.IO;

namespace Diz.Core.util;

public static class StartupTrace
{
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "diz-startup-trace.log");
    private static readonly object Lock = new();

    // Call once at process start to truncate the previous run's log.
    public static void Reset()
    {
        try { lock (Lock) File.WriteAllText(LogPath, $"=== diz-startup-trace {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n"); }
        catch { /* diagnostics must never throw */ }
    }

    // Append/close every call so the last line survives even if the process hangs or is killed.
    public static void Log(string msg)
    {
        try
        {
            lock (Lock)
                File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} [t{Environment.CurrentManagedThreadId}] {msg}{Environment.NewLine}");
        }
        catch { /* diagnostics must never throw */ }
    }
}

using System;
using System.IO;
using System.Text;

namespace Diz.Spike.Avalonia;

/// <summary>
/// Spike-only logger. The WinForms app is a WinExe so there is no console;
/// everything goes to a file we can read after the run.
/// </summary>
public static class SpikeLog
{
    private static readonly object Lock = new();

    public static string LogPath { get; } =
        Path.Combine(Path.GetTempPath(), "diz_avalonia_spike.log");

    public static string ArtifactDir { get; } =
        Path.Combine(Path.GetTempPath(), "diz_avalonia_spike");

    static SpikeLog()
    {
        Directory.CreateDirectory(ArtifactDir);
        try { File.Delete(LogPath); } catch { /* first run */ }
        Write($"=== spike log start {DateTime.Now:O} ===");
        Write($"artifacts: {ArtifactDir}");
    }

    public static void Write(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [tid:{Environment.CurrentManagedThreadId}] {msg}";
        lock (Lock)
        {
            File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
        }
        Console.WriteLine(line);
    }

    public static void Error(string what, Exception ex) =>
        Write($"!! FAIL {what}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
}

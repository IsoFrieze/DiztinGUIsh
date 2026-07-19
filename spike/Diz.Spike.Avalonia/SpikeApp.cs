using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Diz.Spike.Avalonia;

/// <summary>
/// Minimal code-only Avalonia Application. No XAML, so the spike needs no
/// AvaloniaResource/XAML-compiler wiring in the csproj.
/// </summary>
public class SpikeApp : global::Avalonia.Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());

        // Follow the OS light/dark setting so we can observe whether it matches
        // what WinForms is doing in the same process.
        RequestedThemeVariant = ThemeVariant.Default;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // NOTE: deliberately NOT setting MainWindow / not using a
        // ClassicDesktopStyleApplicationLifetime. WinForms owns the process
        // lifetime; Avalonia here is a guest with no lifetime at all
        // (ApplicationLifetime stays null under SetupWithoutStarting).
        SpikeLog.Write($"SpikeApp.OnFrameworkInitializationCompleted; lifetime={(ApplicationLifetime?.GetType().Name ?? "<null>")}");
        base.OnFrameworkInitializationCompleted();
    }
}

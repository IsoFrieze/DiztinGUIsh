using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Diz.Ui.Avalonia;

// NAMESPACE GOTCHA (applies to every file in this project): inside `namespace
// Diz.Ui.Avalonia`, the identifier `Avalonia` resolves to THIS namespace (as a member of
// Diz.Ui), not to the Avalonia framework's root namespace. Never write qualified
// `Avalonia.Xxx` references in code here -- rely on `using` directives (which are resolved
// at compilation-unit scope, where `Avalonia` is the framework) or use `global::Avalonia`.
// This is the type-name-collision issue the Phase 0 spike documented.

/// <summary>
/// The process-wide Avalonia Application object for Diz's Avalonia-backed windows.
/// Code-only (no XAML), matching the Phase 0 spike: no AvaloniaResource/XAML-compiler
/// wiring needed in the csproj.
/// </summary>
public class DizAvaloniaApp : global::Avalonia.Application
{
    /// <summary>Env var that overrides the theme: "light" (default), "dark", or "os".</summary>
    public const string ThemeEnvVarName = "DIZ_AVALONIA_THEME";

    /// <summary>
    /// Where the DataGrid's control theme lives. The control ships in its own assembly and its
    /// theme ships as a separate style resource inside that assembly, so adding the package is
    /// only half of it: without this include every DataGrid renders as an untemplated,
    /// effectively invisible control. Read off the installed package -- the assembly is
    /// Avalonia.Controls.DataGrid and it carries /Themes/Fluent.xaml (and a Simple.xaml, for the
    /// theme this app does not use).
    /// </summary>
    private const string DataGridFluentThemeUri = "avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml";

    /// <summary>Base URI for resolving style includes: this assembly. The include below is
    /// absolute, so this only ever serves as the resolution context.</summary>
    private const string ThisAssemblyBaseUri = "avares://Diz.Ui.Avalonia/";

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());

        // AFTER the Fluent theme: the DataGrid's theme is written against Fluent's resource keys
        // (accents, control brushes), and it carries its own light/dark ThemeDictionaries, so the
        // theme variant chosen below applies to it as well.
        Styles.Add(new StyleInclude(new Uri(ThisAssemblyBaseUri))
        {
            Source = new Uri(DataGridFluentThemeUri),
        });

        // THEMING DECISION (new-ui plan step 5, "Still open" item): PIN TO LIGHT by
        // default instead of following the OS. Rationale: Diz's WinForms UI is light; the
        // Phase 0 spike observed Avalonia resolving Dark from the OS, which puts a dark
        // window next to a light app -- "merely inconsistent, not visibly broken" under
        // the separate-window model, but still jarring. Pinning to light matches WinForms
        // until the whole app is one toolkit. USER-REVERSIBLE via DIZ_AVALONIA_THEME=dark
        // (or =os to follow the system setting).
        RequestedThemeVariant = ThemeVariantFrom(
            Environment.GetEnvironmentVariable(ThemeEnvVarName));
    }

    /// <summary>Pure mapping so it is testable without touching real env vars.</summary>
    public static ThemeVariant ThemeVariantFrom(string? envValue) =>
        envValue?.Trim().ToLowerInvariant() switch
        {
            "dark" => ThemeVariant.Dark,
            "os" or "system" or "default" => ThemeVariant.Default, // follow the OS
            _ => ThemeVariant.Light, // includes null, "", "light", and anything unrecognized
        };
}

#nullable enable

using System;
using System.Windows.Forms;
using Diz.Ui.Avalonia;

namespace Diz.App.Winforms;

public enum LabelEditorBackendKind
{
    WinForms,
    Avalonia,
}

/// <summary>
/// THE RUNTIME BACKEND SWITCH (new-ui plan step 5).
///
/// Set the environment variable <c>DIZ_LABEL_EDITOR=avalonia</c> before launching
/// Diz.App.Winforms to get the Avalonia label editor (a separate top-level Avalonia
/// window). Anything else -- including unset -- keeps the WinForms label editor, so
/// default behavior is unchanged.
///
/// Example (PowerShell):
///     $env:DIZ_LABEL_EDITOR = "avalonia"; .\Diz.App.Winforms.exe
///
/// Mechanism: when avalonia is selected, DizWinformsRegisterServices additionally loads
/// DizUiAvaloniaCompositionRoot AFTER the WinForms composition root; its "LabelEditorView"
/// (and IFileDialogService) registrations override the WinForms ones, and everything else
/// in the app stays WinForms. An env var was chosen as the least invasive mechanism: no
/// settings-file schema change, trivially flippable per-launch, and readable at
/// composition time before any UI exists.
/// </summary>
public static class LabelEditorBackend
{
    public const string EnvVarName = "DIZ_LABEL_EDITOR";

    public static LabelEditorBackendKind Parse(string? value) =>
        string.Equals(value?.Trim(), "avalonia", StringComparison.OrdinalIgnoreCase)
            ? LabelEditorBackendKind.Avalonia
            : LabelEditorBackendKind.WinForms;

    public static LabelEditorBackendKind FromEnvironment() =>
        Parse(Environment.GetEnvironmentVariable(EnvVarName));

    private static bool armed;

    /// <summary>
    /// Pre-initialize Avalonia on the first Application.Idle, per the Phase 0 findings:
    /// Idle creates no window handle (so it cannot break WinformsGuiUtil.SetupDpiStuff's
    /// SetCompatibleTextRenderingDefault requirement) and first fires once the WinForms
    /// message loop is actually pumping. Called from Program.Main only when the Avalonia
    /// backend is selected; the ~200-300 ms Avalonia setup then happens at idle instead of
    /// on the user's first Tools -> Label Editor click.
    /// </summary>
    public static void ArmAvaloniaPreInitOnFirstIdle()
    {
        if (armed)
            return;
        armed = true;
        Application.Idle += InitializeOnFirstIdle;
    }

    private static void InitializeOnFirstIdle(object? sender, EventArgs e)
    {
        Application.Idle -= InitializeOnFirstIdle;
        AvaloniaGuiHost.EnsureInitialized();
    }
}

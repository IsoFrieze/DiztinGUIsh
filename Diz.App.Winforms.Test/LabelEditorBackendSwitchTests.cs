using System;
using Diz.App.Winforms;
using Diz.Controllers.interfaces;
using Diz.Core.util;
using Diz.Ui.Avalonia;
using Diz.Ui.Tui;
using Diz.Ui.Winforms.dialogs;
using Diz.Ui.Winforms.usercontrols;
using Diz.Ui.Winforms.util;
using FluentAssertions;
using LightInject;
using Xunit;

namespace Diz.App.Winforms.Test;

/// <summary>
/// New-ui plan step 5/6, exit criterion: "a runtime switch selects the WinForms or Avalonia
/// label editor; both work." These tests prove the switch selects the right backend at the
/// DI level, both ways, using the app's REAL registration path
/// (DizWinformsRegisterServices.RegisterDizUiServices).
///
/// Step 6 replaced the old last-registration-wins ordering trick with an EXPLICIT if/else
/// branch that registers EITHER the WinForms backend root OR the Avalonia backend root -- never
/// both. So each backend must resolve ALL THREE backend-selectable seams (LabelEditorView,
/// ProgressBarView, IFileDialogService) to its own toolkit's types, and never the other's.
/// If someone breaks or reorders the branch (e.g. registers both roots), these type assertions
/// fail.
///
/// Resolution constructs the real view objects headlessly: the WinForms path builds the
/// LabelEditorForm host + control and the ProgressDialog (no handle until Show; precedent:
/// FileDialogServiceRegistrationTests in Diz.Ui.Winforms.Test), and the Avalonia path
/// builds only the thin adapter objects -- by design they must not initialize the Avalonia
/// platform until Show().
/// </summary>
public class LabelEditorBackendSwitchTests
{
    private static ServiceContainer CreateAppContainer(LabelEditorBackendKind backend)
    {
        var container = (ServiceContainer)DizServiceProvider.CreateServiceContainer();
        DizWinformsRegisterServices.RegisterDizUiServices(container, backend);
        return container;
    }

    [Fact]
    public void WinformsBackend_ResolvesTheWinformsSeams()
    {
        using var container = CreateAppContainer(LabelEditorBackendKind.WinForms);

        container.GetInstance<ILabelEditorView>("LabelEditorView")
            .Should().BeOfType<LabelsViewControl>();
        container.GetInstance<IProgressView>("ProgressBarView")
            .Should().BeOfType<ProgressDialog>();
        container.GetInstance<IFileDialogService>()
            .Should().BeOfType<WinformsFileDialogService>();
    }

    [Fact]
    public void AvaloniaBackend_ResolvesTheAvaloniaSeams()
    {
        using var container = CreateAppContainer(LabelEditorBackendKind.Avalonia);

        container.GetInstance<ILabelEditorView>("LabelEditorView")
            .Should().BeOfType<AvaloniaLabelEditorView>();
        container.GetInstance<IProgressView>("ProgressBarView")
            .Should().BeOfType<AvaloniaProgressView>();
        container.GetInstance<IFileDialogService>()
            .Should().BeOfType<AvaloniaFileDialogService>();
    }

    [Fact]
    public void TuiBackend_ResolvesTheTuiEditorAndKeepsWinformsProgressAndFileDialog()
    {
        using var container = CreateAppContainer(LabelEditorBackendKind.Tui);

        // TUI backend: only the label editor is TUI...
        container.GetInstance<ILabelEditorView>("LabelEditorView")
            .Should().BeOfType<TuiLabelEditorView>();
        // ...the progress popup and file dialogs stay WinForms (registered explicitly in the
        // tui branch, not via the WinForms backend root).
        container.GetInstance<IProgressView>("ProgressBarView")
            .Should().BeOfType<ProgressDialog>();
        container.GetInstance<IFileDialogService>()
            .Should().BeOfType<WinformsFileDialogService>();
    }

    [Fact]
    public void AvaloniaBackend_DoesNotInitializeAvalonia_JustByResolvingTheView()
    {
        using var container = CreateAppContainer(LabelEditorBackendKind.Avalonia);
        container.GetInstance<ILabelEditorView>("LabelEditorView");

        // the timing constraint from Phase 0: Avalonia must not come up before the
        // message loop / DPI setup. Resolution happens in MainWindow's ctor, so it must
        // stay inert. (If some earlier test initialized Avalonia in this process this
        // assertion would be unable to distinguish that -- no test in this assembly does.)
        AvaloniaGuiHost.IsInitialized.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, LabelEditorBackendKind.WinForms)]
    [InlineData("", LabelEditorBackendKind.WinForms)]
    [InlineData("winforms", LabelEditorBackendKind.WinForms)]
    [InlineData("garbage", LabelEditorBackendKind.WinForms)]
    [InlineData("avalonia", LabelEditorBackendKind.Avalonia)]
    [InlineData("AVALONIA", LabelEditorBackendKind.Avalonia)]
    [InlineData("  Avalonia  ", LabelEditorBackendKind.Avalonia)]
    [InlineData("tui", LabelEditorBackendKind.Tui)]
    [InlineData("TUI", LabelEditorBackendKind.Tui)]
    [InlineData("  Tui  ", LabelEditorBackendKind.Tui)]
    public void Parse_MapsEnvVarValues(string? value, LabelEditorBackendKind expected) =>
        LabelEditorBackend.Parse(value).Should().Be(expected);

    [Fact]
    public void FromEnvironment_ReadsTheDocumentedEnvVar()
    {
        var original = Environment.GetEnvironmentVariable(LabelEditorBackend.EnvVarName);
        try
        {
            Environment.SetEnvironmentVariable(LabelEditorBackend.EnvVarName, "avalonia");
            LabelEditorBackend.FromEnvironment().Should().Be(LabelEditorBackendKind.Avalonia);

            Environment.SetEnvironmentVariable(LabelEditorBackend.EnvVarName, null);
            LabelEditorBackend.FromEnvironment().Should().Be(LabelEditorBackendKind.WinForms);
        }
        finally
        {
            Environment.SetEnvironmentVariable(LabelEditorBackend.EnvVarName, original);
        }
    }
}

using System;
using Diz.App.Winforms;
using Diz.Controllers.interfaces;
using Diz.Core.util;
using Diz.Ui.Avalonia;
using Diz.Ui.Winforms.usercontrols;
using Diz.Ui.Winforms.util;
using FluentAssertions;
using LightInject;
using Xunit;

namespace Diz.App.Winforms.Test;

/// <summary>
/// New-ui plan step 5, exit criterion: "a runtime switch selects the WinForms or Avalonia
/// label editor; both work." These tests prove the switch selects the right backend at the
/// DI level, both ways, using the app's REAL registration path
/// (DizWinformsRegisterServices.RegisterDizUiServices) -- including the load-bearing
/// LightInject behavior that a later registration under the same name/type overrides the
/// earlier one (the Avalonia root is registered after the WinForms root).
///
/// Resolution constructs the real view objects headlessly: the WinForms path builds the
/// LabelEditorForm host + control (no handle until Show; precedent:
/// FileDialogServiceRegistrationTests in Diz.Ui.Winforms.Test), and the Avalonia path
/// builds only the thin AvaloniaLabelEditorView adapter -- by design it must not
/// initialize the Avalonia platform until Show().
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
    public void WinformsBackend_ResolvesTheWinformsLabelEditor()
    {
        using var container = CreateAppContainer(LabelEditorBackendKind.WinForms);

        container.GetInstance<ILabelEditorView>("LabelEditorView")
            .Should().BeOfType<LabelsViewControl>();
        container.GetInstance<IFileDialogService>()
            .Should().BeOfType<WinformsFileDialogService>();
    }

    [Fact]
    public void AvaloniaBackend_ResolvesTheAvaloniaLabelEditor_AndItsFileDialogService()
    {
        using var container = CreateAppContainer(LabelEditorBackendKind.Avalonia);

        container.GetInstance<ILabelEditorView>("LabelEditorView")
            .Should().BeOfType<AvaloniaLabelEditorView>();
        // proves last-registration-wins override of the unnamed seam registration too
        container.GetInstance<IFileDialogService>()
            .Should().BeOfType<AvaloniaFileDialogService>();
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

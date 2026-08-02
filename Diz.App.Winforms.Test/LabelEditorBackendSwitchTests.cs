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
/// both. So each backend must resolve ALL NINE backend-selectable seams (LabelEditorView,
/// RegionEditorView, MarkManyView, GotoView, HarshAutoStepView, MisalignmentCheckerView,
/// InOutPointCheckerView, ProgressBarView, IFileDialogService) to its own toolkit's types, and
/// never the other's. If someone breaks or reorders the branch (e.g. registers both roots),
/// these type assertions fail.
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
        container.GetInstance<IRegionListView>("RegionEditorView")
            .Should().BeOfType<RegionListViewControl>();
        container.GetInstance<IMarkManyView>("MarkManyView")
            .Should().BeOfType<WinformsMarkManyView>();
        container.GetInstance<IGotoView>("GotoView")
            .Should().BeOfType<WinformsGotoView>();
        container.GetInstance<IHarshAutoStepView>("HarshAutoStepView")
            .Should().BeOfType<WinformsHarshAutoStepView>();
        container.GetInstance<IMisalignmentCheckerView>("MisalignmentCheckerView")
            .Should().BeOfType<WinformsMisalignmentCheckerView>();
        container.GetInstance<IInOutPointCheckerView>("InOutPointCheckerView")
            .Should().BeOfType<WinformsInOutPointCheckerView>();
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
        container.GetInstance<IRegionListView>("RegionEditorView")
            .Should().BeOfType<AvaloniaRegionListView>();
        container.GetInstance<IMarkManyView>("MarkManyView")
            .Should().BeOfType<AvaloniaMarkManyView>();
        container.GetInstance<IGotoView>("GotoView")
            .Should().BeOfType<AvaloniaGotoView>();
        container.GetInstance<IHarshAutoStepView>("HarshAutoStepView")
            .Should().BeOfType<AvaloniaHarshAutoStepView>();
        container.GetInstance<IMisalignmentCheckerView>("MisalignmentCheckerView")
            .Should().BeOfType<AvaloniaMisalignmentCheckerView>();
        container.GetInstance<IInOutPointCheckerView>("InOutPointCheckerView")
            .Should().BeOfType<AvaloniaInOutPointCheckerView>();
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
        // ...the region editor, mark-many window, goto window, harsh-auto-step window, the two
        // checker windows, progress popup and file dialogs stay WinForms (registered explicitly
        // in the tui branch, not via the WinForms backend root).
        container.GetInstance<IRegionListView>("RegionEditorView")
            .Should().BeOfType<RegionListViewControl>();
        container.GetInstance<IMarkManyView>("MarkManyView")
            .Should().BeOfType<WinformsMarkManyView>();
        container.GetInstance<IGotoView>("GotoView")
            .Should().BeOfType<WinformsGotoView>();
        container.GetInstance<IHarshAutoStepView>("HarshAutoStepView")
            .Should().BeOfType<WinformsHarshAutoStepView>();
        container.GetInstance<IMisalignmentCheckerView>("MisalignmentCheckerView")
            .Should().BeOfType<WinformsMisalignmentCheckerView>();
        container.GetInstance<IInOutPointCheckerView>("InOutPointCheckerView")
            .Should().BeOfType<WinformsInOutPointCheckerView>();
        container.GetInstance<IProgressView>("ProgressBarView")
            .Should().BeOfType<ProgressDialog>();
        container.GetInstance<IFileDialogService>()
            .Should().BeOfType<WinformsFileDialogService>();
    }

    /// <summary>
    /// The mark-many view is resolved fresh for every invocation and thrown away afterwards,
    /// so two resolutions must never hand back the same object (a singleton would leak one
    /// window's state into the next edit).
    /// </summary>
    [Theory]
    [InlineData(LabelEditorBackendKind.WinForms)]
    [InlineData(LabelEditorBackendKind.Avalonia)]
    [InlineData(LabelEditorBackendKind.Tui)]
    public void MarkManyView_ResolvesAFreshInstanceEachTime(LabelEditorBackendKind backend)
    {
        using var container = CreateAppContainer(backend);

        var first = container.GetInstance<IMarkManyView>("MarkManyView");
        var second = container.GetInstance<IMarkManyView>("MarkManyView");

        second.Should().NotBeSameAs(first);
    }

    /// <summary>
    /// The registration name is a bare string that has to match the auto-factory method name
    /// exactly, and nothing checks that at compile time. This resolves through the factory
    /// itself -- the path MainWindow actually takes -- so a typo fails here instead of at the
    /// user's first click.
    /// </summary>
    [Theory]
    [InlineData(LabelEditorBackendKind.WinForms)]
    [InlineData(LabelEditorBackendKind.Avalonia)]
    [InlineData(LabelEditorBackendKind.Tui)]
    public void ViewFactory_HandsOutAMarkManyView_OnEveryBackend(LabelEditorBackendKind backend)
    {
        using var container = CreateAppContainer(backend);

        container.GetInstance<IViewFactory>().GetMarkManyView().Should().NotBeNull();
    }

    /// <summary>
    /// The goto view is resolved fresh for every invocation and thrown away afterwards, so two
    /// resolutions must never hand back the same object (a singleton would leak one window's
    /// state into the next edit).
    /// </summary>
    [Theory]
    [InlineData(LabelEditorBackendKind.WinForms)]
    [InlineData(LabelEditorBackendKind.Avalonia)]
    [InlineData(LabelEditorBackendKind.Tui)]
    public void GotoView_ResolvesAFreshInstanceEachTime(LabelEditorBackendKind backend)
    {
        using var container = CreateAppContainer(backend);

        var first = container.GetInstance<IGotoView>("GotoView");
        var second = container.GetInstance<IGotoView>("GotoView");

        second.Should().NotBeSameAs(first);
    }

    /// <summary>
    /// Same unchecked-string risk as the mark-many registration: "GotoView" has to match the
    /// auto-factory method name exactly, and nothing checks that at compile time. This resolves
    /// through the factory itself -- the path MainWindow actually takes -- so a typo fails here
    /// instead of at the user's first click.
    /// </summary>
    [Theory]
    [InlineData(LabelEditorBackendKind.WinForms)]
    [InlineData(LabelEditorBackendKind.Avalonia)]
    [InlineData(LabelEditorBackendKind.Tui)]
    public void ViewFactory_HandsOutAGotoView_OnEveryBackend(LabelEditorBackendKind backend)
    {
        using var container = CreateAppContainer(backend);

        container.GetInstance<IViewFactory>().GetGotoView().Should().NotBeNull();
    }

    /// <summary>
    /// The harsh-auto-step view is resolved fresh for every invocation and thrown away
    /// afterwards, so two resolutions must never hand back the same object (a singleton would
    /// leak one window's state into the next edit).
    /// </summary>
    [Theory]
    [InlineData(LabelEditorBackendKind.WinForms)]
    [InlineData(LabelEditorBackendKind.Avalonia)]
    [InlineData(LabelEditorBackendKind.Tui)]
    public void HarshAutoStepView_ResolvesAFreshInstanceEachTime(LabelEditorBackendKind backend)
    {
        using var container = CreateAppContainer(backend);

        var first = container.GetInstance<IHarshAutoStepView>("HarshAutoStepView");
        var second = container.GetInstance<IHarshAutoStepView>("HarshAutoStepView");

        second.Should().NotBeSameAs(first);
    }

    /// <summary>
    /// Same unchecked-string risk as the other registrations: "HarshAutoStepView" has to match
    /// the auto-factory method name exactly, and nothing checks that at compile time. This
    /// resolves through the factory itself -- the path MainWindow actually takes -- so a typo
    /// fails here instead of at the user's first click.
    /// </summary>
    [Theory]
    [InlineData(LabelEditorBackendKind.WinForms)]
    [InlineData(LabelEditorBackendKind.Avalonia)]
    [InlineData(LabelEditorBackendKind.Tui)]
    public void ViewFactory_HandsOutAHarshAutoStepView_OnEveryBackend(LabelEditorBackendKind backend)
    {
        using var container = CreateAppContainer(backend);

        container.GetInstance<IViewFactory>().GetHarshAutoStepView().Should().NotBeNull();
    }

    /// <summary>
    /// The misaligned-flags view is resolved fresh for every invocation and thrown away
    /// afterwards, so two resolutions must never hand back the same object (a singleton would
    /// leak one window's report into the next scan).
    /// </summary>
    [Theory]
    [InlineData(LabelEditorBackendKind.WinForms)]
    [InlineData(LabelEditorBackendKind.Avalonia)]
    [InlineData(LabelEditorBackendKind.Tui)]
    public void MisalignmentCheckerView_ResolvesAFreshInstanceEachTime(LabelEditorBackendKind backend)
    {
        using var container = CreateAppContainer(backend);

        var first = container.GetInstance<IMisalignmentCheckerView>("MisalignmentCheckerView");
        var second = container.GetInstance<IMisalignmentCheckerView>("MisalignmentCheckerView");

        second.Should().NotBeSameAs(first);
    }

    /// <summary>
    /// Same unchecked-string risk as the other registrations: "MisalignmentCheckerView" has to
    /// match the auto-factory method name exactly, and nothing checks that at compile time. This
    /// resolves through the factory itself -- the path MainWindow actually takes -- so a typo
    /// fails here instead of at the user's first click.
    /// </summary>
    [Theory]
    [InlineData(LabelEditorBackendKind.WinForms)]
    [InlineData(LabelEditorBackendKind.Avalonia)]
    [InlineData(LabelEditorBackendKind.Tui)]
    public void ViewFactory_HandsOutAMisalignmentCheckerView_OnEveryBackend(LabelEditorBackendKind backend)
    {
        using var container = CreateAppContainer(backend);

        container.GetInstance<IViewFactory>().GetMisalignmentCheckerView().Should().NotBeNull();
    }

    /// <summary>
    /// The in/out-point rescan confirmation is resolved fresh for every invocation and thrown
    /// away afterwards, so two resolutions must never hand back the same object (a singleton
    /// would hand back a window whose task has already completed, and the second ask would
    /// answer itself).
    /// </summary>
    [Theory]
    [InlineData(LabelEditorBackendKind.WinForms)]
    [InlineData(LabelEditorBackendKind.Avalonia)]
    [InlineData(LabelEditorBackendKind.Tui)]
    public void InOutPointCheckerView_ResolvesAFreshInstanceEachTime(LabelEditorBackendKind backend)
    {
        using var container = CreateAppContainer(backend);

        var first = container.GetInstance<IInOutPointCheckerView>("InOutPointCheckerView");
        var second = container.GetInstance<IInOutPointCheckerView>("InOutPointCheckerView");

        second.Should().NotBeSameAs(first);
    }

    /// <summary>
    /// Same unchecked-string risk as the other registrations: "InOutPointCheckerView" has to
    /// match the auto-factory method name exactly, and nothing checks that at compile time. This
    /// resolves through the factory itself -- the path MainWindow actually takes -- so a typo
    /// fails here instead of at the user's first click.
    /// </summary>
    [Theory]
    [InlineData(LabelEditorBackendKind.WinForms)]
    [InlineData(LabelEditorBackendKind.Avalonia)]
    [InlineData(LabelEditorBackendKind.Tui)]
    public void ViewFactory_HandsOutAnInOutPointCheckerView_OnEveryBackend(LabelEditorBackendKind backend)
    {
        using var container = CreateAppContainer(backend);

        container.GetInstance<IViewFactory>().GetInOutPointCheckerView().Should().NotBeNull();
    }

    /// <summary>
    /// The region editor is NOT a per-invocation dialog: MainWindow resolves one in its
    /// constructor and keeps it for the whole run, the same shape the label editor has. So this
    /// deliberately does NOT assert the per-invocation seams' "fresh instance each time"
    /// contract, which would be the wrong requirement to write down for a cached window -- and
    /// the label editor, the other long-lived seam, asserts nothing about lifetime at all.
    ///
    /// What DOES have to hold is that the registration is not a SINGLETON. Each owner rebinds
    /// its view to its own project controller, so two owners sharing one instance would leave
    /// one of them driving the other's project. That is the same reason the WinForms-side
    /// registration test gives, checked here across every backend.
    /// </summary>
    [Theory]
    [InlineData(LabelEditorBackendKind.WinForms)]
    [InlineData(LabelEditorBackendKind.Avalonia)]
    [InlineData(LabelEditorBackendKind.Tui)]
    public void RegionEditorView_IsNotSharedBetweenOwners(LabelEditorBackendKind backend)
    {
        using var container = CreateAppContainer(backend);

        var first = container.GetInstance<IRegionListView>("RegionEditorView");
        var second = container.GetInstance<IRegionListView>("RegionEditorView");

        second.Should().NotBeSameAs(first);
    }

    /// <summary>
    /// Same unchecked-string risk as the other registrations: "RegionEditorView" has to match the
    /// auto-factory method name exactly, and nothing checks that at compile time. This resolves
    /// through the factory itself -- the path MainWindow actually takes -- so a typo fails here
    /// instead of at the user's first Tools -> Region List.
    /// </summary>
    [Theory]
    [InlineData(LabelEditorBackendKind.WinForms)]
    [InlineData(LabelEditorBackendKind.Avalonia)]
    [InlineData(LabelEditorBackendKind.Tui)]
    public void ViewFactory_HandsOutARegionEditorView_OnEveryBackend(LabelEditorBackendKind backend)
    {
        using var container = CreateAppContainer(backend);

        container.GetInstance<IViewFactory>().GetRegionEditorView().Should().NotBeNull();
    }

    [Fact]
    public void AvaloniaBackend_DoesNotInitializeAvalonia_JustByResolvingTheView()
    {
        using var container = CreateAppContainer(LabelEditorBackendKind.Avalonia);
        container.GetInstance<ILabelEditorView>("LabelEditorView");
        container.GetInstance<IRegionListView>("RegionEditorView");
        container.GetInstance<IMarkManyView>("MarkManyView");
        container.GetInstance<IGotoView>("GotoView");
        container.GetInstance<IHarshAutoStepView>("HarshAutoStepView");
        container.GetInstance<IMisalignmentCheckerView>("MisalignmentCheckerView");
        container.GetInstance<IInOutPointCheckerView>("InOutPointCheckerView");

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

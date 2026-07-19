using System;
using System.Linq;
using System.Reflection;
using Avalonia.Styling;
using Diz.Controllers.interfaces;
using Diz.Ui.Avalonia;
using FluentAssertions;
using LightInject;
using Xunit;

namespace Diz.App.Winforms.Test;

// NOTE: these live here (not next to Diz.Test's LabelViewModelAssemblyRulesTests, whose
// pattern they mirror) deliberately: referencing Diz.Ui.Avalonia from Diz.Test would pull
// Avalonia transitively into the Diz.Ui.Winforms SUBMODULE's test project (regenerating
// its packages.lock.json), and step 5 keeps all changes out of that submodule. This
// project already reaches Diz.Ui.Avalonia through Diz.App.Winforms.

/// <summary>
/// New-ui plan step 5 rules for the Diz.Ui.Avalonia assembly, mirroring the pattern of
/// LabelViewModelAssemblyRulesTests: a future edit that violates a plan decision fails a
/// test instead of being discovered at runtime.
///
/// Decision 4 (no UI interop at any granularity) means this assembly may never reference
/// WinForms, Eto, or Avalonia's WinForms-interop package. Its csproj additionally guards
/// the TFM at build time; this checks the compiled reality.
/// </summary>
public class DizUiAvaloniaAssemblyRulesTests
{
    private static Assembly AvaloniaUiAssembly => typeof(AvaloniaLabelEditorView).Assembly;

    [Fact]
    public void Assembly_ReferencesNoWinFormsEtoOrInteropToolkit()
    {
        var refs = AvaloniaUiAssembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        refs.Should().NotContain(n =>
            n.Contains("System.Windows.Forms", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Diz.Ui.Winforms", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Eto", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("System.Drawing", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Interoperability", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Avalonia.Win32", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Assembly_MayReferenceOnlyTheAllowedDizAssemblies()
    {
        // the view/host layer legitimately needs more than the VM assembly does:
        // Controllers (ILabelEditorView, IFileDialogService, IProjectController),
        // Cpu.65816 (the IA-resolution port wiring), and the VM assembly itself.
        var dizRefs = AvaloniaUiAssembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("Diz.", StringComparison.Ordinal))
            .ToList();

        dizRefs.Should().BeSubsetOf([
            "Diz.Controllers",
            "Diz.Core",
            "Diz.Core.Interfaces",
            "Diz.Cpu.65816",
            "Diz.Import",
            "Diz.Ui.ViewModels",
        ]);
    }
}

/// <summary>Registration shape of the Avalonia composition root (mirrors the WinForms
/// FileDialogServiceRegistrationTests): resolving must construct the thin host adapter
/// without initializing the Avalonia platform.</summary>
public class DizUiAvaloniaCompositionRootTests
{
    [Fact]
    public void CompositionRoot_ResolvesLabelEditorView_UnderTheViewFactoryKey()
    {
        using var container = new ServiceContainer();
        container.RegisterFrom<DizUiAvaloniaCompositionRoot>();

        var view = container.GetInstance<ILabelEditorView>("LabelEditorView");
        view.Should().BeOfType<AvaloniaLabelEditorView>();
    }

    [Fact]
    public void CompositionRoot_ResolvesTheAvaloniaFileDialogService_AsASingleton()
    {
        using var container = new ServiceContainer();
        container.RegisterFrom<DizUiAvaloniaCompositionRoot>();

        var service = container.GetInstance<IFileDialogService>();
        service.Should().BeOfType<AvaloniaFileDialogService>();
        container.GetInstance<IFileDialogService>().Should().BeSameAs(service);
        container.GetInstance<AvaloniaFileDialogService>().Should().BeSameAs(service);
    }
}

public class AvaloniaFileDialogServiceTests
{
    [Fact]
    public void ParseWinformsFilter_TranslatesTheRealImportFilterString()
    {
        // the exact filter string the label editor passes through the seam
        var types = AvaloniaFileDialogService.ParseWinformsFilter(
            "Comma Separated Value Files|*.csv|BSNES Symbols Map|*.cpu.sym|Text Files|*.txt|All Files|*.*");

        types.Should().HaveCount(4);
        types[0].Name.Should().Be("Comma Separated Value Files");
        types[0].Patterns.Should().Equal("*.csv");
        types[1].Patterns.Should().Equal("*.cpu.sym");
        types[3].Name.Should().Be("All Files");
        types[3].Patterns.Should().Equal("*.*");
    }

    [Fact]
    public void ParseWinformsFilter_HandlesMultiPatternAndMalformedInput()
    {
        AvaloniaFileDialogService.ParseWinformsFilter("Images|*.png;*.bmp")
            .Should().SatisfyRespectively(t => t.Patterns.Should().Equal("*.png", "*.bmp"));

        AvaloniaFileDialogService.ParseWinformsFilter(null).Should().BeEmpty();
        AvaloniaFileDialogService.ParseWinformsFilter("").Should().BeEmpty();
        // odd trailing part (name with no pattern) is ignored, not thrown on
        AvaloniaFileDialogService.ParseWinformsFilter("A|*.a|Dangling")
            .Should().HaveCount(1);
    }

    [Fact]
    public async System.Threading.Tasks.Task Prompts_WithNoOwnerWindow_ReturnNullLikeCancel()
    {
        var service = new AvaloniaFileDialogService();

        (await service.PromptOpenFileAsync("", "A|*.a")).Should().BeNull();
        (await service.PromptSaveFileAsync("", "A|*.a")).Should().BeNull();
        (await service.PromptSelectFolderAsync("")).Should().BeNull();
    }
}

public class DizAvaloniaAppThemeTests
{
    // THEMING DECISION (documented in DizAvaloniaApp + the plan doc): pinned to Light by
    // default to match the light WinForms UI; DIZ_AVALONIA_THEME reverses it.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("light")]
    [InlineData("nonsense")]
    public void Theme_DefaultsToLight(string? envValue) =>
        DizAvaloniaApp.ThemeVariantFrom(envValue).Should().Be(ThemeVariant.Light);

    [Fact]
    public void Theme_EnvVarSelectsDarkOrOs()
    {
        DizAvaloniaApp.ThemeVariantFrom("dark").Should().Be(ThemeVariant.Dark);
        DizAvaloniaApp.ThemeVariantFrom("DARK").Should().Be(ThemeVariant.Dark);
        DizAvaloniaApp.ThemeVariantFrom("os").Should().Be(ThemeVariant.Default);
        DizAvaloniaApp.ThemeVariantFrom("system").Should().Be(ThemeVariant.Default);
    }
}

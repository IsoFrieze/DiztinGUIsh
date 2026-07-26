using System;
using System.Linq;
using System.Reflection;
using Diz.Ui.ViewModels.Labels;
using Diz.Ui.ViewModels.MarkMany;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.MarkMany;

/// <summary>
/// The ViewModel-assembly rules (banned UI verbs, allowed references, no UI toolkit) are
/// already enforced across the whole assembly by LabelViewModelAssemblyRulesTests, which
/// sweeps every type it contains. These tests exist so that coverage cannot be lost silently:
/// they pin the MarkMany ViewModels to that same assembly, and re-check the vocabulary rule
/// scoped to them so a violation names the offending MarkMany member directly.
/// </summary>
public class MarkManyViewModelAssemblyRulesTests
{
    // assembled at runtime so a source grep for the banned words doesn't hit this test.
    private static readonly string[] BannedWords =
    [
        "Pro" + "mpt",
        "Sh" + "ow",
        "Dia" + "log",
        "F" + "orm",
    ];

    [Fact]
    public void MarkManyViewModelsLiveInTheSweptViewModelAssembly()
    {
        var sweptAssembly = typeof(LabelEditorViewModel).Assembly;

        typeof(MarkManyViewModel<>).Assembly.Should().BeSameAs(sweptAssembly);
        typeof(AddressRangeViewModel).Assembly.Should().BeSameAs(sweptAssembly);
        typeof(MarkManySettings).Assembly.Should().BeSameAs(sweptAssembly);
    }

    [Fact]
    public void NoMarkManyTypeOrMemberName_ContainsABannedUiVerb()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic |
                                 BindingFlags.Instance | BindingFlags.Static |
                                 BindingFlags.DeclaredOnly;

        var offenders = typeof(MarkManyViewModel<>).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(AddressRangeViewModel).Namespace)
            .SelectMany(t => t.GetMembers(all).Select(m => $"{t.Name}.{m.Name}").Append(t.Name))
            .Where(name => BannedWords.Any(w => name.Contains(w, StringComparison.Ordinal)))
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty(
            "ViewModels hold state and build commands; the host layer owns all user interaction");
    }
}

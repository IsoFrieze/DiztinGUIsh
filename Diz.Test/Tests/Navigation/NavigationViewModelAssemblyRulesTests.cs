using System;
using System.Linq;
using System.Reflection;
using Diz.Ui.ViewModels.Labels;
using Diz.Ui.ViewModels.Navigation;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.Navigation;

/// <summary>
/// The ViewModel-assembly rules (banned UI verbs, allowed references, no UI toolkit) are already
/// enforced across the whole assembly by LabelViewModelAssemblyRulesTests, which sweeps every type
/// it contains. These tests exist so that coverage cannot be lost silently: they pin the
/// navigation ViewModel to that same assembly, and re-check the vocabulary rule scoped to it so a
/// violation names the offending navigation member directly.
/// </summary>
public class NavigationViewModelAssemblyRulesTests
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
    public void NavigationViewModelsLiveInTheSweptViewModelAssembly()
    {
        var sweptAssembly = typeof(LabelEditorViewModel).Assembly;

        typeof(NavigationHistoryViewModel).Assembly.Should().BeSameAs(sweptAssembly);
        typeof(NavigationRequest).Assembly.Should().BeSameAs(sweptAssembly);
    }

    [Fact]
    public void NoNavigationTypeOrMemberName_ContainsABannedUiVerb()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic |
                                 BindingFlags.Instance | BindingFlags.Static |
                                 BindingFlags.DeclaredOnly;

        var offenders = typeof(NavigationHistoryViewModel).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(NavigationHistoryViewModel).Namespace)
            .SelectMany(t => t.GetMembers(all).Select(m => $"{t.Name}.{m.Name}").Append(t.Name))
            .Where(name => BannedWords.Any(w => name.Contains(w, StringComparison.Ordinal)))
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty(
            "ViewModels hold state and answer questions; the host layer owns all user interaction");
    }
}

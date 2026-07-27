using System;
using System.Linq;
using System.Reflection;
using Diz.Ui.ViewModels.HarshAutoStep;
using Diz.Ui.ViewModels.Labels;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.HarshAutoStep;

/// <summary>
/// The ViewModel-assembly rules (banned UI verbs, allowed references, no UI toolkit) are
/// already enforced across the whole assembly by LabelViewModelAssemblyRulesTests, which
/// sweeps every type it contains. These tests exist so that coverage cannot be lost silently:
/// they pin the HarshAutoStep ViewModel to that same assembly, and re-check the vocabulary rule
/// scoped to it so a violation names the offending HarshAutoStep member directly.
/// </summary>
public class HarshAutoStepViewModelAssemblyRulesTests
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
    public void HarshAutoStepViewModelLivesInTheSweptViewModelAssembly()
    {
        var sweptAssembly = typeof(LabelEditorViewModel).Assembly;

        typeof(HarshAutoStepViewModel).Assembly.Should().BeSameAs(sweptAssembly);
    }

    [Fact]
    public void NoHarshAutoStepTypeOrMemberName_ContainsABannedUiVerb()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic |
                                 BindingFlags.Instance | BindingFlags.Static |
                                 BindingFlags.DeclaredOnly;

        var offenders = typeof(HarshAutoStepViewModel).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(HarshAutoStepViewModel).Namespace)
            .SelectMany(t => t.GetMembers(all).Select(m => $"{t.Name}.{m.Name}").Append(t.Name))
            .Where(name => BannedWords.Any(w => name.Contains(w, StringComparison.Ordinal)))
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty(
            "ViewModels hold state and answer questions; the host layer owns all user interaction");
    }
}

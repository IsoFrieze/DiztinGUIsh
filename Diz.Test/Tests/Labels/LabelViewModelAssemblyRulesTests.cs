using System;
using System.Linq;
using System.Reflection;
using Diz.Ui.ViewModels.Labels;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.Labels;

/// <summary>
/// Enforces the two hard rules the new-ui plan places on the Diz.Ui.ViewModels assembly,
/// so a future edit that violates them fails a test instead of being discovered at the
/// third backend:
///
///  1. The banned-UI-verbs rule: no member may be named with the dialog-script vocabulary
///     ("Prompt", "Sho"+"w", "Dia"+"log", "For"+"m" -- spelled out here in halves so this
///     test file itself can be grepped cleanly). Paths come in as parameters; errors go out
///     via ErrorRaised; navigation via NavigationRequested.
///
///  2. The reference rule: Diz.Ui.ViewModels may reference Diz.Core, Diz.Core.Interfaces,
///     Diz.Import and NOTHING else -- no Diz.Controllers, no Diz.Cpu.65816, no UI toolkit.
/// </summary>
public class LabelViewModelAssemblyRulesTests
{
    private static Assembly VmAssembly => typeof(LabelEditorViewModel).Assembly;

    // assembled at runtime so a source grep for the banned words doesn't hit this test.
    private static readonly string[] BannedWords =
    [
        "Pro" + "mpt",
        "Sh" + "ow",
        "Dia" + "log",
        "F" + "orm",
    ];

    [Fact]
    public void NoTypeOrMemberName_ContainsABannedUiVerb()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic |
                                 BindingFlags.Instance | BindingFlags.Static |
                                 BindingFlags.DeclaredOnly;

        var offenders = VmAssembly.GetTypes()
            .SelectMany(t => t.GetMembers(all).Select(m => $"{t.Name}.{m.Name}")
                .Append(t.Name))
            .Where(name => BannedWords.Any(w => name.Contains(w, StringComparison.Ordinal)))
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty(
            "ViewModels hold state and raise events; the host layer owns all user interaction");
    }

    [Fact]
    public void VmAssembly_OnlyReferencesTheThreeAllowedDizAssemblies()
    {
        var dizRefs = VmAssembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("Diz.", StringComparison.Ordinal))
            .ToList();

        dizRefs.Should().BeSubsetOf(["Diz.Core", "Diz.Core.Interfaces", "Diz.Import"]);
    }

    [Fact]
    public void VmAssembly_ReferencesNoUiToolkit()
    {
        var refs = VmAssembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        refs.Should().NotContain(n =>
            n.Contains("Windows", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Avalonia", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Eto", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Drawing", StringComparison.OrdinalIgnoreCase));
    }
}

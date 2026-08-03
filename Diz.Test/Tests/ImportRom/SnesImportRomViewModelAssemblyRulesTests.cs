using System;
using System.Linq;
using System.Reflection;
using Diz.Ui.ViewModels.ImportRom;
using Diz.Ui.ViewModels.Labels;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.ImportRom;

/// <summary>
/// The ViewModel-assembly rules (banned UI verbs, allowed references, no UI toolkit) are
/// already enforced across the whole assembly by LabelViewModelAssemblyRulesTests, which
/// sweeps every type it contains. These tests exist so that coverage cannot be lost silently:
/// they pin the ROM-import ViewModel to that same assembly, and re-check the vocabulary rule
/// scoped to it so a violation names the offending import member directly.
///
/// The import screen is the one most likely to drift, because the SNES analysis it displays
/// lives in an assembly this one may not reference -- reaching for it directly is exactly the
/// shortcut these rules exist to catch.
/// </summary>
public class SnesImportRomViewModelAssemblyRulesTests
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
    public void SnesImportRomViewModelLivesInTheSweptViewModelAssembly()
    {
        var sweptAssembly = typeof(LabelEditorViewModel).Assembly;

        typeof(SnesImportRomViewModel).Assembly.Should().BeSameAs(sweptAssembly);
    }

    [Fact]
    public void NoImportRomTypeOrMemberName_ContainsABannedUiVerb()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic |
                                 BindingFlags.Instance | BindingFlags.Static |
                                 BindingFlags.DeclaredOnly;

        var offenders = typeof(SnesImportRomViewModel).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(SnesImportRomViewModel).Namespace)
            .SelectMany(t => t.GetMembers(all).Select(m => $"{t.Name}.{m.Name}").Append(t.Name))
            .Where(name => BannedWords.Any(w => name.Contains(w, StringComparison.Ordinal)))
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty(
            "ViewModels hold state and answer questions; the host layer owns all user interaction");
    }

    [Fact]
    public void NoImportRomTypeMentionsTheSnesCpuAssembly()
    {
        // the import screen exists to display what the SNES ROM analyser found, so it is the
        // most tempting place to reach straight into Diz.Cpu.65816. Everything analysis-derived
        // arrives here as plain data through the caller-supplied recompute delegate instead.
        var importRomTypes = typeof(SnesImportRomViewModel).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(SnesImportRomViewModel).Namespace)
            .ToList();

        importRomTypes.Should().NotBeEmpty();

        var offendingTypes = importRomTypes
            .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic |
                                          BindingFlags.Instance | BindingFlags.Static |
                                          BindingFlags.DeclaredOnly))
            .SelectMany(TypesReferencedBy)
            .Where(t => t.Assembly.GetName().Name?.StartsWith("Diz.Cpu", StringComparison.Ordinal) == true)
            .Select(t => t.FullName!)
            .Distinct()
            .ToList();

        offendingTypes.Should().BeEmpty();
    }

    private static Type[] TypesReferencedBy(MemberInfo member) => member switch
    {
        PropertyInfo p => [p.PropertyType],
        FieldInfo f => [f.FieldType],
        MethodInfo m => m.GetParameters().Select(x => x.ParameterType).Append(m.ReturnType).ToArray(),
        ConstructorInfo c => c.GetParameters().Select(x => x.ParameterType).ToArray(),
        _ => [],
    };
}

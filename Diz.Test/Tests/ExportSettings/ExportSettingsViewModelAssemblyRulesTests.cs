using System;
using System.Linq;
using System.Reflection;
using Diz.Ui.ViewModels.ExportSettings;
using Diz.Ui.ViewModels.Labels;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.ExportSettings;

/// <summary>
/// The ViewModel-assembly rules (banned UI verbs, allowed references, no UI toolkit) are already
/// enforced across the whole assembly by LabelViewModelAssemblyRulesTests, which sweeps every
/// type it contains. These tests exist so that coverage cannot be lost silently: they pin the
/// export-settings ViewModel to that same assembly, and re-check the vocabulary rule scoped to
/// it so a violation names the offending export-settings member directly.
///
/// The export-settings screen is the one that collides head-on with the vocabulary rule. Its
/// central field is a line-layout string, and the obvious name for it -- the one the settings
/// record itself uses -- contains one of the banned substrings. A property with that name would
/// fail the sweep twice over: once as the property, once as its compiler-generated backing
/// field. The name chosen instead is LineTemplate, and the point of the test below is that the
/// obvious name cannot quietly come back in a later edit.
/// </summary>
public class ExportSettingsViewModelAssemblyRulesTests
{
    // assembled at runtime so a source grep for the banned words doesn't hit this test.
    private static readonly string[] BannedWords =
    [
        "Pro" + "mpt",
        "Sh" + "ow",
        "Dia" + "log",
        "F" + "orm",
    ];

    private const BindingFlags AllMembers =
        BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static |
        BindingFlags.DeclaredOnly;

    private static Type[] ExportSettingsTypes => typeof(ExportSettingsViewModel).Assembly.GetTypes()
        .Where(t => t.Namespace == typeof(ExportSettingsViewModel).Namespace)
        .ToArray();

    [Fact]
    public void ExportSettingsViewModelLivesInTheSweptViewModelAssembly()
    {
        var sweptAssembly = typeof(LabelEditorViewModel).Assembly;

        typeof(ExportSettingsViewModel).Assembly.Should().BeSameAs(sweptAssembly);
    }

    [Fact]
    public void NoExportSettingsTypeOrMemberName_ContainsABannedUiVerb()
    {
        ExportSettingsTypes.Should().NotBeEmpty();

        var offenders = ExportSettingsTypes
            .SelectMany(t => t.GetMembers(AllMembers).Select(m => $"{t.Name}.{m.Name}").Append(t.Name))
            .Where(name => BannedWords.Any(w => name.Contains(w, StringComparison.Ordinal)))
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty(
            "ViewModels hold state and answer questions; the host layer owns all user interaction");
    }

    /// <summary>
    /// The line-layout string keeps the name that survives the vocabulary sweep. Reintroducing
    /// the settings record's own name for it -- alone or as part of a longer name -- fails here
    /// with a direct explanation, instead of failing the whole-assembly sweep with a
    /// backing-field name nobody recognises.
    /// </summary>
    [Fact]
    public void TheLineLayoutPropertyIsCalledLineTemplate_AndTheObviousNameStaysOut()
    {
        var lineTemplate = typeof(ExportSettingsViewModel).GetProperty(
            nameof(ExportSettingsViewModel.LineTemplate), BindingFlags.Public | BindingFlags.Instance);

        lineTemplate.Should().NotBeNull("the line-layout string is edited through LineTemplate");
        lineTemplate!.PropertyType.Should().Be<string>();
        lineTemplate.CanRead.Should().BeTrue();
        lineTemplate.CanWrite.Should().BeTrue();

        var bannedName = "F" + "ormat";

        var offenders = ExportSettingsTypes
            .SelectMany(t => t.GetMembers(AllMembers).Select(m => $"{t.Name}.{m.Name}").Append(t.Name))
            .Where(name => name.Contains(bannedName, StringComparison.Ordinal))
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty(
            "the settings record's name for this string contains a banned substring, so the " +
            "ViewModel calls it LineTemplate -- see this class's summary before renaming it back");
    }

    /// <summary>
    /// Nothing on this screen may reach the assembly that parses line templates and renders
    /// sample assembly. Both answers arrive as caller-supplied delegates; reaching for the real
    /// thing directly is exactly the shortcut these rules exist to catch, and it would not even
    /// be referenceable from here.
    /// </summary>
    [Fact]
    public void NoExportSettingsTypeMentionsTheAssemblyWriter()
    {
        var offendingTypes = ExportSettingsTypes
            .SelectMany(t => t.GetMembers(AllMembers))
            .SelectMany(TypesReferencedBy)
            .Where(t => t.Assembly.GetName().Name?.StartsWith("Diz.LogWriter", StringComparison.Ordinal) == true)
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

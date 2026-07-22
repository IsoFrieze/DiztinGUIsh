using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using Diz.Cpu._65816;
using Diz.Test.Utils;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Diz.Test.Tests.SerializationTests;

/// <summary>
/// Tests for the save-format 108 -> 109 change: labels gained an "Author" (freeform string) and a
/// "Confidence" (freeform level string) field, and ProjectSettings gained a "ConfidenceLevels"
/// vocabulary. All are optional/additive with safe defaults, so older files load fine (MigrationNoOp).
/// These tests pin the round-trip, the on-disk emit shape, and the vocabulary defaulting/preservation.
/// </summary>
public class LabelAuthorConfidence109Tests : ContainerFixture
{
    [Inject] private readonly IProjectXmlSerializer serializer = null!;
    [Inject] private readonly ISnesSampleProjectFactory sampleProjectFactory = null!;

    private readonly ITestOutputHelper output;

    public LabelAuthorConfidence109Tests(ITestOutputHelper output)
    {
        this.output = output;
    }

    private const int SomeLabelAddress = 0x808000 + 0x32; // "Test_Indices" in the sample data

    private Project CreateProjectWithAnnotatedLabel()
    {
        var project = (Project)sampleProjectFactory.Create();
        var label = project.Data.Labels.GetLabel(SomeLabelAddress);
        label.Should().NotBeNull("the sample data has a label at this address");
        label!.Author = "Alice";
        label.Confidence = "High";
        return project;
    }

    [Fact]
    public void LatestSaveFormatVersionIs109()
    {
        ProjectXmlSerializer.LatestSaveFormatVersion.Should().Be(109);
    }

    [Fact]
    public void SaveLoadRoundTripPreservesAuthorAndConfidence()
    {
        var saved = serializer.Save(CreateProjectWithAnnotatedLabel());

        var loaded = serializer.Load(saved).Root.Project;

        var label = loaded.Data.Labels.GetLabel(SomeLabelAddress);
        label.Should().NotBeNull();
        label!.Author.Should().Be("Alice");
        label.Confidence.Should().Be("High");
    }

    [Fact]
    public void AnnotatedLabelEmitsAuthorAndConfidenceAttributes()
    {
        var xml = Encoding.UTF8.GetString(serializer.Save(CreateProjectWithAnnotatedLabel()));
        output.WriteLine(FindLabelElement(xml, "Alice").ToString());

        var element = FindLabelElement(xml, "Alice");
        element.Attribute("By")?.Value.Should().Be("Alice");
        element.Attribute("Cf")?.Value.Should().Be("High");
    }

    [Fact]
    public void UnannotatedLabelOmitsAuthorAndConfidenceAttributes()
    {
        // a plain sample-data label leaves Author="" and Confidence="" (the defaults). The
        // serializer must NOT bloat every label with Author="" / Confidence="".
        var xml = Encoding.UTF8.GetString(serializer.Save((Project)sampleProjectFactory.Create()));

        // "Test_Indices" is a Name-only label (no Comment, no Author, no Confidence).
        var element = LabelElements(xml).Single(e => e.Attribute("Name")?.Value == "Test_Indices");
        output.WriteLine(element.ToString());

        element.Attribute("By").Should().BeNull("empty Author must not be emitted (EmitWhen set)");
        element.Attribute("Cf").Should().BeNull("default Confidence (\"\") must not be emitted (EmitWhen set)");

        // documents (does not change) existing behavior: the older Name/Comment members DO still
        // emit even when empty. we deliberately only gave the new fields emit-when-set treatment.
        element.Attribute("Comment")?.Value.Should().Be("", "empty Comment is still emitted, as before");
    }

    [Fact]
    public void ExcludedLabelAuthorsBlocklistPersistsWithProject()
    {
        // PHASE 2b: LogWriterSettings.ExcludedLabelAuthors is serialized with the project (as the
        // normalized ExcludedLabelAuthorsList string) so the export blocklist survives save/load.
        // Also confirms EXS doesn't choke on the settings (the interface-typed collection member is
        // [XmlIgnore]; the string form is what serializes).
        var project = (Project)sampleProjectFactory.Create();
        project.LogWriterSettings = project.LogWriterSettings with
        {
            ExcludedLabelAuthors = new[] { "Alice", "Bob" },
        };

        var loaded = serializer.Load(serializer.Save(project)).Root.Project;

        loaded.LogWriterSettings.ExcludedLabelAuthors
            .Should().BeEquivalentTo(new[] { "Alice", "Bob" });
    }

    [Fact]
    public void OldV108FileWithoutAuthorOrConfidenceLoadsToDefaults()
    {
        // shape a real v108 file: save at current version, drop the new attributes, roll SaveVersion back.
        var xml = Encoding.UTF8.GetString(serializer.Save(CreateProjectWithAnnotatedLabel()));

        xml.Should().Contain("By=\"Alice\"");
        xml = xml.Replace(" By=\"Alice\"", "").Replace(" Cf=\"High\"", "");
        xml = xml.Replace(
            $"SaveVersion=\"{ProjectXmlSerializer.LatestSaveFormatVersion}\"",
            "SaveVersion=\"108\"");

        var openResult = serializer.Load(Encoding.UTF8.GetBytes(xml));

        openResult.OpenResult.ProjectFileOriginalVersion.Should().Be(108);
        var label = openResult.Root.Project.Data.Labels.GetLabel(SomeLabelAddress);
        label.Should().NotBeNull();
        label!.Author.Should().Be("");
        label.Confidence.Should().Be("");
    }

    [Fact]
    public void LegacyEnumNameConfidence_LoadsAsString()
    {
        // "Medium" is the same token an older enum-based file stored; it loads to the string "Medium".
        var project = (Project)sampleProjectFactory.Create();
        var label = project.Data.Labels.GetLabel(SomeLabelAddress);
        label!.Confidence = "Medium";

        var xml = Encoding.UTF8.GetString(serializer.Save(project));
        xml.Should().Contain("Cf=\"Medium\"");

        var loaded = serializer.Load(Encoding.UTF8.GetBytes(xml)).Root.Project;
        loaded.Data.Labels.GetLabel(SomeLabelAddress)!.Confidence.Should().Be("Medium");
    }

    [Fact]
    public void OffVocabularyConfidenceValue_IsPreservedThroughRoundTrip()
    {
        // an off-list value (not in ConfidenceLevels) must survive save/load verbatim, never dropped.
        var project = (Project)sampleProjectFactory.Create();
        project.Data.Labels.GetLabel(SomeLabelAddress)!.Confidence = "Foobar";

        var loaded = serializer.Load(serializer.Save(project)).Root.Project;

        loaded.Data.Labels.GetLabel(SomeLabelAddress)!.Confidence.Should().Be("Foobar");
    }

    [Fact]
    public void ProjectWithoutConfidenceLevelsElement_LoadsDefaultVocabulary()
    {
        // shape an older file: save (which emits ConfidenceLevels), then strip the element and reload.
        // EXS constructs ProjectSettings (running the initializer = the 6 defaults) and only assigns
        // members present in the XML, so an absent ConfidenceLevels keeps the default vocabulary.
        var xml = Encoding.UTF8.GetString(serializer.Save((Project)sampleProjectFactory.Create()));
        var doc = XDocument.Parse(xml);
        doc.Descendants().Where(e => e.Name.LocalName == "ConfidenceLevels").Remove();

        var loaded = serializer.Load(Encoding.UTF8.GetBytes(doc.ToString())).Root.Project;

        loaded.ProjectSettings.ConfidenceLevels.Should().Equal(ProjectSettings.DefaultConfidenceLevels);
    }

    [Fact]
    public void CustomConfidenceLevels_RoundTrips()
    {
        var project = (Project)sampleProjectFactory.Create();
        project.ProjectSettings.ConfidenceLevels = new List<string> { "definitely_wrong", "hunch", "certain" };

        var loaded = serializer.Load(serializer.Save(project)).Root.Project;

        loaded.ProjectSettings.ConfidenceLevels.Should().Equal("definitely_wrong", "hunch", "certain");
    }

    private static XElement FindLabelElement(string xml, string author) =>
        LabelElements(xml).Single(e => e.Attribute("By")?.Value == author);

    // label elements are the <Value .../> entries inside the Labels dictionary.
    private static System.Collections.Generic.IEnumerable<XElement> LabelElements(string xml) =>
        XDocument.Parse(xml).Descendants()
            .Where(e => e.Name.LocalName == "Value" && e.Attribute("Name") != null);
}

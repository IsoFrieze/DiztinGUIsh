using System.Linq;
using System.Text;
using System.Xml.Linq;
using Diz.Core.export;
using Diz.Core.model;
using Diz.Core.serialization;
using Diz.Cpu._65816;
using Diz.Test.Utils;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.SerializationTests;

/// <summary>
/// The exported repo's directory tiers (hand-authored assets / extracted / build) are
/// per-project settings on LogWriterSettings rather than constants in the exporter, so a game
/// repo can name its folders whatever it names them and the generated build still points at
/// the right places. These pin that they persist with the project, and that a project file
/// written before they existed loads to the defaults instead of to empty strings -- which
/// would emit paths like "/assets/font.png" and break every build.
/// </summary>
public class AssetTierPathSettingsTests : ContainerFixture
{
    [Inject] private readonly IProjectXmlSerializer serializer = null!;
    [Inject] private readonly ISnesSampleProjectFactory sampleProjectFactory = null!;

    private static readonly string[] TierAttributes =
        ["AssetsDirPath", "ExtractedDirPath", "BuildDirPath"];

    [Fact]
    public void DefaultsAreTheStandardTierNames()
    {
        var settings = new LogWriterSettings();

        settings.AssetsDirPath.Should().Be("assets");
        settings.ExtractedDirPath.Should().Be("extracted");
        settings.BuildDirPath.Should().Be("build");

        // the generated tier: "export" reads as the same thing as "extracted" at a glance and
        // sorts next to it, which is exactly the confusion the tier names exist to prevent.
        settings.FileOrFolderOutPath.Should().Be("generated");
    }

    [Fact]
    public void CustomTierPathsRoundTripWithTheProject()
    {
        var project = (Project)sampleProjectFactory.Create();
        project.LogWriterSettings = project.LogWriterSettings with
        {
            AssetsDirPath = "art",
            ExtractedDirPath = "decoded",
            BuildDirPath = "out",
        };

        var loaded = serializer.Load(serializer.Save(project)).Root.Project;

        loaded.LogWriterSettings.AssetsDirPath.Should().Be("art");
        loaded.LogWriterSettings.ExtractedDirPath.Should().Be("decoded");
        loaded.LogWriterSettings.BuildDirPath.Should().Be("out");
    }

    [Fact]
    public void ProjectFileWithoutTierAttributesLoadsTheDefaults()
    {
        // shape a file written before these settings existed: save, then strip the attributes.
        var xml = Encoding.UTF8.GetString(serializer.Save((Project)sampleProjectFactory.Create()));
        var doc = XDocument.Parse(xml);

        var settingsElement = doc.Descendants()
            .Single(e => e.Name.LocalName == "LogWriterSettings");
        foreach (var name in TierAttributes)
        {
            settingsElement.Attribute(name).Should().NotBeNull("export must persist " + name);
            settingsElement.Attribute(name)!.Remove();
        }

        var loaded = serializer.Load(Encoding.UTF8.GetBytes(doc.ToString())).Root.Project;

        loaded.LogWriterSettings.AssetsDirPath.Should().Be("assets");
        loaded.LogWriterSettings.ExtractedDirPath.Should().Be("extracted");
        loaded.LogWriterSettings.BuildDirPath.Should().Be("build");
    }

    [Fact]
    public void AnExistingOutputPathIsNotOverwrittenByTheNewDefault()
    {
        // the generated-dir default changed; projects that already carry a value keep it.
        var project = (Project)sampleProjectFactory.Create();
        project.LogWriterSettings = project.LogWriterSettings with { FileOrFolderOutPath = "asm" };

        var loaded = serializer.Load(serializer.Save(project)).Root.Project;

        loaded.LogWriterSettings.FileOrFolderOutPath.Should().Be("asm");
    }
}

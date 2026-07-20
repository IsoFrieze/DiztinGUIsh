using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using Diz.Cpu._65816;
using Diz.Cpu._65816.import;
using Diz.Test.Utils;
using ExtendedXmlSerializer;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.SerializationTests;

/// <summary>
/// Tests for the save-format 107 -> 108 change: Region elements serialize with short XML attribute
/// names, and older files that still use the long names are rewritten on load.
/// </summary>
public class MigrationShortenRegionAttributes108Tests : ContainerFixture
{
    [Inject] private readonly IProjectXmlSerializer serializer = null!;
    [Inject] private readonly ISnesSampleProjectFactory sampleProjectFactory = null!;
    [Inject] private readonly IXmlSerializerFactory xmlSerializerFactory = null!;

    // every (long name, short name) pair, and a distinctive value for each so a mixed-up rename
    // can't accidentally pass.
    private static readonly (string LongName, string ShortName, string Value)[] Attributes =
    [
        ("StartSnesAddress", "S", "12713984"), // $C20000
        ("EndSnesAddress", "E", "12779519"),   // $C2FFFF
        ("RegionName", "Id", "bank_C2"),
        ("ContextToApply", "Ctx", "some_context"),
        ("Priority", "Pri", "7"),
        ("ExportSeparateFile", "SepFile", "true"),
        ("ExportType", "Type", "Asset"),
        ("AssetType", "AType", "gfx.snes.4bpp"),
        ("AssetVersion", "AVer", "3"),
        ("AssetName", "AName", "some_asset_name"),
        ("AssetOptions", "AOpts", "{'width':16}"),
    ];

    private static Region MakeFullyPopulatedRegion() => new()
    {
        StartSnesAddress = 12713984,
        EndSnesAddress = 12779519,
        RegionName = "bank_C2",
        ContextToApply = "some_context",
        Priority = 7,
        ExportSeparateFile = true,
        ExportType = RegionExportType.Asset,
        AssetType = "gfx.snes.4bpp",
        AssetVersion = "3",
        AssetName = "some_asset_name",
        AssetOptions = "{'width':16}",
    };

    private static void AssertFullyPopulated(IRegion region)
    {
        region.StartSnesAddress.Should().Be(12713984);
        region.EndSnesAddress.Should().Be(12779519);
        region.RegionName.Should().Be("bank_C2");
        region.ContextToApply.Should().Be("some_context");
        region.Priority.Should().Be(7);
        region.ExportSeparateFile.Should().BeTrue();
        region.ExportType.Should().Be(RegionExportType.Asset);
        region.AssetType.Should().Be("gfx.snes.4bpp");
        region.AssetVersion.Should().Be("3");
        region.AssetName.Should().Be("some_asset_name");
        region.AssetOptions.Should().Be("{'width':16}");
    }

    private Project CreateProjectWithFullyPopulatedRegion()
    {
        var project = (Project)sampleProjectFactory.Create();
        project.Data.Regions.Clear();
        project.Data.Regions.Add(MakeFullyPopulatedRegion());
        return project;
    }

    [Fact]
    public void AppliesToSaveVersion107()
    {
        new MigrationShortenRegionAttributes108().AppliesToSaveVersion.Should().Be(107);
    }

    [Fact]
    public void RenamesEveryLongAttributeAndLeavesOtherAttributesAlone()
    {
        // shaped like a real saved file: the Region element carries a namespace prefix, and the
        // serializer's own "exs:"-prefixed attributes sit alongside the ordinary members.
        XNamespace ns = "clr-namespace:Diz.Core.model.snes;assembly=Diz.Core";
        XNamespace exs = "https://extendedxmlserializer.github.io/v2";

        var regionElement = new XElement(ns + "Region", new XAttribute(exs + "type", "Region"));
        foreach (var (longName, _, value) in Attributes)
            regionElement.SetAttributeValue(longName, value);

        var document = new XDocument(new XElement("Root", regionElement));

        new MigrationShortenRegionAttributes108().OnLoadingPreProcessXml(document);

        foreach (var (longName, shortName, value) in Attributes)
        {
            regionElement.Attribute(longName).Should().BeNull($"'{longName}' must be renamed away");
            regionElement.Attribute(shortName)?.Value.Should().Be(value);
        }

        // not ours: must survive untouched
        regionElement.Attribute(exs + "type")!.Value.Should().Be("Region");
    }

    [Fact]
    public void SkipsAttributesThatArentPresent()
    {
        // a real v107 file omits any attribute left at its default value.
        var regionElement = new XElement("Region",
            new XAttribute("StartSnesAddress", "100"),
            new XAttribute("RegionName", "partial"));
        var document = new XDocument(new XElement("Root", regionElement));

        new MigrationShortenRegionAttributes108().OnLoadingPreProcessXml(document);

        regionElement.Attributes().Select(a => a.Name.LocalName)
            .Should().BeEquivalentTo(["S", "Id"]);
    }

    [Fact]
    public void SavedProjectUsesShortAttributeNamesOnly()
    {
        var xml = Encoding.UTF8.GetString(serializer.Save(CreateProjectWithFullyPopulatedRegion()));

        var regionElement = XDocument.Parse(xml)
            .Descendants().Single(element => element.Name.LocalName == "Region");

        foreach (var (longName, shortName, value) in Attributes)
        {
            regionElement.Attribute(longName).Should().BeNull($"'{longName}' is the old v107 name");
            regionElement.Attribute(shortName)?.Value.Should().Be(value);
        }
    }

    [Fact]
    public void SaveLoadRoundTripPreservesEveryRegionField()
    {
        var saved = serializer.Save(CreateProjectWithFullyPopulatedRegion());

        var loaded = serializer.Load(saved).Root.Project;

        loaded.Data.Regions.Should().ContainSingle();
        AssertFullyPopulated(loaded.Data.Regions[0]);
    }

    [Fact]
    public void OldV107FileWithLongAttributeNamesStillLoads()
    {
        var v107Xml = MakeV107ShapedXml();

        var openResult = serializer.Load(Encoding.UTF8.GetBytes(v107Xml));

        openResult.OpenResult.ProjectFileOriginalVersion.Should().Be(107);
        openResult.Root.Project.Data.Regions.Should().ContainSingle();
        AssertFullyPopulated(openResult.Root.Project.Data.Regions[0]);
    }

    [Fact]
    public void WithoutTheRenameTheDeserializerSilentlyDropsTheValues()
    {
        // the reason this migration has to exist at all: ExtendedXmlSerializer ignores XML
        // attributes it doesn't recognize rather than failing, so a v107 file read by a v108
        // deserializer would quietly produce a region with every field at its default.
        var config = xmlSerializerFactory.GetSerializer(null).Create();

        var shortNameXml = config.Serialize(MakeFullyPopulatedRegion());
        var longNameXml = ToV107AttributeNames(shortNameXml);

        var deserialized = config.Deserialize<Region>(longNameXml);

        deserialized.StartSnesAddress.Should().Be(0);
        deserialized.EndSnesAddress.Should().Be(0);
        deserialized.RegionName.Should().BeNull();
        deserialized.Priority.Should().Be(0);
        deserialized.ExportSeparateFile.Should().BeFalse();
    }

    // save at the current version, then rewrite the result back into what v107 looked like on disk
    private string MakeV107ShapedXml()
    {
        var xml = Encoding.UTF8.GetString(serializer.Save(CreateProjectWithFullyPopulatedRegion()));

        xml = ToV107AttributeNames(xml);

        var v107Xml = xml.Replace(
            $"SaveVersion=\"{ProjectXmlSerializer.LatestSaveFormatVersion}\"",
            "SaveVersion=\"107\"");

        v107Xml.Should().NotBe(xml, "the SaveVersion attribute should have been rewritten");
        return v107Xml;
    }

    private static string ToV107AttributeNames(string xml)
    {
        foreach (var (longName, shortName, value) in Attributes)
        {
            var shortAttribute = $"{shortName}=\"{value}\"";
            xml.Should().Contain(shortAttribute, "the current format writes short attribute names");
            xml = xml.Replace(shortAttribute, $"{longName}=\"{value}\"");
        }

        return xml;
    }
}

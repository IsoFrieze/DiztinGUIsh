using System.Xml.Linq;
using Diz.Core.serialization.xml_serializer;
using JetBrains.Annotations;

namespace Diz.Cpu._65816.import;

// Save format 107 -> 108.
//
// Pure rename: every Region element's attributes get short XML names (StartSnesAddress -> S,
// EndSnesAddress -> E, and so on). Regions are the most repeated element in a large project file,
// so the long names cost real disk space and load time for no benefit. The C# properties on
// Region are untouched -- only the serialized names change.
//
// This has to run on the raw XML: the deserializer silently ignores attributes it doesn't
// recognize, so a v107 file read without this rename would load regions with every field at its
// default value instead of failing loudly.
//
// Renames are namespace-agnostic (matched on local name) because saved files carry a namespace
// prefix on the element, and skip attributes that aren't present. Any attribute not in the map --
// e.g. the serializer's own "exs:"-prefixed type hints -- is left alone.
[UsedImplicitly]
public sealed class MigrationShortenRegionAttributes108 : IMigration
{
    public int AppliesToSaveVersion => 107;

    private static readonly (string OldName, string NewName)[] AttributeRenames =
    [
        ("StartSnesAddress", "S"),
        ("EndSnesAddress", "E"),
        ("RegionName", "Id"),
        ("ContextToApply", "Ctx"),
        ("Priority", "Pri"),
        ("ExportSeparateFile", "SepFile"),
        ("ExportType", "Type"),
        ("AssetType", "AType"),
        ("AssetVersion", "AVer"),
        ("AssetName", "AName"),
        ("AssetOptions", "AOpts"),
    ];

    public void OnLoadingPreProcessXml(XDocument document)
    {
        if (document == null)
            return;

        var regionElements = document
            .Descendants()
            .Where(element => element.Name.LocalName == "Region")
            .ToList();

        foreach (var regionElement in regionElements)
        {
            RenameAttributes(regionElement);
        }
    }

    private static void RenameAttributes(XElement regionElement)
    {
        foreach (var (oldName, newName) in AttributeRenames)
        {
            // only ever match un-prefixed (no-namespace) attributes: that's what the serializer
            // writes for ordinary members.
            var oldAttribute = regionElement.Attribute(oldName);
            if (oldAttribute == null)
                continue;

            oldAttribute.Remove();
            regionElement.SetAttributeValue(newName, oldAttribute.Value);
        }
    }
}

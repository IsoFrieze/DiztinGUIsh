#nullable enable
using System.ComponentModel;
using Diz.Core.serialization.xml_serializer;

namespace Diz.Core.model;

[TypeConverter(typeof(ExpandableObjectConverter))]
public class ProjectSettings
{
    // any public properties here will be shown in the Tools -> Preferences menu
    
    [Category("Project save format settings")]
    [DisplayName("Project save format options")]
    [Description("Advanced options for tweaking how your .diz or .dizraw file will be saved (most people never need to mess with this). Takes effect when you save the project.")]
    public RomBytesOutputFormatSettings RomBytesOutputFormatSettings { get; set; } = new();

    [Category("BSNES Import Options")]
    [DisplayName("Usage map / tracelog import only changes unmarked Rom Bytes")]
    [Description(
        "If true, usage map and tracelog imports/capture won't change anything you already marked. If False, your data will be overwritten from BSNES's usage map. " +
        "(useful if you manually marked a lot of instructions incorrectly and they're desync'd. BSNES's marking is really good but not foolproof, " +
        "and it has been known to get M/X flags incorrect rarely). Safest option is to leave this OFF")]
    public bool BsnesUsageMapImportOnlyChangedUnmarked { get; set; } = true;

    public override string ToString() => "";
}
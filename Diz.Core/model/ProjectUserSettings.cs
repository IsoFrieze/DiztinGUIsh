#nullable enable
using System.ComponentModel;

namespace Diz.Core.model;

public class ProjectUserSettings
{
    // if false (default) show SNES memory addresses in the grid under the "PC" column.
    // if true, show ROM offsets instead. (useful as default on NES)
    [Category("GUI")]
    [DisplayName("Main Grid Show ROM Offsets (not SNES addresses)")]
    [Description("Toggle main grid between showing SNES addresses vs ROM offsets. NES games probably want ROM offsets always")]
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
    public bool DisplayOffsetsInGrid { get; set; } = false;
    
    // ----- TODO ---- hide everything below here from the GUI with some kind of tag
        
    // current view offset
    // (where are you scrolled in the UI in the main table.
    //  changes OFTEN while using the app. save this to pick up your place when re-opening Diz)
    [Browsable(false)] public int CurrentViewOffset { get; set; }
    
    // attached ROM filename.
    // the main project file will store the checksums/etc that this must match, or it'll ask for another rom file.
    // this is important to keep locally only because we don't want any stored path or ROM filenames to leak into
    // public git repos, potentially exposing people's sensitive user info/etc.
    [Browsable(false)] public string AttachedRomFilename { get; set; } = "";
}
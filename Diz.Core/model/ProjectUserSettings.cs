#nullable enable
using System.ComponentModel;

namespace Diz.Core.model;

// these "User settings" are saved alongside each project BUT are intended to be user-specific and not shared with all users
// i.e. unlike the main project file, the user shoudn't check their project settings (in a .dizprefs file) into git, it should be gitignore'd
// (NOTE: there's a different settings file for global Application-specific stuff, and for stuff saved WITH the project intended to be shared)
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
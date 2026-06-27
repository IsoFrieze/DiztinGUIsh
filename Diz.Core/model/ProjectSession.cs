#nullable enable
using System.ComponentModel;
using System.IO;
using Diz.Core.util;

namespace Diz.Core.model;

/// <summary>
/// temporary data stored about the current project "session"
/// i.e. mostly stuff we don't want serialized to XML that may change
/// from run to run of the app (like working dir,etc)
/// stuff in here might want to be saved somewhere else. 
/// </summary>
public class ProjectSession(IProjectWithSession project, string projectFileName) : IProjectSession
{
    // cache of the last filename this project was saved as.
    // (This field may require some rework for GUI multi-project support)
    public string ProjectFileName
    {
        get => projectFileName;
        set => this.SetField(PropertyChanged, ref projectFileName, value);
    }
        
    public bool UnsavedChanges
    {
        get => unsavedChanges;
        set => this.SetField(PropertyChanged, ref unsavedChanges, value);
    }
        
    public string? ProjectDirectory => Util.GetDirNameOrEmpty(projectFileName);
    public string AttachedRomFileFullPath => Path.Combine(ProjectDirectory ?? "", project.AttachedRomFilename);

    private string projectFileName = projectFileName;
    private bool unsavedChanges;

    public event PropertyChangedEventHandler? PropertyChanged;
}
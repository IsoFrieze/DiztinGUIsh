#nullable enable

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Xml.Serialization;
using Diz.Core.export;
using Diz.Core.Interfaces;
using Diz.Core.model.snes;
using Diz.Core.serialization.xml_serializer;
using Diz.Core.util;

namespace Diz.Core.model;

public interface IProject : 
    INotifyPropertyChanged, 
    IProjectWithSession,
    ISnesCachedVerificationInfo // see if we can get rid of this eventually. only needed for now for serialization
{
    public Data Data { get; }
}

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

public class ProjectUserSettings
{
    // these settings are saved per-project BUT are intended to be user-specific and not shared with all users
    // i.e. unlike the main project file, the user won't check this into git
    // (NOTE: there's a different settings file for global Application-specific stuff, and for stuff saved WITH the project intended to be shared) 
        
    // current view offset (where are you scrolled in the UI)
    public int CurrentViewOffset { get; set; }
    
    // attached ROM filename.
    // the main project file will store the checksums/etc that this must match, or it'll ask for another rom file.
    // this is important to keep locally only because we don't want any stored path or ROM filenames to leak into
    // public git repos, potentially exposing people's sensitive user info/etc.
    public string AttachedRomFilename { get; set; } = "";
}

public class Project : IProject
{
    // Any public properties will be automatically serialized to XML unless noted.
    // They will require a get AND set.
    // Order is important.

    [XmlIgnore]
    public string ProjectFileName
    {
        get => projectFileName;
        set => this.SetField(PropertyChanged, ref projectFileName, value);
    }
    
    [XmlIgnore]
    public string AttachedRomFilename
    {
        get => ProjectUserSettings.AttachedRomFilename ?? "";
        set 
        {
            if (ProjectUserSettings.AttachedRomFilename != value)
            {
                if (Session != null) Session.UnsavedChanges = true;
            }

            // below is same as: this.SetField(PropertyChanged, ProjectUserSettings.AttachedFromFilename, value);
            
            if (NotifyPropertyChangedExtensions.FieldIsEqual(ProjectUserSettings.AttachedRomFilename, value)) 
                return;
            
            ProjectUserSettings.AttachedRomFilename = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AttachedRomFilename)));
        }
    }

    // safety checks:
    // The rom "Game name" and "Checksum" are copies of certain bytes from the ROM which
    // get stored with the project file.  REMEMBER: We don't store the actual ROM bytes
    // in the project file, so when we load a project, we must also open the same ROM and load its
    // bytes in the project.
    //
    // Project = Metadata
    // Rom = The real data
    //
    // If we load a ROM, and then its checksum and name don't match what we have stored in the XML,
    // then we have an issue (i.e. not the same ROM, or it was modified, or missing, etc).
    // The user must either provide a ROM matching these criteria, or abort loading the project.
    public string InternalRomGameName
    {
        get => internalRomGameName;
        set => this.SetField(PropertyChanged, ref internalRomGameName, value);
    }

    public uint InternalCheckSum
    {
        get => internalCheckSum;
        set => this.SetField(PropertyChanged, ref internalCheckSum, value);
    }

    public LogWriterSettings LogWriterSettings
    {
        get => logWriterSettings with
        {
            BaseOutputPath = Session?.ProjectDirectory ?? "",
        };
        set => this.SetField(PropertyChanged, ref logWriterSettings, value);
    }

    // settings saved WITH the project file.
    // for things that are expected to get checked into git and shared with all project users
    public ProjectSettings ProjectSettings
    {
        get => projectSettings;
        set => this.SetField(PropertyChanged, ref projectSettings, value);
    }

    // settings saved WITHOUT the project file.
    // for things that are not expected to get checked into git and shared with all project users
    [XmlIgnore]
    public ProjectUserSettings ProjectUserSettings
    {
        get => projectUserSettings;
        set => this.SetField(PropertyChanged, ref projectUserSettings, value);
    }

    // needs to come last for serialization. this is the heart of the app, the actual
    // data from the ROM and metadata we add/create.
    public Data Data
    {
        get => data!;
        set => this.SetField(PropertyChanged, ref data, value);
    }
        
    /// <summary>
    /// Temporary session-specific data associated with this Project
    /// </summary>
    /// <remarks>
    /// Never saved with XML, this is all temporary data that exists as long as its open
    /// in the app, and no longer.
    /// </remarks>
    [XmlIgnore]
    public IProjectSession? Session
    {
        get => session;
        set
        {
            var previouslyUnsaved = false;

            if (session != null)
            {
                session.PropertyChanged -= SessionOnPropertyChanged;
                previouslyUnsaved = session.UnsavedChanges;
            }

            this.SetField(PropertyChanged, ref session, value);

            if (session == null) 
                return;
                
            session.PropertyChanged += SessionOnPropertyChanged;
            session.UnsavedChanges = previouslyUnsaved;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Project()
    {
        logWriterSettings = new LogWriterSettings();
        PropertyChanged += ProjectPropertyChanged;
    }
        
    private string GetAbsolutePathToRomFile()
    {
        var pathToProjectFile = GetFullBasePathToRomFile(session?.ProjectFileName ?? "");
        var attachedRomFileNoPath = Path.GetFileName(AttachedRomFilename);
        return Path.Combine(pathToProjectFile, attachedRomFileNoPath);
    }
        
    private string? GetFullBasePathToRomFile(string projFileName)
    {
        var projDir = session?.ProjectDirectory ?? "";
        if (projDir != "")
            return projDir;
                
        return projFileName != "" ? Util.GetDirNameOrEmpty(projFileName) : "";
    }
        
    private void SessionOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IProjectSession.ProjectFileName):
                AttachedRomFilename = GetAbsolutePathToRomFile();
                break;
        }
            
        PropertyChanged?.Invoke(sender, e);
    }

    private void ProjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IProjectSession.UnsavedChanges) && Session != null)
            Session.UnsavedChanges = true;

        // try not to get too fancy in this method.
    }

    // don't access these backing fields directly, instead, always use the properties
    private string projectFileName = "";
    private string internalRomGameName = "";
    private uint internalCheckSum;
    private Data? data; // TODO: change to IData
    private LogWriterSettings logWriterSettings;
    private IProjectSession? session;
    private ProjectSettings projectSettings = new();
    private ProjectUserSettings projectUserSettings = new();

    #region Equality
    protected bool Equals(Project other)
    {
        return ProjectFileName == other.ProjectFileName && AttachedRomFilename == other.AttachedRomFilename && Equals(Data, other.Data) && InternalRomGameName == other.InternalRomGameName && InternalCheckSum == other.InternalCheckSum;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Project) obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (ProjectFileName != null ? ProjectFileName.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (AttachedRomFilename != null ? AttachedRomFilename.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Data != null ? Data.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (InternalRomGameName != null ? InternalRomGameName.GetHashCode() : 0);
            hashCode = (int) ((hashCode * 397) ^ InternalCheckSum);
            return hashCode;
        }
    }
    #endregion
}
    
public interface IProjectWithSession {
    IProjectSession? Session { get; set; }
    string AttachedRomFilename { get; }
}
    
public interface IProjectSession : INotifyPropertyChanged
{
    public string? ProjectDirectory { get; }
    string AttachedRomFileFullPath { get; }
    string ProjectFileName { get; set; }
    bool UnsavedChanges { get; set; }
}
    
    
/// <summary>
/// temporary data stored about the current project "session"
/// i.e. mostly stuff we don't want serialized to XML that may change
/// from run to run of the app (like working dir,etc)
/// stuff in here might want to be saved somewhere else. 
/// </summary>
public class ProjectSession : IProjectSession
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
        
    public string? ProjectDirectory =>
        Util.GetDirNameOrEmpty(projectFileName);
        
    public string AttachedRomFileFullPath =>
        Path.Combine(ProjectDirectory, project.AttachedRomFilename);

    private readonly IProjectWithSession project;
        
    private string projectFileName;
    private bool unsavedChanges;

    public ProjectSession(IProjectWithSession project, string projectFileName)
    {
        this.projectFileName = projectFileName;
        this.project = project;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
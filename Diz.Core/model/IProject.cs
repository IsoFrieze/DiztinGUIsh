#nullable enable
using System.ComponentModel;
using Diz.Core.Interfaces;
using Diz.Core.model.snes;

namespace Diz.Core.model;

// represents a particular system's architecture (like main CPU on a SNES vs SPC700 on a SNES vs NES vs Genesis, etc)
public interface IArchitecture;

public interface IProjectSettings;

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

public interface IProject : 
    INotifyPropertyChanged, 
    IProjectWithSession,
    // ISnesCachedVerificationInfo // see if we can get rid of this eventually. only needed for now for serialization
{
    public Data Data { get; }
}
using Diz.Core;
using Diz.Core.model;
using Diz.Core.serialization;

namespace Diz.PowerShell;

public class ProjectFileReader : IProjectFileOpener
{
    private readonly IProjectFileManager projectFileManager;
    
    private string? filename;

    public ProjectFileReader(IProjectFileManager projectFileManager) => 
        this.projectFileManager = projectFileManager;

    public void SetOpenFilename(string projectFilename) => 
        filename = projectFilename;

    public Project? Read()
    {
        if (string.IsNullOrEmpty(filename))
            return null;
        
        var openResult = projectFileManager.Open(filename);
        return openResult?.Root?.Project ?? null;
    }
}
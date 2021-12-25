#nullable enable

using System.IO;

namespace Diz.Core.util;

public interface IFilesystemService
{
    bool DirectoryExists(string? outputDirectoryName);
    void CreateDirectory(string name);
}

/// <summary>
/// Wrapper for OS-level path/file manipulation.
/// Mostly for ease of unit testing
/// </summary>
public class FilesystemService : IFilesystemService
{
    public virtual bool DirectoryExists(string? outputDirectoryName) => 
        Directory.Exists(outputDirectoryName);

    public virtual void CreateDirectory(string name) => 
        Directory.CreateDirectory(name);
}
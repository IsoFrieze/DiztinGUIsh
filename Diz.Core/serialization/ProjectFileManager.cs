#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using Diz.Core.model;
using Diz.Core.serialization.xml_serializer;
using Diz.Core.util;

namespace Diz.Core.serialization;

public interface IProjectSerializer
{
    ProjectOpenResult Load(byte[] rawBytes);
    byte[] Save(Project project);
    IMigrationRunner MigrationRunner { get; }
}

public interface IProjectXmlSerializer : IProjectSerializer
{
    public delegate void SerializeEvent(IProjectXmlSerializer projectXmlSerializer, ProjectSerializedRoot rootElement);
    public event SerializeEvent BeforeSerialize;
    public event SerializeEvent AfterDeserialize;
}

public interface IAddRomDataCommand
{
    public bool ShouldProjectCartTitleMatchRomBytes { get; set; }
    public ProjectSerializedRoot? Root { get; set; }
    public Func<string, string>? GetNextRomFileToTry { get; set; }
    public IMigrationRunner? MigrationRunner { get; set; }

    public void TryReadAttachedProjectRom();
}
    
public class ProjectFileManager : IProjectFileManager
{
    private readonly Func<IProjectXmlSerializer> projectXmlSerializerCreate;
    private readonly Func<IAddRomDataCommand> addRomDataCommandCreate;

    public ProjectFileManager(Func<IProjectXmlSerializer> projectXmlSerializerCreate, Func<IAddRomDataCommand> addRomDataCommandCreate)
    {
        this.projectXmlSerializerCreate = projectXmlSerializerCreate;
        this.addRomDataCommandCreate = addRomDataCommandCreate;
    }

    // TODO: remove this and just do it by passing different stuff in the constructor, or use a decorator
    protected virtual IProjectXmlSerializer CreateProjectXmlSerializer() =>
        projectXmlSerializerCreate();

    public Func<string, string>? RomPromptFn { get; set; } = null;

    public ProjectOpenResult Open(string filename)
    {
        Trace.WriteLine("Opening Project START");

        var (serializer, openResult) = Deserialize(filename);
        VerifyIntegrityDeserialized(openResult.Root);
        PostSerialize(filename, openResult.Root, serializer);

        Trace.WriteLine("Opening Project END");
        return openResult;
    }

    private void PostSerialize(string filename, ProjectSerializedRoot xmlProjectSerializedRoot, IProjectSerializer serializer)
    {
        xmlProjectSerializedRoot.Project.ProjectFileName = filename;

        xmlProjectSerializedRoot.Project.Session = new ProjectSession(xmlProjectSerializedRoot.Project, filename);

        // at this stage, 'Data' is populated with everything EXCEPT the actual ROM bytes.
        // It would be easy to store the ROM bytes in the save file, but, for copyright reasons,
        // we leave it out.
        //
        // So now, with all our metadata loaded successfully, we now open the .smc file on disk
        // and marry the original rom's bytes with all of our metadata loaded from the project file.
        var romAddCmd = addRomDataCommandCreate();
        Debug.Assert(romAddCmd != null);
        
        romAddCmd.Root = xmlProjectSerializedRoot;
        romAddCmd.GetNextRomFileToTry = RomPromptFn;
        romAddCmd.MigrationRunner = serializer.MigrationRunner;

        romAddCmd.TryReadAttachedProjectRom();
    }

    private static void VerifyIntegrityDeserialized(ProjectSerializedRoot xmlProjectSerializedRoot)
    {
        var data = xmlProjectSerializedRoot.Project.Data;
        Debug.Assert(data.Labels != null && data.Comments != null);
        Debug.Assert(data.RomBytes is { Count: > 0 });
    }

    private (IProjectSerializer serializer, ProjectOpenResult projectOpenResult) Deserialize(string filename)
    {
        var projectFileBytes = ReadProjectFileBytes(filename);
        var serializer = GetSerializerForFormat(projectFileBytes);
        var projectOpenResult = DeserializeWith(serializer, projectFileBytes);
        return (serializer, projectOpenResult);
    }

    private byte[] ReadProjectFileBytes(string filename)
    {
        var projectFileBytes = ReadAllBytes(filename);

        if (IsLikelyCompressed(filename))
            projectFileBytes = Util.TryUnzip(projectFileBytes);
            
        return projectFileBytes;
    }
        
    public static bool IsBinaryFileFormat(byte[] data)
    {
        try {
            for (var i = 0; i < ProjectSerializer.DizWatermark.Length; i++) {
                if (data[i + 1] != (byte) ProjectSerializer.DizWatermark[i])
                    return false;
            }
            return true;
        } 
        catch (Exception) 
        {
            return false;
        }
    }

    private IProjectSerializer GetSerializerForFormat(byte[] data)
    {
        if (IsBinaryFileFormat(data))
        {
#if ENABLE_LEGACY_BINARY_SERIALIZER
                return new BinarySerializer();
#else
            throw new InvalidDataException(
                "Legacy binary serializer is no longer supported (use an older version of Diz to update your save file");
#endif
        }

        return CreateProjectXmlSerializer();
    }

    private static bool IsLikelyCompressed(string filename) => 
        !Path.GetExtension(filename).Equals(".dizraw", StringComparison.InvariantCultureIgnoreCase);

    public string? Save(Project project, string filename)
    {
        try
        {
            // Everything saves in XML format from here on out.
            // Binary format is deprecated.
            Save(project, filename, CreateProjectXmlSerializer());
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        return null;
    }

    private void Save(Project project, string filename, IProjectSerializer serializer)
    {
        var data = DoSave(project, filename, serializer);

        WriteBytes(filename, data);
        project.ProjectFileName = filename;
            
        // always do this last
        if (project.Session != null) 
            project.Session.UnsavedChanges = false;
    }

    private byte[] DoSave(Project project, string filename, IProjectSerializer serializer)
    {
        var data = SerializeWith(project, serializer);

        if (IsLikelyCompressed(filename))
            data = Util.TryZip(data);

        return data;
    }

    #region Hooks
    protected virtual ProjectOpenResult DeserializeWith(IProjectSerializer serializer, byte[] rawBytes) => 
        serializer.Load(rawBytes);

    protected virtual byte[] SerializeWith(Project project, IProjectSerializer serializer) => 
        serializer.Save(project);

    protected virtual byte[] ReadAllBytes(string filename) => 
        File.ReadAllBytes(filename);

    protected virtual void WriteBytes(string filename, byte[] data) => 
        File.WriteAllBytes(filename, data);

    #endregion
}

public interface IProjectFileManager
{
    /// <summary>
    /// A function that the project file manager calls if it needs to ask the user
    /// to locate a ROM that matches a project file. If null, the check is skipped.
    /// </summary>
    Func<string, string>? RomPromptFn { get; set; }
    
    /// <summary>
    /// Open a file from disk
    /// </summary>
    /// <param name="filename"></param>
    /// <returns>Info about how it went, plus the data</returns>
    ProjectOpenResult Open(string filename);
    
    /// <summary>
    /// Save a project to disk
    /// </summary>
    /// <param name="project"></param>
    /// <param name="filename"></param>
    /// <returns>error msg. null if succeeded, otherwise a string with the error messages of why it can't open.</returns>
    string? Save(Project project, string filename);
}
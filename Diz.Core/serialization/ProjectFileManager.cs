﻿#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    public delegate void SerializeEvent(IProjectXmlSerializer projectXmlSerializer, ProjectXmlSerializer.Root rootElement);
    public event SerializeEvent BeforeSerialize;
    public event SerializeEvent AfterDeserialize;
}

public interface IAddRomDataCommand
{
    public bool ShouldProjectCartTitleMatchRomBytes { get; set; }
    public ProjectXmlSerializer.Root? Root { get; set; }
    public Func<string, string>? GetNextRomFileToTry { get; set; }
    public IMigrationRunner? MigrationRunner { get; set; }

    public void TryReadAttachedProjectRom();
}

public interface IFileByteReader
{
    byte[] ReadAllBytes(string filename);
}

public interface IFileByteWriter
{
    void WriteBytes(string filename, byte[] data);
}

public interface IFileByteProvider : IFileByteReader, IFileByteWriter
{
    
}

public class ProjectFileManager : IProjectFileManager
{
    private readonly Func<IProjectXmlSerializer> projectXmlSerializerCreate;
    private readonly Func<IAddRomDataCommand> addRomDataCommandCreate;
    private readonly Func<string, IFileByteProvider> fileByteProviderFactory;

    public ProjectFileManager(
        Func<IProjectXmlSerializer> projectXmlSerializerCreate, 
        Func<IAddRomDataCommand> addRomDataCommandCreate, 
        Func<string, IFileByteProvider> fileByteProviderFactory
        ) {
        this.projectXmlSerializerCreate = projectXmlSerializerCreate;
        this.addRomDataCommandCreate = addRomDataCommandCreate;
        this.fileByteProviderFactory = fileByteProviderFactory;
    }

    // TODO: remove this and just do it by passing different stuff in the constructor, or use a decorator

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

    private void PostSerialize(string filename, ProjectXmlSerializer.Root xmlProjectSerializedRoot, IProjectSerializer serializer)
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

    private static void VerifyIntegrityDeserialized(ProjectXmlSerializer.Root xmlProjectSerializedRoot)
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
        var fileByteIo = CreateFileBytesProvider(filename);

        var projectFileBytes = fileByteIo.ReadAllBytes(filename);

        if (IsLikelyCompressed(filename))
            projectFileBytes = Util.TryUnzip(projectFileBytes);
            
        return projectFileBytes;
    }
    
    private static bool IsDirectoryBasedProject(string filename) => 
        Path.GetExtension(filename).Equals(".dizdir", StringComparison.InvariantCultureIgnoreCase);

    private IFileByteProvider CreateFileBytesProvider(string filename)
    {
        var projectSaveType = IsDirectoryBasedProject(filename) ? "Multiple" : "Single";
        return fileByteProviderFactory(projectSaveType);
    }

    public static bool IsBinaryFileFormat(byte[] data)
    {
        try {
            return !ProjectSerializer.DizWatermark.Where((t, i) => data[i + 1] != (byte)t).Any();
        } catch (Exception) {
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

        return projectXmlSerializerCreate();
    }

    private static readonly string[] UncompressedExtensions = {
        ".dizraw", 
        ".dizdir"
    };

    private static bool IsLikelyCompressed(string filename) => 
        !UncompressedExtensions.Any(ext => ext.Equals(Path.GetExtension(filename), StringComparison.InvariantCultureIgnoreCase));

    public string? Save(Project project, string filename)
    {
        try
        {
            // Everything saves in XML format from here on out.
            // Binary format is deprecated.
            Save(project, filename, projectXmlSerializerCreate());
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

        var fileByteIo = CreateFileBytesProvider(filename);
        fileByteIo.WriteBytes(filename, data);
        
        project.ProjectFileName = filename;
        
        if (project.Session == null) 
            return;
        project.Session.ProjectFileName = project.ProjectFileName;
        
        // only do this at the VERY END
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
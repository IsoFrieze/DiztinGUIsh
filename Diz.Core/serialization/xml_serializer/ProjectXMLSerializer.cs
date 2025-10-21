using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Diz.Core.Interfaces;
using Diz.Core.model;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using JetBrains.Annotations;

namespace Diz.Core.serialization.xml_serializer;

public class ProjectOpenResult
{
    public class Result
    {
        public List<string> Warnings { get; set; } = new();
        
        /// <summary>
        /// The project version# of this file on disk before loaded it.
        /// </summary>
        public int ProjectFileOriginalVersion { get; set; } = -1;

        /// <summary>
        /// The project version# of the data after we loaded it from disk
        /// It may be different from ProjectFileOriginalVersion if we upgraded it.
        /// </summary>
        public int ProjectFileUpgradedToVersion { get; set; } = -1;
    }

    /// <summary>
    /// Contains metadata from the operation of opening the file (not the file contents).
    /// Contains things like non-fatal warnings, etc, anything we might want communicate to the user about the act
    /// Of Loading this project from disk.
    /// </summary>
    public Result OpenResult { get; } = new();
    
    /// <summary>
    /// The actual data for the root object that gets deserialized from the project file. Contains the Project
    /// </summary>
    public ProjectXmlSerializer.Root Root { get; set; }
}

public class ProjectXmlSerializer : ProjectSerializer, IProjectXmlSerializer
{
    public const int FirstSaveFormatVersion = 100;   // NEVER CHANGE THIS ONE.

    // increment this if you change the XML file format
    // history:
    // - 100: original XML version for Diz 2.0, from <= 2020. [see notes below about earlier now-removed binary save format]
    // - 101: no structure changes but, japanese chars in SNES header cartridge title were being saved
    //        incorrectly, so, allow project XMLs to load IF we can fix up the bad data.
    // - 102: added comments support to the special RomBytes data section.
    //        bumped RomBytes data format from v200 (initial) to v201.
    // - 103: added "context" fields to labels, added regions (no migrations required)
    // - 104: added "ExportSeparateFile" field to regions (no migrations required)
    public const int LatestSaveFormatVersion = 104;  //  REMEMBER: IF YOU CHANGE THIS YOU MUST ADD A NEW IMigration ENTRY IN RegisterMigrations()
    
    // About older project save formats from ancient Diz 1.0:
    // The older binary savefile format BEFORE v100 in Diz 1.0 is removed, and modern Diz can't open them anymore.
    // To deal with opening one of these ancient saves:
    // - grab one of the old Diz 2.0 versions from the Github Releases page
    //   [around 2021 era likely works. try Diz v2.1.1.2 "ish" - like https://github.com/IsoFrieze/DiztinGUIsh/releases/tag/v2.2.1.2]
    //   that supported both the old Binary Serialization format, and .diz XML file formats >v100.
    // - open old project file, re-save as XML-based file format.
    // - open with latest Diz and it should be able to read any XML-based file format.
    //
    // it is unlikely anyone using Diz these days has one of these older file formats that
    // hasn't already been converted, so shouldn't be a big deal.
    // reach out in Discord if you need help with this.
    
    // changes as we run the migrations
    public int CurrentSaveFormatVersion { get; } = LatestSaveFormatVersion;

    // update this if we are dropping support for ancient save formats.
    public const int EarliestSupportedSaveFormatVersion = FirstSaveFormatVersion;
    
    [CanBeNull] public event IProjectXmlSerializer.SerializeEvent BeforeSerialize;
    [CanBeNull] public event IProjectXmlSerializer.SerializeEvent AfterDeserialize;
    
    
    public class Root
    {
        // XML serializer specific metadata, top-level deserializer.
        // This is unique to JUST the XML serializer, doesn't affect any other types of serializers.
        // i.e. there is no global 'save format version' number, it's serializer-specific.
        //
        // NOTE: Please try and keep 'Root' unchanged and as generic as possible.  It's way better
        // to change 'Project'
        public int SaveVersion { get; set; } = -1;
        public string Watermark { get; set; }
        public string Extra1 { get; set; } = ""; // reserved for future use
        public string Extra2 { get; set; } = ""; // reserved for future use

        // The actual project itself. Almost any change you want to make should go in here.
        public Project Project { get; set; }
    };

    
    private readonly IXmlSerializerFactory xmlSerializerFactory;
    public ProjectXmlSerializer(IXmlSerializerFactory xmlSerializerFactory, IMigrationRunner migrationRunner = null, int migrateLoadedXmlToVersion = -1) : base(migrationRunner)
    {
        if (migrateLoadedXmlToVersion != -1)
            CurrentSaveFormatVersion = migrateLoadedXmlToVersion;
        
        this.xmlSerializerFactory = xmlSerializerFactory;
    }

    public override byte[] Save(Project project)
    {
        // Wrap the project in a top-level root element with some info about the XML file
        // format version. Note that each serializer has its own implementation of storing this metadata
        var rootElement = new Root
        {
            SaveVersion = CurrentSaveFormatVersion,
            Watermark = DizWatermark,
            Project = project
        };
        
        // one silly thing. we'll re-sort labels so they show up in the output file in order.
        project.Data.Labels.SortLabels();

        BeforeSerialize?.Invoke(this, rootElement);

        var serializerConfig = GetSerializerConfig(project.ProjectSettings.RomBytesOutputFormatSettings);
        
        var xmlStr = serializerConfig.Create().Serialize(
            new XmlWriterSettings {Indent = true},
            rootElement);

        return Encoding.UTF8.GetBytes(xmlStr);
    }

    private IConfigurationContainer GetSerializerConfig(
        [CanBeNull] RomBytesOutputFormatSettings romBytesOutputFormat = null        // only used when saving
        ) => 
        xmlSerializerFactory.GetSerializer(romBytesOutputFormat);
    
    public override ProjectOpenResult Load(byte[] projectFileRawXmlBytes)
    {
        // Note: Migrations not yet written for XML itself. ExtendedXmlSerializer has support for this
        // if we need it, put it in a new MigrationRunner.SetupMigrateXml() or similar.

        var xmlStr = Encoding.UTF8.GetString(projectFileRawXmlBytes);
        var versionOnDisk = RunPreDeserializeIntegrityChecks(xmlStr);

        MigrationRunner.StartingSaveVersion = versionOnDisk;
        MigrationRunner.TargetSaveVersion = CurrentSaveFormatVersion;

        var root = DeserializeProjectXml(xmlStr);
        RunIntegrityChecks(root.SaveVersion, root.Watermark);
            
        AfterDeserialize?.Invoke(this, root);

        var projectOpenResult = new ProjectOpenResult
        {
            Root = root,
            OpenResult =
            {
                ProjectFileOriginalVersion = versionOnDisk,
                ProjectFileUpgradedToVersion = CurrentSaveFormatVersion,   
            }
        };
        
        if (versionOnDisk != CurrentSaveFormatVersion)
        {
            // this isn't necessarily anything that's an issue, but, we definitely want to tell the user.
            projectOpenResult.OpenResult.Warnings.Add($"Diz project file format on disk was older: [file version={versionOnDisk}]." +
                                                      $" When this project is next saved, it will automatically be upgraded to the latest format [file version={CurrentSaveFormatVersion}]." +
                                                      $" To be safe, you take a moment to make a backup copy of your project files before saving or exporting, just in case.");
        }
        
        return projectOpenResult;
    }

    // finally. this is the real deal.
    private Root DeserializeProjectXml(string xmlStr) => 
        GetSerializerConfig().Create().Deserialize<Root>(xmlStr);

    // return the save file version# detected in the raw data
    private int RunPreDeserializeIntegrityChecks(string rawXml)
    {
        // run this check before opening with our real serializer. read a minimal part of the XML
        // manually to verify the root element looks sane.
        var xDoc = XDocument.Parse(rawXml);
        var xRoot = xDoc.Root;
            
        var saveVersionStr = xRoot?.Attribute("SaveVersion")?.Value;
        var waterMarkStr = xRoot?.Attribute("Watermark")?.Value;
            
        if (string.IsNullOrEmpty(saveVersionStr))
            throw new InvalidDataException("SaveVersion attribute missing on root element");

        var saveVersion = int.Parse(saveVersionStr);

        RunIntegrityChecks(saveVersion, waterMarkStr);

        return saveVersion;
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private void RunIntegrityChecks(int saveVersion, string watermark)
    {
        if (watermark != DizWatermark)
            throw new InvalidDataException(
                "This file doesn't appear to be a valid DiztinGUIsh XML file (missing/invalid watermark element in XML)");
            
        if (saveVersion > CurrentSaveFormatVersion)
            throw new InvalidDataException(
                $"The save file version '{saveVersion}' is newer than is supported in this version of DiztinGUIsh"+
                $", cancelling project open. (we only support save versions <= {CurrentSaveFormatVersion})."+
                " Please check if there is an update for DiztinGUIsh in order to open this file.");

        // Apply any migrations here for older save file formats. Right now,
        // there aren't any because we're on the first revision.
        // The XML serialization might be fairly forgiving of most kinds of changes,
        // so you may not have to write migrations unless properties are renamed or deleted.
        if (saveVersion < EarliestSupportedSaveFormatVersion)
        {
            throw new InvalidDataException(
                $"The save file version is from an older version of DiztinGUIsh and can't be imported with this newer version. Try using an older version to bring it up to date and re-import here again." +
                "Please check for an upgraded release of DiztinGUIsh, it should be able to open this file."+
                $"(Save file version of loaded project: '{saveVersion}', we are expecting version {CurrentSaveFormatVersion}.)");
        }
    }
}
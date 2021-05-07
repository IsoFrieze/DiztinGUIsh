using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Diz.Core.model;
using Diz.Core.util;
using ExtendedXmlSerializer;

namespace Diz.Core.serialization.xml_serializer
{
    public class ProjectXmlSerializer : ProjectSerializer
    {
        // NEVER CHANGE THIS ONE.
        public const int FirstSaveFormatVersion = 100;

        // increment this if you change the XML file format
        // history:
        // - 100: original XML version for Diz 2.0
        // - 101: no structure changes but, japanese chars in SNES header cartridge title were being saved
        //        incorrectly, so, allow project XMLs to load IF 
        public const int CurrentSaveFormatVersion = 101;

        // update this if we are dropped support for really old save formats.
        public const int EarliestSupportedSaveFormatVersion = FirstSaveFormatVersion;

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

        public override byte[] Save(Project project)
        {
            // Wrap the project in a top-level root element with some info about the XML file
            // format version. Note that each serializer has its own implementation of storing this metadata
            var rootElement = new Root
            {
                SaveVersion = CurrentSaveFormatVersion,
                Watermark = Watermark,
                Project = project,
            };

            BeforeSerialize?.Invoke(this, rootElement);

            var xmlStr = XmlSerializerSupport.GetSerializer().Create().Serialize(
                new XmlWriterSettings {Indent = true},
                rootElement);

            var finalBytes = Encoding.UTF8.GetBytes(xmlStr);

            // if you want some sanity checking, run this to verify everything saved correctly
            // DebugVerifyProjectEquality(project, finalBytes);
            // end debug

            return finalBytes;
        }

        public delegate void SerializeEvent(ProjectXmlSerializer projectXmlSerializer, Root rootElement);
        public event SerializeEvent BeforeSerialize;
        public event SerializeEvent AfterDeserialize;

        // just for debugging purposes, compare two projects together to make sure they serialize/deserialize
        // correctly.
        private void DebugVerifyProjectEquality(Root originalProjectWeJustSaved, byte[] projectBytesWeJustSerialized)
        {
            var (xmlRoot, _) = Load(projectBytesWeJustSerialized);
            var project2 = xmlRoot.Project;

            new ProjectFileManager().PostSerialize(xmlRoot);
            DebugVerifyProjectEquality(originalProjectWeJustSaved.Project, project2);
        }

        public override (Root xmlRoot, string warning) Load(byte[] rawBytes)
        {
            // TODO: it would be much more user-friendly/reliable if we could deserialize the
            // Root element ALONE first, check for valid version/watermark, and only then try
            // to deserialize the rest of the doc.
            //
            // Also, we can do data migrations based on versioning, and ExtendedXmlSerializer

            var xmlStr = Encoding.UTF8.GetString(rawBytes);
            RunIntegrityChecks(xmlStr);
            var root = XmlSerializerSupport.GetSerializer().Create().Deserialize<Root>(xmlStr);
            AfterDeserialize?.Invoke(this, root);
            RunIntegrityChecks(root.SaveVersion, root.Watermark);

            var warning = "";

            return (root, warning);
        }

        private static void RunIntegrityChecks(string rawXml)
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
        }

        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
        private static void RunIntegrityChecks(int saveVersion, string watermark)
        {
            if (watermark != Watermark)
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

        public static void OnBeforeAddLinkedRom(ref AddRomDataCommand romAddCmd)
        {
            PostSerializeMigrations.Run(ref romAddCmd, true);
        }

        public static void OnAfterAddLinkedRom(ref AddRomDataCommand romAddCmd)
        {
            PostSerializeMigrations.Run(ref romAddCmd, false);
        }
    }
}
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Xml;
using Diz.Core.model;
using ExtendedXmlSerializer;

namespace Diz.Core.serialization.xml_serializer
{
    public class ProjectXmlSerializer : ProjectSerializer
    {
        // NEVER CHANGE THIS ONE.
        private const int FirstSaveFormatVersion = 100;

        // increment this if you change the XML file format
        private const int CurrentSaveFormatVersion = FirstSaveFormatVersion;

        // update this if we are dropped support for really old save formats.
        // ReSharper disable once UnusedMember.Local
        private const int EarliestSupportedSaveFormatVersion = FirstSaveFormatVersion;

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        private class Root
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
            var rootElement = new Root()
            {
                SaveVersion = CurrentSaveFormatVersion,
                Watermark = DizWatermark,
                Project = project,
            };

            var xmlStr = XmlSerializerSupport.GetSerializer().Create().Serialize(
                new XmlWriterSettings {Indent = true},
                rootElement);

            var finalBytes = Encoding.UTF8.GetBytes(xmlStr);

            // if you want some sanity checking, run this to verify everything saved correctly
            #if HEAVY_LOADSAVE_VERIFICATION
            DebugVerifyProjectEquality(project, finalBytes);
            #endif

            return finalBytes;
        }

        #if HEAVY_LOADSAVE_VERIFICATION
        // just for debugging purposes, compare two projects together to make sure they serialize/deserialize
        // correctly.
        private void DebugVerifyProjectEquality(Project originalProjectWeJustSaved, byte[] projectBytesWeJustSerialized)
        {
            var result = Load(projectBytesWeJustSerialized);
            var project2 = result.project;

            new ProjectFileManager().PostSerialize(project2);
            DebugVerifyProjectEquality(originalProjectWeJustSaved, project2);
        }
        #endif

        public override (Project project, string warning) Load(byte[] data)
        {
            // TODO: it would be much more user-friendly/reliable if we could deserialize the
            // Root element ALONE first, check for valid version/watermark, and only then try
            // to deserialize the rest of the doc.
            //
            // Also, we can do data migrations based on versioning, and ExtendedXmlSerializer

            var text = Encoding.UTF8.GetString(data);
            var root = XmlSerializerSupport.GetSerializer().Create().Deserialize<Root>(text);

            if (root.Watermark != DizWatermark)
                throw new InvalidDataException(
                    "This file doesn't appear to be a valid DiztinGUIsh XML file (missing/invalid watermark element in XML)");

            if (root.SaveVersion > CurrentSaveFormatVersion)
                throw new InvalidDataException(
                    $"Save file version is newer than this version of DiztinGUIsh, likely can't be opened safely. This save file version = '{root.SaveVersion}', our highest supported version is {CurrentSaveFormatVersion}");

            // Apply any migrations here for older save file formats. Right now,
            // there aren't any because we're on the first revision.
            // The XML serialization might be fairly forgiving of most kinds of changes,
            // so you may not have to write migrations unless properties are renamed or deleted.
            if (root.SaveVersion < CurrentSaveFormatVersion)
            {
                throw new InvalidDataException(
                    $"Save file version is newer than this version of DiztinGUIsh, likely can't be opened safely. This save file version = '{root.SaveVersion}', our highest supported version is {CurrentSaveFormatVersion}");
            }

            var project = root.Project;
            var warning = "";

            return (project, warning);
        }
    }
}
using System;
using System.Diagnostics;
using System.IO;
using Diz.Core.model;
using Diz.Core.model.project;
using Diz.Core.serialization.binary_serializer_old;
using Diz.Core.serialization.xml_serializer;
using Diz.Core.util;

namespace Diz.Core.serialization
{
    public class ProjectFileManager
    {
        public Func<string, string> RomPromptFn { get; set; }

        public (Project project, string warning) Open(string filename)
        {
            Trace.WriteLine("Opening Project START");
            
            #if PROFILING
            using var profilerSnapshot = new ProfilerDotTrace.CaptureSnapshot();
            #endif

            var (serializer, xmlRoot, warning) = Deserialize(filename);
            VerifyIntegrityDeserialized(xmlRoot);
            PostSerialize(filename, xmlRoot, serializer);

            Trace.WriteLine("Opening Project END");
            return (xmlRoot.Project, warning);
        }

        private void PostSerialize(string filename, ProjectXmlSerializer.Root xmlRoot, ProjectSerializer serializer)
        {
            xmlRoot.Project.ProjectFileName = filename;

            // at this stage, 'Data' is populated with everything EXCEPT the actual ROM bytes.
            // It would be easy to store the ROM bytes in the save file, but, for copyright reasons,
            // we leave it out.
            //
            // So now, with all our metadata loaded successfully, we now open the .smc file on disk
            // and marry the original rom's bytes with all of our metadata loaded from the project file.

            var romAddCmd = new AddRomDataCommand(xmlRoot)
            {
                GetNextRomFileToTry = RomPromptFn,
                MigrationRunner = serializer.MigrationRunner,
            };

            romAddCmd.TryReadAttachedProjectRom();
        }

        private static void VerifyIntegrityDeserialized(ProjectXmlSerializer.Root xmlRoot)
        {
            var data = xmlRoot.Project.Data;
            // Debug.Assert(data.Labels != null && data.Comments != null);
            // Debug.Assert(data.RomBytes != null && data.RomBytes.Count > 0);
        }

        private (ProjectSerializer serializer, ProjectXmlSerializer.Root xmlRoot, string warning) Deserialize(string filename)
        {
            var projectFileBytes = ReadProjectFileBytes(filename);
            var serializer = GetSerializerForFormat(projectFileBytes);
            var (xmlRoot, warning) = DeserializeWith(serializer, projectFileBytes);
            return (serializer, xmlRoot, warning);
        }

        private byte[] ReadProjectFileBytes(string filename)
        {
            var projectFileBytes = ReadAllBytes(filename);

            if (IsLikelyCompressed(filename))
                projectFileBytes = Util.TryUnzip(projectFileBytes);
            
            return projectFileBytes;
        }

        private ProjectSerializer GetSerializerForFormat(byte[] data)
        {
            if (BinarySerializer.IsBinaryFileFormat(data))
                return new BinarySerializer();

            return CreateProjectXmlSerializer();
        }

        protected virtual ProjectXmlSerializer CreateProjectXmlSerializer()
        {
            return new ProjectXmlSerializer();
        }

        private static bool IsLikelyCompressed(string filename) => 
            !Path.GetExtension(filename).Equals(".dizraw", StringComparison.InvariantCultureIgnoreCase);

        public void Save(Project project, string filename)
        {
            // Everything saves in XML format from here on out.
            // Binary format is deprecated.
            ProjectSerializer defaultSerializer = new ProjectXmlSerializer();
            Save(project, filename, defaultSerializer);
        }

        private void Save(Project project, string filename, ProjectSerializer serializer)
        {
            var data = DoSave(project, filename, serializer);

            WriteBytes(filename, data);
            project.UnsavedChanges = false;
            project.ProjectFileName = filename;
        }

        private byte[] DoSave(Project project, string filename, ProjectSerializer serializer)
        {
            var data = SerializeWith(project, serializer);

            if (IsLikelyCompressed(filename))
                data = Util.TryZip(data);

            return data;
        }

        #region Hooks
        protected virtual (ProjectXmlSerializer.Root xmlRoot, string warning) DeserializeWith(
            ProjectSerializer serializer, byte[] rawBytes
            ) => serializer.Load(rawBytes);

        protected virtual byte[] SerializeWith(Project project, ProjectSerializer serializer) => 
            serializer.Save(project);

        protected virtual byte[] ReadAllBytes(string filename) => 
            File.ReadAllBytes(filename);

        protected virtual void WriteBytes(string filename, byte[] data) => 
            File.WriteAllBytes(filename, data);

        #endregion
    }
}

using System;
using System.Diagnostics;
using System.IO;
using Diz.Core.model;
using Diz.Core.serialization.binary_serializer_old;
using Diz.Core.serialization.xml_serializer;
using Diz.Core.util;

namespace Diz.Core.serialization
{
    public class ProjectFileManager
    {
        public Func<string, string> RomPromptFn { get; set; }

        // helper version that throws exceptions if any issue present, including warnings. use mostly for testing or automation
        public static Project Load(string romFilename)
        {
            var (project, warning) = new ProjectFileManager().Open(romFilename);
            if (!string.IsNullOrEmpty(warning))
                throw new InvalidDataException($"failed opening project:\n{warning}");

            return project;
        }

        public (Project project, string warning) Open(string filename)
        {
            Trace.WriteLine("Opening Project START");

            var (xmlRoot, warning) = DoOpen(filename);
            xmlRoot.Project.ProjectFileName = filename;
            PostSerialize(xmlRoot);

            Trace.WriteLine("Opening Project END");
            return (xmlRoot.Project, warning);
        }

        private (ProjectXmlSerializer.Root xmlRoot, string warning) DoOpen(string projectFilename)
        {
            var rawBytes = ReadAllBytes(projectFilename);

            if (!GuessIfIsUncompressedProject(projectFilename))
                rawBytes = Util.TryUnzip(rawBytes);

            var serializer = GetSerializerForFormat(rawBytes);
            return DeserializeWith(serializer, rawBytes);
        }

        public bool PostSerialize(ProjectXmlSerializer.Root xmlRoot)
        {
            // at this stage, 'Data' is populated with everything EXCEPT the actual ROM bytes.
            // It would be easy to store the ROM bytes in the save file, but, for copyright reasons,
            // we leave it out.
            //
            // So now, with all our metadata loaded successfully, we now open the .smc file on disk
            // and marry the original rom's bytes with all of our metadata loaded from the project file.

            Debug.Assert(xmlRoot.Project.Data.Labels != null && xmlRoot.Project.Data.Comments != null);
            Debug.Assert(xmlRoot.Project.Data.RomBytes != null && xmlRoot.Project.Data.RomBytes.Count > 0);

            var romAddCmd = new AddRomDataCommand
            {
                Root = xmlRoot,
                GetNextRomFileToTry = RomPromptFn,
            };
            
            ProjectXmlSerializer.OnBeforeAddLinkedRom(ref romAddCmd);
            var result = romAddCmd.TryReadAttachedProjectRom();
            ProjectXmlSerializer.OnAfterAddLinkedRom(ref romAddCmd);
            return result;
        }

        private static ProjectSerializer GetSerializerForFormat(byte[] data)
        {
            if (BinarySerializer.IsBinaryFileFormat(data))
                return new BinarySerializer();

            return new ProjectXmlSerializer();
        }

        private static bool GuessIfIsUncompressedProject(string filename)
        {
            return Path.GetExtension(filename).Equals(".dizraw", StringComparison.InvariantCultureIgnoreCase);
        }

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

        public byte[] DoSave(Project project, string filename, ProjectSerializer serializer)
        {
            var data = SerializeWith(project, serializer);

            if (!GuessIfIsUncompressedProject(filename))
                data = Util.TryZip(data);

            return data;
        }

        #region Hooks
        protected virtual (ProjectXmlSerializer.Root xmlRoot, string warning) DeserializeWith(ProjectSerializer serializer, byte[] rawBytes) => 
            serializer.Load(rawBytes);

        protected virtual byte[] SerializeWith(Project project, ProjectSerializer serializer) => 
            serializer.Save(project);

        protected virtual byte[] ReadAllBytes(string filename) => 
            File.ReadAllBytes(filename);

        protected virtual void WriteBytes(string filename, byte[] data) => 
            File.WriteAllBytes(filename, data);

        #endregion
    }
}

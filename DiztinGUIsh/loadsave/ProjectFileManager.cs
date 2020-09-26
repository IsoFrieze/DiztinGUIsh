using System;
using System.IO;
using System.Windows.Forms;
using DiztinGUIsh.loadsave.binary_serializer_old;
using DiztinGUIsh.loadsave.xml_serializer;

namespace DiztinGUIsh.loadsave
{
    static class ProjectFileManager
    {
        public static Project Open(string filename)
        {
            try
            {
                return DoOpen(filename);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error opening project file", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        private static Project DoOpen(string filename)
        {
            var data = File.ReadAllBytes(filename);

            if (!IsUncompressedProject(filename))
                data = Util.TryUnzip(data);

            var serializer = GetSerializerForFormat(data);
            var project = serializer.Load(data);

            project.ProjectFileName = filename;

            return project;
        }

        private static ProjectSerializer GetSerializerForFormat(byte[] data)
        {
            if (BinarySerializer.IsBinaryFileFormat(data))
                return new BinarySerializer();
            
            return new ProjectXmlSerializer();
        }

        private static bool IsUncompressedProject(string filename)
        {
            return Path.GetExtension(filename).Equals(".dizraw", StringComparison.InvariantCultureIgnoreCase);
        }

        public static void Save(Project project, string filename)
        {
            // Everything saves in XML format from here on out.
            // Binary format is deprecated.
            ProjectSerializer defaultSerializer = new ProjectXmlSerializer();

            Save(project, filename, defaultSerializer);
        }

        private static void Save(Project project, string filename, ProjectSerializer serializer)
        {
            var data = DoSave(project, filename, serializer);

            File.WriteAllBytes(filename, data);
            project.UnsavedChanges = false;
            project.ProjectFileName = filename;
        }

        private static byte[] DoSave(Project project, string filename, ProjectSerializer serializer)
        {
            var data = serializer.Save(project);

            if (!IsUncompressedProject(filename))
                data = Util.TryZip(data);

            return data;
        }
    }
}

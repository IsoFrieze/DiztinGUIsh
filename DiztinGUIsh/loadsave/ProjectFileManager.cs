﻿using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using DiztinGUIsh.loadsave.binary_serializer_old;
using DiztinGUIsh.loadsave.xml_serializer;

namespace DiztinGUIsh.loadsave
{
    public static class ProjectFileManager
    {
        public static Project Open(string filename, Func<string, string> romPromptFn)
        {
            try
            {
                return DoOpen(filename, romPromptFn);
            }
            catch (Exception e)
            {
                // TODO: remove direct GUI stuff from here.
                MessageBox.Show(e.Message, "Error opening project file", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        private static Project DoOpen(string filename, Func<string, string> romPromptFn)
        {
            var data = File.ReadAllBytes(filename);

            if (!IsUncompressedProject(filename))
                data = Util.TryUnzip(data);

            var serializer = GetSerializerForFormat(data);
            var project = serializer.Load(data);

            project.ProjectFileName = filename;

            PostSerialize(project, romPromptFn);

            return project;
        }

        private static bool PostSerialize(Project project, Func<string, string> romPromptFn)
        {
            // at this stage, 'Data' is populated with everything EXCEPT the actual ROM bytes.
            // It would be easy to store the ROM bytes in the save file, but, for copyright reasons,
            // we leave it out.
            //
            // So now, with all our metadata loaded successfully, we now open the .smc file on disk
            // and marry the original rom's bytes with all of our metadata loaded from the project file.

            var data = project.Data;

            Debug.Assert(data.Labels != null && data.Comments != null);
            Debug.Assert(data.RomBytes != null && data.RomBytes.Count > 0);

            var rom = ReadFromOriginalRom(project, romPromptFn);
            if (rom == null)
                return false;

            data.CopyRomDataIn(rom);
            return true;
        }

        public static byte[] ReadFromOriginalRom(Project project, Func<string, string> romPromptFn)
        {
            string firstRomFileWeTried;
            var nextFileToTry = firstRomFileWeTried = project.AttachedRomFilename;
            byte[] rom;

            // try to open a ROM that matches us, if not, ask the user until they give up
            do
            {
                var error = project.ReadRomIfMatchesProject(nextFileToTry, out rom);
                if (error == null)
                    break;

                nextFileToTry = romPromptFn(error);
                if (nextFileToTry == null)
                    return null;
            } while (true);

            project.AttachedRomFilename = nextFileToTry;

            if (project.AttachedRomFilename != firstRomFileWeTried)
                project.UnsavedChanges = true;

            return rom;
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

        public static Project ImportRomAndCreateNewProject(Project.ImportRomSettings importSettings)
        {
            var project = new Project
            {
                AttachedRomFilename = importSettings.RomFilename,
                UnsavedChanges = false,
                ProjectFileName = null,
                Data = new Data()
            };

            project.Data.RomMapMode = importSettings.ROMMapMode;
            project.Data.RomSpeed = importSettings.ROMSpeed;
            project.Data.CreateRomBytesFromRom(importSettings.RomBytes);

            foreach (var pair in importSettings.InitialLabels)
                project.Data.AddLabel(pair.Key, pair.Value, true);

            foreach (var pair in importSettings.InitialHeaderFlags)
                project.Data.SetFlag(pair.Key, pair.Value);

            // Save a copy of these identifying ROM bytes with the project file itself.
            // When we reload, we will make sure the linked ROM still matches them.
            project.InternalCheckSum = project.Data.GetRomCheckSumsFromRomBytes();
            project.InternalRomGameName = project.Data.GetRomNameFromRomBytes();

            project.UnsavedChanges = true;

            return project;
        }
    }
}
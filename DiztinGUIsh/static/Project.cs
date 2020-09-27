using DiztinGUIsh.window;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using ExtendedXmlSerializer;

namespace DiztinGUIsh
{
    // MODEL
    public class Project
    {
        // Any public properties will be automatically serialized to XML.
        // They require a get AND set.

        public string ProjectFileName { get; set; }
        public string AttachedRomFilename { get; set; }
        public bool UnsavedChanges { get; set; }

        // safety check: these are copies of data in the ROM, they must always match the ROM on project load
        // or else we might be looking at the wrong file.
        public string InternalRomGameName { get; set; }
        public int InternalCheckSum { get; set; } = -1;

        // needs to come last for serialization
        public Data Data { get; set; }

        public LogWriterSettings LogWriterSettings;

        public Project()
        {
            LogWriterSettings.SetDefaults();
        }

        public class ImportRomSettings
        {
            public Data.ROMMapMode ROMMapMode;
            public Data.ROMSpeed ROMSpeed;

            public Dictionary<int, Label> InitialLabels;
            public Dictionary<int, Data.FlagType> InitialHeaderFlags;

            public byte[] rom_bytes;
            public string rom_filename;
        }

        public void ImportRomAndCreateNewProject(ImportRomSettings importSettings)
        {
            AttachedRomFilename = importSettings.rom_filename;
            UnsavedChanges = false;
            ProjectFileName = null;

            Data = new Data();
            Data.Initiate(importSettings.rom_bytes, importSettings.ROMMapMode, importSettings.ROMSpeed);

            // TODO: get this UI out of here. probably just use databinding instead
            // AliasList.me.ResetDataGrid();

            if (importSettings.InitialLabels.Count > 0)
            {
                foreach (var pair in importSettings.InitialLabels)
                    Data.AddLabel(pair.Key, pair.Value, true);
                UnsavedChanges = true;
            }

            if (importSettings.InitialHeaderFlags.Count > 0)
            {
                foreach (var pair in importSettings.InitialHeaderFlags)
                    Data.SetFlag(pair.Key, pair.Value);
                UnsavedChanges = true;
            }

            // Save a copy of these identifying ROM bytes with the project file itself.
            // When we reload, we will make sure the linked ROM still matches them.
            InternalCheckSum = Data.GetRomCheckSumsFromRomBytes();
            InternalRomGameName = Data.GetRomNameFromRomBytes();
        }

        public byte[] ReadFromOriginalRom()
        {
            string firstRomFileWeTried;
            var nextFileToTry = firstRomFileWeTried = AttachedRomFilename;
            byte[] rom = null;

            do {
                var error = ReadROMIfMatchesProject(nextFileToTry, out rom);
                if (error == null)
                    break;

                nextFileToTry = PromptForNewRom($"{error} Link a new ROM now?");
                if (nextFileToTry == null)
                    return null;
            } while (true);

            AttachedRomFilename = nextFileToTry;

            if (AttachedRomFilename != firstRomFileWeTried)
                UnsavedChanges = true;

            return rom;
        }

        private string ReadROMIfMatchesProject(string filename, out byte[] rom_bytes)
        {
            string error_msg = null;

            try {
                rom_bytes = Util.ReadAllRomBytesFromFile(filename);
                if (rom_bytes != null)
                {
                    error_msg = IsThisRomIsIdenticalToUs(rom_bytes);
                    if (error_msg == null)
                        return null;
                }
            } catch (Exception ex) {
                error_msg = ex.Message;
            }

            rom_bytes = null;
            return error_msg;
        }

        // returns error message if it's not identical, or null if everything is OK.
        private string IsThisRomIsIdenticalToUs(byte[] rom)
        {
            var offset = Data.GetRomSettingOffset(Data.RomMapMode);
            if (rom.Length <= offset + 10)
                return "The linked ROM is too small. It can't be opened.";

            var romInternalGameName = Util.ReadStringFromByteArray(rom, 0x15, offset);

            var myChecksums = Util.ByteArrayToInteger(rom, offset + 7);

            if (romInternalGameName != InternalRomGameName)
                return $"The linked ROM's internal name '{romInternalGameName}' doesn't " + 
                       $"match the project's internal name of '{InternalRomGameName}'.";
            
            if (myChecksums != InternalCheckSum)
                return $"The linked ROM's checksums '{myChecksums:X8}' " + 
                       $"don't match the project's checksums of '{InternalCheckSum:X8}'.";

            return null;
        }

        private string PromptForNewRom(string promptText)
        {
            var dialogResult = MessageBox.Show(promptText, "Error",
                MessageBoxButtons.YesNo, MessageBoxIcon.Error);

            if (dialogResult == DialogResult.No)
                return null;

            return PromptToSelectFile();
        }

        private string PromptToSelectFile()
        {
            return Util.PromptToSelectFile(ProjectFileName);
        }

        public bool PostSerializationLoad()
        {
            // at this stage, 'Data' is populated with everything EXCEPT the actual ROM bytes.
            // It would be easy to store the ROM bytes in the save file, but, for copyright reasons,
            // we leave it out.
            //
            // So now, with all our metadata loaded successfully, we now open the .smc file on disk
            // and marry the original rom's bytes with all of our metadata loaded from the project file.

            var rom = ReadFromOriginalRom();
            if (rom == null)
                return false;

            Data.CopyRomDataIn(rom);
            return true;
        }

        #region Equality
        protected bool Equals(Project other)
        {
            return ProjectFileName == other.ProjectFileName && AttachedRomFilename == other.AttachedRomFilename && Equals(Data, other.Data) && InternalRomGameName == other.InternalRomGameName && InternalCheckSum == other.InternalCheckSum;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Project) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (ProjectFileName != null ? ProjectFileName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (AttachedRomFilename != null ? AttachedRomFilename.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Data != null ? Data.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (InternalRomGameName != null ? InternalRomGameName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ InternalCheckSum;
                return hashCode;
            }
        }
        #endregion
    }
}

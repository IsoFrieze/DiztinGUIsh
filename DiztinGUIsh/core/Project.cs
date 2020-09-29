using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

namespace DiztinGUIsh
{
    public class Project : DizModel
    {
        // Any public properties will be automatically serialized to XML.
        // They require a get AND set. Order is important.
        public string ProjectFileName
        {
            get => projectFileName;
            set => SetField(ref projectFileName, value);
        }

        public string AttachedRomFilename
        {
            get => attachedRomFilename;
            set => SetField(ref attachedRomFilename, value);
        }

        // would be cool to make this more automatic. probably hook into SetField()
        // for a lot of it.
        public bool UnsavedChanges
        {
            get => unsavedChanges;
            set => SetField(ref unsavedChanges, value);
        }

        // safety checks:
        // The rom "Game name" and "Checksum" are copies of certain bytes from the ROM which
        // get stored with the project file.  REMEMBER: We don't store the actual ROM bytes
        // in the project file, so when we load a project, we must also open the same ROM and load its
        // bytes in the project.
        //
        // Project = Metadata
        // Rom = The real data
        //
        // If we load a ROM, and then its checksum and name don't match what we have stored,
        // then we have an issue (i.e. not the same ROM, or it was modified, or missing, etc).
        // The user must either provide the correct ROM, or abort loading the project.
        public string InternalRomGameName
        {
            get => internalRomGameName;
            set => SetField(ref internalRomGameName, value);
        }

        public int InternalCheckSum
        {
            get => internalCheckSum;
            set => SetField(ref internalCheckSum, value);
        }

        public LogWriterSettings LogWriterSettings
        {
            get => logWriterSettings;
            set => SetField(ref logWriterSettings, value);
        }

        // purely visual. what offset is currently being looked at in the main grid.
        // we store it here because we want to save it out with the project file
        private int currentViewOffset;
        public int CurrentViewOffset
        {
            get => currentViewOffset;
            set => SetField(ref currentViewOffset, value);
        }

        // needs to come last for serialization. this is the heart of the app, the actual
        // data from the ROM and metadata we add/create.
        public Data Data
        {
            get => data;
            set => SetField(ref data, value);
        }

        public Project()
        {
            LogWriterSettings.SetDefaults();
        }

        public struct ImportRomSettings
        {
            public Data.ROMMapMode ROMMapMode;
            public Data.ROMSpeed ROMSpeed;

            public Dictionary<int, Label> InitialLabels;
            public Dictionary<int, Data.FlagType> InitialHeaderFlags;

            public byte[] rom_bytes;
            public string rom_filename;
        }

        private string projectFileName;
        private string attachedRomFilename;
        private bool unsavedChanges;
        private string internalRomGameName;
        private int internalCheckSum = -1;
        private Data data;
        private LogWriterSettings logWriterSettings;

        public byte[] ReadFromOriginalRom()
        {
            string firstRomFileWeTried;
            var nextFileToTry = firstRomFileWeTried = AttachedRomFilename;
            byte[] rom;

            do {
                var error = ReadRomIfMatchesProject(nextFileToTry, out rom);
                if (error == null)
                    break;

                // TODO: move to controller
                nextFileToTry = PromptForNewRom($"{error} Link a new ROM now?");
                if (nextFileToTry == null)
                    return null;
            } while (true);

            AttachedRomFilename = nextFileToTry;

            if (AttachedRomFilename != firstRomFileWeTried)
                UnsavedChanges = true;

            return rom;
        }

        private string ReadRomIfMatchesProject(string filename, out byte[] rom_bytes)
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

        private string IsThisRomIsIdenticalToUs(byte[] romBytes) => 
            Util.IsThisRomIsIdenticalToUs(romBytes, Data.RomMapMode, InternalRomGameName, InternalCheckSum);
        private string PromptForNewRom(string promptText)
        {
            // TODO: put this in the view, hooked up through controller.

            var dialogResult = MessageBox.Show(promptText, "Error",
                MessageBoxButtons.YesNo, MessageBoxIcon.Error);

            return dialogResult == DialogResult.Yes ? PromptToSelectFile() : null;
        }

        private string PromptToSelectFile()
        {
            // TODO: move to controller
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

            Debug.Assert(Data.RomBytes != null && Data.Labels != null && Data.Comments != null);

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

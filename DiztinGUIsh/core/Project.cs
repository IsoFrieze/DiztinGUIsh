using System;
using System.Collections.Generic;
using DiztinGUIsh.core.util;

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

        public class ImportRomSettings
        {
            // temp
            private Data.ROMMapMode mode;
            public Data.ROMMapMode ROMMapMode
            {
                get => mode;
                set => mode = value;
            }

            public Data.ROMSpeed ROMSpeed { get; set; }

            public Dictionary<int, Label> InitialLabels { get; set; }
            public Dictionary<int, Data.FlagType> InitialHeaderFlags { get; set; }

            public byte[] RomBytes { get; set; }
            public string RomFilename { get; set; }
        }

        private string projectFileName;
        private string attachedRomFilename;
        private bool unsavedChanges;
        private string internalRomGameName;
        private int internalCheckSum = -1;
        private Data data;
        private LogWriterSettings logWriterSettings;

        public string ReadRomIfMatchesProject(string filename, out byte[] romBytes)
        {
            string errorMsg = null;

            try {
                romBytes = RomUtil.ReadAllRomBytesFromFile(filename);
                if (romBytes != null)
                {
                    errorMsg = IsThisRomIsIdenticalToUs(romBytes);
                    if (errorMsg == null)
                        return null;
                }
            } catch (Exception ex) {
                errorMsg = ex.Message;
            }

            romBytes = null;
            return errorMsg;
        }

        private string IsThisRomIsIdenticalToUs(byte[] romBytes) => 
            RomUtil.IsThisRomIsIdenticalToUs(romBytes, Data.RomMapMode, InternalRomGameName, InternalCheckSum);

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

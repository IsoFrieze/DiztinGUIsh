using System;
using Diz.Core.export;
using Diz.Core.util;
using DiztinGUIsh;

namespace Diz.Core.model
{
    public class Project : DizDataModel
    {
        // Any public properties will be automatically serialized to XML unless noted.
        // They will require a get AND set.
        // Order is important.

        // not saved in XML
        public string ProjectFileName
        {
            get => projectFileName;
            set => SetField(ref projectFileName, value);
        }

        // not saved in XML
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

        public uint InternalCheckSum
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

        // don't access these backing fields directly, instead, always use the properties
        private string projectFileName;
        private string attachedRomFilename;
        private bool unsavedChanges;
        private string internalRomGameName;
        private uint internalCheckSum;
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
        
        public void CacheVerificationInfo()
        {
            // Save a copy of these identifying ROM bytes with the project file itself, so they'll
            // be serialized to disk on project save. When we reload, we verify the recreated ROM data still matches both
            // of these. If either are wrong, then the ROM on disk could be different from the one associated with the 
            // project.
            InternalCheckSum = Data.RomCheckSumsFromRomBytes;
            InternalRomGameName = Data.CartridgeTitleName;
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
                hashCode = (int) ((hashCode * 397) ^ InternalCheckSum);
                return hashCode;
            }
        }
        #endregion
    }
}

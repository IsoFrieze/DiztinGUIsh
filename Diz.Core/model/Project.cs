using System;
using System.ComponentModel;
using System.IO;
using System.Xml.Serialization;
using Diz.Core.export;
using Diz.Core.model.snes;
using Diz.Core.util;
using JetBrains.Annotations;

namespace Diz.Core.model
{
    public class Project : INotifyPropertyChanged
    {
        // Any public properties will be automatically serialized to XML unless noted.
        // They will require a get AND set.
        // Order is important.

        // NOT saved in XML, just a cache of the last filename this project was saved as.
        // (This field may require some rework for GUI multi-project support)
        [XmlIgnore] 
        public string ProjectFileName
        {
            get => projectFileName;
            set
            {
                string GetFullBasePathToRomFile()
                {
                    if (ProjectDirectory != "")
                        return ProjectDirectory;
                    
                    return value != "" ? Util.GetDirNameOrEmpty(value) : "";
                }
                
                var absolutePathToRomFile = Path.Combine(GetFullBasePathToRomFile(), Path.GetFileName(AttachedRomFilename) ?? "");
                
                if (!this.SetField(PropertyChanged, ref projectFileName, value))
                    return;

                // this will take the absolute path to the ROM file and convert it to a relative path
                // relative to the Project's dir.
                AttachedRomFilename = absolutePathToRomFile;
            }
        }

        [XmlIgnore] public string ProjectDirectory =>
            Util.GetDirNameOrEmpty(projectFileName);
        
        // RELATIVE PATH from ProjectDirectory to the original ROM file (.smc/.sfc/etc)
        public string AttachedRomFilename
        {
            get => attachedRomFilename;
            set
            {
                var projectFileRelativePath = Util.TryGetRelativePath(value, ProjectDirectory);
                
                if (attachedRomFilename != projectFileRelativePath)
                    UnsavedChanges = true;
                
                this.SetField(PropertyChanged, ref attachedRomFilename, projectFileRelativePath);
            }
        }

        [XmlIgnore] public string AttachedRomFileFullPath =>
            Path.Combine(ProjectDirectory, AttachedRomFilename);

        // NOT saved in XML
        // (would be cool to make this more automatic. probably hook into SetField()
        // for a lot of it)
        [XmlIgnore] public bool UnsavedChanges
        {
            get => unsavedChanges;
            set => this.SetField(PropertyChanged, ref unsavedChanges, value);
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
        // The user must either provide a ROM matching these criteria, or abort loading the project.
        public string InternalRomGameName
        {
            get => internalRomGameName;
            set => this.SetField(PropertyChanged, ref internalRomGameName, value);
        }

        public uint InternalCheckSum
        {
            get => internalCheckSum;
            set => this.SetField(PropertyChanged, ref internalCheckSum, value);
        }

        public LogWriterSettings LogWriterSettings
        {
            get => logWriterSettings;
            set => this.SetField(PropertyChanged, ref logWriterSettings, value);
        }

        // needs to come last for serialization. this is the heart of the app, the actual
        // data from the ROM and metadata we add/create.
        public Data Data
        {
            get => data;
            set => this.SetField(PropertyChanged, ref data, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public Project()
        {
            LogWriterSettings.SetDefaults();
        }

        // don't access these backing fields directly, instead, always use the properties
        private string projectFileName = "";
        private string attachedRomFilename = "";
        private bool unsavedChanges;
        private string internalRomGameName;
        private uint internalCheckSum;
        private Data data;
        private LogWriterSettings logWriterSettings;

        #region Equality
        protected bool Equals(Project other)
        {
            return ProjectFileName == other.ProjectFileName && AttachedRomFilename == other.AttachedRomFilename && Equals(Data, other.Data) && InternalRomGameName == other.InternalRomGameName && InternalCheckSum == other.InternalCheckSum;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
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

        public void CacheVerificationInfo()
        {
            // Save a copy of these identifying ROM bytes with the project file itself, so they'll
            // be serialized to disk on project save. When we reload, we verify the recreated ROM data still matches both
            // of these. If either are wrong, then the ROM on disk could be different from the one associated with the 
            // project.
            InternalCheckSum = Data.RomCheckSumsFromRomBytes;
            InternalRomGameName = Data.CartridgeTitleName;
        }
    }
}

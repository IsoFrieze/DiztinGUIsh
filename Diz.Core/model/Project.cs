#nullable enable

using System.ComponentModel;
using System.IO;
using System.Xml.Serialization;
using Diz.Core.export;
using Diz.Core.model.snes;
using Diz.Core.util;

namespace Diz.Core.model
{
    /// <summary>
    /// Any public properties will be automatically serialized to XML unless noted.
    /// They will require a get AND set. Order is important. 
    /// </summary>
    public class Project : INotifyPropertyChanged, IProjectWithSession
    {
        /// <summary>
        /// RELATIVE PATH from ProjectDirectory to the original ROM file (.smc/.sfc/etc)
        /// this is the only thing we should store in the XML to avoid people saving the file
        /// on different computers causing the path to flip flop around a bunch. 
        /// </summary>
        public string AttachedRomFilename
        {
            get => attachedRomFilename;
            set => this.SetField(PropertyChanged, ref attachedRomFilename, GetProjectFileRelativePath(value));
        }

        /// <summary>
        /// safety checks:
        /// The rom "Game name" and "Checksum" are copies of certain bytes from the ROM which
        /// get stored with the project file.  REMEMBER: We don't store the actual ROM bytes
        /// in the project file, so when we load a project, we must also open the same ROM and load its
        /// bytes in the project.
        ///
        /// Project = Metadata
        /// Rom = The real data
        ///
        /// If we load a ROM, and then its checksum and name don't match what we have stored,
        /// then we have an issue (i.e. not the same ROM, or it was modified, or missing, etc).
        /// The user must either provide a ROM matching these criteria, or abort loading the project. 
        /// </summary>
        public string InternalRomGameName
        {
            get => internalRomGameName;
            set => this.SetField(PropertyChanged, ref internalRomGameName, value);
        }
        
        /// <summary>
        /// 2bytes complement + 2 bytes checksum. store as 4 bytes of data in the XML.
        /// this will be set when the ROM is very first imported, and will be checked
        /// whenever we load the project.  this is important as a verification method 
        /// to make sure we load the ROM from the disk into .Data correctly.
        /// this is needed because we do not store the actual bytes from the ROM with the XML,
        /// and we need a way to verify that we have married the data correctly
        /// (and the ROM hasn't been corrupted) on project load from disk. 
        /// </summary>
        public uint InternalCheckSum
        {
            get => internalCheckSum;
            set => this.SetField(PropertyChanged, ref internalCheckSum, value);
        }

        /// <summary>
        /// User preferences for assembly exporter (formatting/etc)
        /// </summary>
        public LogWriterSettings LogWriterSettings
        {
            get => logWriterSettings;
            set => this.SetField(PropertyChanged, ref logWriterSettings, value);
        }
        
        /// <summary>
        /// The actual data associated with the ROM, labels, comments, etc
        /// This is the beating heart of Diz
        /// </summary>
        public Data? Data
        {
            get => data;
            set => this.SetField(PropertyChanged, ref data, value);
        }

        #region Non-Serialized Data

        /// <summary>
        /// Temporary session-specific data associated with this Project
        /// </summary>
        /// <remarks>
        /// Never saved with XML, this is all temporary data that exists as long as its open
        /// in the app, and no longer.
        /// </remarks>
        [XmlIgnore]
        public IProjectSession? Session
        {
            get => session;
            set
            {
                if (session != null)
                    session.PropertyChanged -= SessionOnPropertyChanged;

                this.SetField(PropertyChanged, ref session, value);
                
                if (session != null)
                    session.PropertyChanged += SessionOnPropertyChanged;
            }
        }
        
        // don't access these backing fields directly, always use the properties
        private string attachedRomFilename = "";
        private string internalRomGameName = "";
        private uint internalCheckSum;
        private LogWriterSettings logWriterSettings;
        private Data? data;
        private IProjectSession? session;

        #endregion
        public event PropertyChangedEventHandler? PropertyChanged;

        public Project()
        {
            LogWriterSettings = new LogWriterSettings();
            PropertyChanged += ProjectPropertyChanged;
        }

        private void ProjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // try not to get too fancy in this method.
            if (Session != null) 
                Session.UnsavedChanges = true;
        }

        public void CacheVerificationInfo()
        {
            // Save a copy of these identifying ROM bytes with the project file itself, so they'll
            // be serialized to disk on project save. When we reload, we verify the recreated ROM data still matches both
            // of these. If either are wrong, then the ROM on disk could be different from the one associated with the 
            // project.
            InternalCheckSum = Data?.RomCheckSumsFromRomBytes ?? 0x0;
            InternalRomGameName = Data?.CartridgeTitleName ?? "";
        }
        
        private string GetProjectFileRelativePath(string filename) =>
            Util.TryGetRelativePath(filename, Session?.ProjectDirectory ?? "");

        private string GetAbsolutePathToRomFile()
        {
            var pathToProjectFile = GetFullBasePathToRomFile(session?.ProjectFileName ?? "");
            var attachedRomFileNoPath = Path.GetFileName(AttachedRomFilename);
            return Path.Combine(pathToProjectFile, attachedRomFileNoPath);
        }
        
        private string GetFullBasePathToRomFile(string projFileName)
        {
            var projDir = session?.ProjectDirectory ?? "";
            if (projDir != "")
                return projDir;
                
            return projFileName != "" ? Util.GetDirNameOrEmpty(projFileName) : "";
        }

        private void SessionOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IProjectSession.ProjectFileName):
                    AttachedRomFilename = GetAbsolutePathToRomFile();
                    break;
            }
            
            PropertyChanged?.Invoke(sender, e);
        }
        
        #region Equality
        protected bool Equals(Project other)
        {
            // do not consider anything in 'session' in here,
            // we want this to be based on the immutable data only.
            return AttachedRomFilename == other.AttachedRomFilename &&
                   InternalRomGameName == other.InternalRomGameName &&
                   InternalCheckSum == other.InternalCheckSum &&
                   Equals(Data, other.Data); // do this last, expensive.
        }

        public override bool Equals(object? obj)
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
                var hashCode = AttachedRomFilename?.GetHashCode() ?? 0;
                hashCode = (hashCode * 397) ^ (Data != null ? Data.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ InternalRomGameName?.GetHashCode() ?? 0;
                hashCode = (int) ((hashCode * 397) ^ InternalCheckSum);
                return hashCode;
            }
        }
        #endregion
    }

    public interface IProjectWithSession {
        IProjectSession? Session { get; set; }
        string AttachedRomFilename { get; }
    }
    
    public interface IProjectSession : INotifyPropertyChanged
    {
        public string ProjectDirectory { get; }
        string AttachedRomFileFullPath { get; }
        string ProjectFileName { get; set; }
        bool UnsavedChanges { get; set; }
    }

    /// <summary>
    /// temporary data stored about the current project "session"
    /// i.e. mostly stuff we don't want serialized to XML that may change
    /// from run to run of the app (like working dir,etc)
    /// stuff in here might want to be saved somewhere else. 
    /// </summary>
    public class ProjectSession : IProjectSession
    {
        // cache of the last filename this project was saved as.
        // (This field may require some rework for GUI multi-project support)
        public string ProjectFileName
        {
            get => projectFileName;
            set => this.SetField(PropertyChanged, ref projectFileName, value);
        }
        
        public bool UnsavedChanges
        {
            get => unsavedChanges;
            set => this.SetField(PropertyChanged, ref unsavedChanges, value);
        }
        
        public string ProjectDirectory =>
            Util.GetDirNameOrEmpty(projectFileName);
        
        public string AttachedRomFileFullPath =>
            Path.Combine(ProjectDirectory, project.AttachedRomFilename);

        private string projectFileName = "";
        private bool unsavedChanges;
        private readonly IProjectWithSession project;

        public ProjectSession(IProjectWithSession project)
        {
            this.project = project;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}

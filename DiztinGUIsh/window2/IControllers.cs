using System;
using System.ComponentModel;
using Diz.Core.export;
using Diz.Core.model;
using DiztinGUIsh.controller;
using DiztinGUIsh.util;

namespace DiztinGUIsh.window2
{
    public interface IController
    {
        IViewer View { get; }
    }

    public interface ICloseHandler
    {
        public event EventHandler Closed;
    }

    public interface IFormController : IController, ICloseHandler
    {
        IFormViewer FormView { get; }
    }

    public interface IDataController : IController
    {
        Data Data { get; }
    }
    
    public interface IBytesGridViewerDataController<TByteItem> : IDataController, INotifyPropertyChanged
    {
        IBytesGridViewer<TByteItem> ViewGrid { get; set; } 
        DataSubsetWithSelection Rows { get; }
        void MatchCachedRowsToView();
    }
    
    public interface IProjectController
    {
        public Project Project  { get; }
        
        public class ProjectChangedEventArgs
        {
            public enum ProjectChangedType
            {
                Invalid,
                Saved,
                Opened,
                Imported,
                Closing
            }

            public ProjectChangedType ChangeType;
            public Project Project;
            public string Filename;
        }   
                
        delegate void ProjectChangedEvent(object sender, ProjectChangedEventArgs e);

        event ProjectChangedEvent ProjectChanged;
        
        void SaveProject(string filename);
        
        void OpenProject(string fileName);

        void MarkProjectAsUnsaved();
        
        bool ImportRomAndCreateNewProject(string romFilename);
    }
    
    public interface IProjectOpenerHandler : ILongRunningTaskHandler
    {
        public void OnProjectOpenSuccess(string filename, Project project);
        public void OnProjectOpenWarning(string warnings);
        public void OnProjectOpenFail(string fatalError);
        public string AskToSelectNewRomFilename(string error);
    }

    public interface I65816CpuOperations
    {
        void Step(int offset);
        void StepIn(int offset);
        void AutoStepHarsh(int offset);
        void AutoStepSafe(int offset);
        void Mark(int offset);
        public void MarkMany(int offset, int whichIndex);
        
        void SetDataBank(int romOffset, int result);
        void SetDirectPage(int romOffset, int result);
        void SetMFlag(int romOffset, bool value);
        void SetXFlag(int romOffset, bool value);
        
        void AddLabel(int offset, Label label, bool overwrite);
        void AddComment(int offset, string comment, bool overwrite);
    }

    public interface IExportDisassembly
    {
        void UpdateExportSettings(LogWriterSettings selectedSettings);
        void WriteAssemblyOutput();
    }

    public interface ITraceLogImporters
    {
        void ImportBizHawkCdl(string filename);
        long ImportBsnesUsageMap(string fileName);
        long ImportBsnesTraceLogs(string[] fileNames);
    }

    public interface IProjectNavigation
    {
        public int SelectedSnesOffset { get; set; }

        void GoTo(int offset);
        void GoToUnreached(bool end, bool direction);
        void GoToIntermediateAddress(int offset);
        void OnUserChangedSelection(RomByteData newSelection);
    }

    public interface IMainFormController : 
        
        IFormController,
        
        // TODO: shouldn't have the word 'Grid' in here for Main Form controller. refactor
        // either naming or functionality.
        // IBytesGridViewerDataController<RomByteDataGridRow>,
        
        IProjectController,
        I65816CpuOperations, 
        IExportDisassembly, 
        IProjectOpenerHandler, 
        ITraceLogImporters, 
        IProjectNavigation 
    {
        public FlagType CurrentMarkFlag { get; set; }
        
        public bool MoveWithStep { get; set; }
    }
}
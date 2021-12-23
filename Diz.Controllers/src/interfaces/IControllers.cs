using System;
using Diz.Controllers.controllers;
using Diz.Core;
using Diz.Core.commands;
using Diz.Core.export;
using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.Core.util;

// NOTE: lots of these interfaces were created temporarily for major refactoring.
// when that process is finished, we should probably take a pass here to simplify anything
// that ended up being unnecessary or over-complicated

namespace Diz.Controllers.interfaces
{
    public interface IController
    {
        
    }

    public interface ICloseHandler
    {
        public event EventHandler Closed;
    }

    public interface IFormController : IController, ICloseHandler, IShowable
    {
        
    }

    public interface IDataController : IController
    {
        Data Data { get; }
    }
    
    #if DIZ_3_BRANCH
    public interface IBytesGridDataController<TRow, TItem> : IDataController, INotifyPropertyChanged
    {
        IBytesGridViewer<TItem> ViewGrid { get; set; } 
        DataSubsetWithSelection<TRow, TItem> DataSubset { get; }
        void MatchCachedRowsToView();
    }
    #endif

    public interface IStartFormController : IFormController
    {
        public IStartFormViewer View { get; }
    }

    public interface IProjectOpener
    {
        public void OpenFileWithNewView(string filename);
        public void OpenNewViewOfLastLoadedProject();
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
        
        Project OpenProject(string filename, bool showPopupAlertOnLoaded);
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
        // void OnUserChangedSelection(ByteEntry newSelection);
    }

    public interface ILabelImporter
    {
        void ImportLabelsCsv(ILabelEditorView labelEditor, bool replaceAll);
    }

    #if DIZ_3_BRANCH
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
        IProjectNavigation,
        ILabelImporter
    {
        public FlagType CurrentMarkFlag { get; set; }
        public bool MoveWithStep { get; set; }
        
        void SetProject(string filename, Project project);
    }
    #endif

    public interface IMarkManyController : IController
    {
        IDataRange DataRange { get; }
        IReadOnlySnesRomBase Data { get; }
        MarkCommand GetMarkCommand();
    }

    public interface ILogCreatorSettingsEditorController : IController, ICloseHandler, INotifyPropertyChangedExt
    {
        internal enum PromptCreateDirResult
        {
            AlreadyExists,
            DontWantToCreateItNow,
            DidCreateItNow,
        }
    
        ILogCreatorSettingsEditorView View { get; set; }
    
        LogWriterSettings Settings { get; set; }

        string GetSampleOutput();

        bool ValidateFormat(string formatStr);
        bool EnsureSelectRealOutputDirectory(bool forcePrompt = false);
    }
}
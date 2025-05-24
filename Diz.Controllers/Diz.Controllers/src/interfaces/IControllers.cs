using System;
using System.Collections.Generic;
using System.ComponentModel;
using Diz.Controllers.controllers;
using Diz.Core;
using Diz.Core.commands;
using Diz.Core.export;
using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.Core.util;
using Diz.Cpu._65816;
using JetBrains.Annotations;

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
    
    public interface IProjectController : 
        ITraceLogImporters, 
        IFixInstructionUtils, 
        IFixMisalignedFlags
    {
        // diz3.0 is going to need some major surgery from this one.

        public Project Project { get; }
        
        IViewFactory ViewFactory { get; }
        
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

        IProjectView ProjectView { get; set; }

        bool OpenProject(string filename);  // older signature
        string SaveProject(string filename); // older signature. new should return void

        bool ImportRomAndCreateNewProject(string romFilename);
        void ImportLabelsCsv(ILabelEditorView labelEditor, bool replaceAll);
        void SelectOffset(int offset, ISnesNavigation.HistoryArgs historyArgs = null);

        bool ConfirmSettingsThenExportAssembly();
        bool ExportAssemblyWithCurrentSettings();

        void MarkChanged(); // rename to MarkUnsaved or similar in Diz3.0
    }
    
    public interface IProjectOpenerHandler : ILongRunningTaskHandler
    {
        public void OnProjectOpenSuccess(string filename, Project project);
        public void OnProjectOpenWarnings(IReadOnlyList<string> warnings);
        public void OnProjectOpenFail(string fatalError);
        public string AskToSelectNewRomFilename(string error);
        
        Project OpenProject(string filename, bool showPopupAlertOnLoaded);
    }

    public interface IExportDisassembly
    {
        void UpdateExportSettings(LogWriterSettings selectedSettings);
        void WriteAssemblyOutput();
    }

    public interface IFixInstructionUtils
    {
        // probably combine this with something else.
        // not sure this should really be an interface but...
        bool RescanForInOut();
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

    public interface IMarkManyController<out TDataSource> : IController
    {
        IDataRange DataRange { get; }
        TDataSource Data { get; }
        MarkCommand GetMarkCommand();
    }

    public interface ILogCreatorSettingsEditorController : IController, ICloseHandler, INotifyPropertyChangedExt
    {
        ILogCreatorSettingsEditorView View { get; set; }
    
        LogWriterSettings Settings { get; set; }

        public string KeepPathsRelativeToThisPath { get; set; }

        bool PromptSetupAndValidateExportSettings();

        bool EnsureSelectRealOutputDirectory(bool forcePrompt = false);
        string GetSampleOutput();
    }
    
    
    public interface IDizAppSettings : INotifyPropertyChanged
    {
        string LastProjectFilename { get; set; }
        bool OpenLastFileAutomatically { get; set; }
        string LastOpenedFile { get; set; }
    }

    public interface IDizDocument : INotifyPropertyChanged
    {
        Project Project { get; set; }
        string LastProjectFilename { get; set; }
        public BindingList<NavigationEntry> NavigationHistory { get; set; }
    }
}
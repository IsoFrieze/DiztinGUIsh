using System;
using Diz.Core.export;
using Diz.Core.model;
using Diz.Core.util;
using DiztinGUIsh.controller;
using DiztinGUIsh.window;
using Equin.ApplicationFramework;

namespace DiztinGUIsh.window2
{
    public interface IViewer
    {

    }

    public interface IFormViewer : IViewer
    {
        public event EventHandler Closed;
    }
    
    public interface IBytesGridViewer<TByteItem> : IViewer
    {
        // get the number base that will be used to display certain items in the grid
        public Util.NumberBase DataGridNumberBase { get; }
        TByteItem SelectedRomByteRow { get; }
        public BindingListView<TByteItem> DataSource { get; set; }
    }

    public interface IBytesFormViewer : IFormViewer
    {
        
    }
    
    
    
    // --------------------------------
    
    
    

    public interface IController
    {
        IViewer View { get; }

        public event EventHandler Closed;
    }

    public interface IDataController : IController
    {
        Data Data { get; }
    }
    
    public interface IBytesGridViewerDataController<TByteItem> : IDataController
    {
        IBytesGridViewer<TByteItem> ViewGrid { get; set; }
    }
    
    public interface IProjectController
    {
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
        
        void OpenProject(string fileName);

        void MarkProjectAsUnsaved();
        
        bool ImportRomAndCreateNewProject(string romFilename);

        void SaveProject(string filename);
    }

    public interface I65816CpuOperations
    {
        void Step(int offset);
        void StepIn(int offset);
        void AutoStepHarsh(int offset);
        void AutoStepSafe(int offset);
        void Mark(int offset);
        public void MarkMany(int offset, int whichIndex);
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
    }
    
    public interface IMainFormController : IBytesGridViewerDataController<RomByteDataGridRow>, IProjectController, I65816CpuOperations, IExportDisassembly, IProjectOpener, ITraceLogImporters, IProjectNavigation
    {
        public DizDocument Document  { get; }

        public FlagType CurrentMarkFlag { get; set; }
        
        public bool MoveWithStep { get; set; }
    }
}
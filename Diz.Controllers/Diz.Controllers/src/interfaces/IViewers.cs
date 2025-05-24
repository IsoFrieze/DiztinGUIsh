using System;
using System.Collections.Generic;
using Diz.Controllers.controllers;
using Diz.Core.commands;
using Diz.Core.Interfaces;
using JetBrains.Annotations;

namespace Diz.Controllers.interfaces;

public interface IFormViewer : ICloseHandler, IShowable
{
    
}

public interface IShowable
{
    void Show();
}
    
public interface IFocusable
{
    void BringFormToTop(); // not the most elegant thing
}

public interface IStartFormViewer : IProjectOpenRequester, IFormViewer
{
        
}

public record ProjectOpenEventArgs
{
    public string Filename { get; init; }
    public bool OpenLast { get; init; }
}
    
public interface IProjectOpenRequester
{
    public event EventHandler<ProjectOpenEventArgs> ProjectOpenRequested;
}

public interface ICloseable
{
    void Close();
}

public interface IModalDialog
{
    /// <summary>
    /// Show the dialog to the user and wait for them to complete
    /// the steps on the view
    /// </summary>
    /// <returns>True if steps were completed and we have a valid result</returns>
    bool PromptDialog();
}
    

public interface IProgressView : IFormViewer, ICloseable, IModalDialog, IProgress<int> {
    public bool IsMarquee { get; set; }
    public string TextOverride { get; set; }
    bool Visible { get; set; }
        
    /// <summary>
    /// Signal that a job (potentially running in another task/thread) has completed.
    /// CAUTION: Implementers should use thread-safety measures, this may be called
    /// from a different thread than any other calls 
    /// </summary>
    void SignalJobIsDone();
}

// diz2 version (use it)
public interface IMarkManyView<TDataSource> : IModalDialog 
    where TDataSource : IRomSize
{
    MarkCommand.MarkManyProperty Property { get; set; }
    object GetPropertyValue();
    [CanBeNull] IMarkManyController<TDataSource> Controller { get; set; }

    void AttemptSetSettings(Dictionary<MarkCommand.MarkManyProperty, object> settings);
    Dictionary<MarkCommand.MarkManyProperty, object> SaveCurrentSettings();
}


#if DIZ_3_BRANCH
    public interface IBytesGridViewer<TByteItem> : IRowBaseViewer<TByteItem>, IViewer
    {
        public List<TByteItem> DataSource { get; set; }
        int TargetNumberOfRowsToShow { get; }

        void SelectRow(int row);
        

        void BeginEditingSelectionComment();
        void BeginEditingSelectionLabel();
        
        public class SelectedOffsetChangedEventArgs : EventArgs
        {
            public TByteItem Row { get; init; }
            public int RowIndex { get; init; }
        }

        public delegate void SelectedOffsetChange(object sender, SelectedOffsetChangedEventArgs e);

        public event SelectedOffsetChange SelectedOffsetChanged;
    }
#endif
    
public interface ILabelEditorView : IFormViewer, IFocusable
{
    [CanBeNull] public IProjectController ProjectController { get; set; }
        
    string PromptForCsvFilename();
    void RepopulateFromData();
    void ShowLineItemError(string exMessage, int errLine);
    void RebindProject();
}
    
public interface IImportRomDialogView
{
    IImportRomDialogController Controller { get; set; }
    public List<string> EnabledVectorTableEntries { get; }
        
    bool ShowAndWaitForUserToConfirmSettings();
    void RefreshUi();
}

public interface ICommonGui
{
    bool PromptToConfirmAction(string msg);
        
    void ShowError(string msg);
    void ShowWarning(string msg);
    void ShowMessage(string msg);
}
    
public interface ILogCreatorSettingsEditorView : IFormViewer
{
    ILogCreatorSettingsEditorController Controller { get; set; }
    
    [CanBeNull] string PromptForLogPathFromFileOrFolderDialog(bool askForFile);
    bool PromptCreatePath(string buildFullOutputPath, string extraMsg);
        
    /// <summary>
    /// Main method, return true if we showed the dialog and edited successfully.
    /// </summary>
    /// <returns></returns>
    bool PromptEditAndConfirmSettings();
}
    
#if DIZ_3_BRANCH
    public interface IDataGridEditorForm : IFormViewer, IProjectView
    {
        IMainFormController MainFormController { get; set; }
    }
#endif
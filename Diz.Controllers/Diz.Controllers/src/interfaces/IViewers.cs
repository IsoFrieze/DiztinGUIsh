using System;
using System.Collections.Generic;
using Diz.Controllers.controllers;
using Diz.Core.commands;
using Diz.Core.Interfaces;
using JetBrains.Annotations;

namespace Diz.Controllers.interfaces;

public interface IFormViewer
{
    public event EventHandler OnFormClosed;
    // void Close();
    void Show();
    void BringFormToTop();
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
    

// new-ui plan step 6 (threading rewrite): a progress view is now a plain non-modal window
// the long-running-task handler shows while work runs on a Task and closes on completion.
// GONE (with ProgressBarWorker): IModalDialog.PromptDialog (the blocking ShowDialog),
// IsVisible() (the worker's spin-wait predicate), and SignalJobIsDone() (the cross-thread
// close signal). Report(int) is marshalled to the UI thread by the handler's IProgress<int>.
public interface IProgressView : IFormViewer, IProgress<int> {
    public bool IsMarquee { get; set; }
    public string TextOverride { get; set; }

    /// <summary>Close/hide the progress window. Called on the UI thread when the work finishes.</summary>
    void Close();
}

// diz2 version (use it)
public interface IMarkManyView<TDataSource> : IModalDialog 
    where TDataSource : IRomSize
{
    MarkCommand.MarkManyProperty Property { get; set; }
    object GetPropertyValue();
    [CanBeNull] IMarkManyController<TDataSource> Controller { get; set; }

    void RestoreUiFromSettings(MarkManyViewSettings settings);
    MarkManyViewSettings BuildSettingsFromUi();
}


public interface ILabelEditorView : IFormViewer
{
    // a lot of these fields/methods shouldn't be done this way
    // (step 4 of the new-ui plan removed the prompt-shaped members: file paths now come
    // from IFileDialogService in the view layer, and import errors are surfaced by
    // ProjectController via ICommonGui.)

    void SetProjectController([CanBeNull] IProjectController projectController);
    void RepopulateFromData(); // keep
    void RebindProject(); // keep

    void FocusOrCreateLabelAtSelectedRomOffsetIa();
    void FocusOrCreateLabelAtRomOffsetIa(int selectedOffset);
    void FocusOrCreateLabelAtSnesAddress(int snesAddress);
}

public interface IRegionListView : IFormViewer
{
    void SetProjectController([CanBeNull] IProjectController projectController);
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
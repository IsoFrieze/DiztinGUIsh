using System;
using System.Collections.Generic;
using Diz.Controllers.controllers;
using Diz.Core.commands;
using Diz.Core.Interfaces;
using Diz.Ui.ViewModels.Navigation;
using JetBrains.Annotations;

namespace Diz.Controllers.interfaces;

public interface IFormViewer
{
    public event EventHandler OnFormClosed;
    // void Close();
    void Show();
    void BringFormToTop();
}

// a progress view is a plain non-modal window the long-running-task handler shows while work
// runs on a Task, and closes on completion. There is deliberately no blocking "show and wait"
// call, no visibility predicate to spin on, and no cross-thread done signal: Report(int) is
// marshalled to the UI thread by the handler's IProgress<int>.
public interface IProgressView : IFormViewer, IProgress<int> {
    public bool IsMarquee { get; set; }
    public string TextOverride { get; set; }

    /// <summary>Close/hide the progress window. Called on the UI thread when the work finishes.</summary>
    void Close();
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

/// <summary>
/// The back/forward history window. Modelled on <see cref="IRegionListView"/>, the closest analog:
/// LONG-LIVED. The main window resolves exactly one of these in its constructor and keeps it for
/// the application's lifetime, and the window HIDES rather than closes -- so
/// <see cref="IFormViewer.OnFormClosed"/> is declared but never raised, and Show() after a close
/// re-shows the same window with its scroll position intact.
///
/// IT DOES NOT OWN THE HISTORY. Everything this view shows -- the entries and which one the user
/// is on -- lives in the <see cref="NavigationHistoryViewModel"/> the host assigns below, because
/// back/forward are main-window menu commands that must work with this window closed, or never
/// opened at all. Resolving a view is therefore NOT what makes navigation work; it only makes it
/// visible.
///
/// CONSTRUCTION MUST STAY INERT. The main window resolves this before its message loop is running,
/// and the Avalonia backend may not initialize its platform that early -- so neither construction
/// nor either property below may touch a toolkit. Both are recorded and applied when Show() first
/// builds the window. (Same constraint documented on AvaloniaRegionListView.)
/// </summary>
public interface INavigationHistoryView : IFormViewer
{
    /// <summary>
    /// The history to display. Assigned by the host, which OWNS it: this view borrows it and must
    /// never dispose it. Null detaches.
    /// </summary>
    [CanBeNull] NavigationHistoryViewModel ViewModel { get; set; }

    /// <summary>
    /// Overshoot the in-window back/forward buttons ask for. Seeded by the host with the same
    /// number its own back/forward menu commands use, so "go back" means one thing however it is
    /// triggered. Row activation deliberately stays at
    /// <see cref="NavigationHistoryViewModel.NoOvershoot"/>, landing exactly on the row the user
    /// pointed at -- an asymmetry inherited from the old WinForms control.
    /// </summary>
    int BackForwardOvershoot { get; set; }
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
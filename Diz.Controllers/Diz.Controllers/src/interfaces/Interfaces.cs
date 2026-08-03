using System;
using Diz.Controllers.controllers;
using Diz.Controllers.util;
using Diz.Core.model;
// using Diz.Core.model.byteSources;
using Diz.Core.model.snes;

namespace Diz.Controllers.interfaces;

public interface IDizApp
{
    void Run(string initialProjectFileToOpen = "");
}
    
public interface IGridRow<out TItem>
{
    Data Data { get; init; }
    TItem Item { get; }
}

public interface IProjectsManager : ISampleProjectLoader
{
    Project GetProject(string filename);

    public event EventHandler<Project> OnProjectOpened;
    void OpenProjectFile(string filename);

    Project GetLastOpenedProject();
    void OpenLastLoadedProject();
}

public interface ISampleProjectLoader
{
    Project GetSampleProject();
}
    
// note: this is an autofactory, so the names of the methods map to registrations (strings)
public interface IViewFactory
{
    ISnesImportRomView GetSnesImportRomView();
    IProgressView GetProgressBarView();
    IExportSettingsView GetExportSettingsView();
    ILabelEditorView GetLabelEditorView();
    IMarkManyView GetMarkManyView();
    IGotoView GetGotoView();
    IHarshAutoStepView GetHarshAutoStepView();
    IMisalignmentCheckerView GetMisalignmentCheckerView();
    IInOutPointCheckerView GetInOutPointCheckerView();
    IMainGridWindowView GetMainGridWindowView();
    IAboutView GetAboutView();
    IRegionListView GetRegionEditorView();
    INavigationHistoryView GetNavigationHistoryView();
}
    
public interface IControllerFactory
{
    ILargeFilesReaderController GetLargeFileReaderProgressController();
}
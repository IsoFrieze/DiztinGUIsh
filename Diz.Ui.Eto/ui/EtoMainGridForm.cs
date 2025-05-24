using System.ComponentModel;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core.model;
using Diz.LogWriter;
using Eto.Drawing;
using Eto.Forms;
using Label = Eto.Forms.Label;

namespace Diz.Ui.Eto.ui;

public class EtoMainGridForm : Form, IMainGridWindowView
{
    public event EventHandler? OnFormClosed;
    
    private readonly IDizDocument document;
    private readonly IDizAppSettings appSettings; 
    private readonly IViewFactory viewFactory;
    private readonly IProjectController projectController;

    
    public EtoMainGridForm(
        IProjectController projectController,
        IDizAppSettings appSettings, 
        IDizDocument document,
        IViewFactory viewFactory)
    {
        CreateGui();
        
        this.document = document;
        this.appSettings = appSettings;
        this.viewFactory = viewFactory;
        this.projectController = projectController;
        this.projectController.ProjectView = this;

        // TODO
        // aliasList = viewFactory.GetLabelEditorView();
        // aliasList.ProjectController = this.projectController;
            
        this.document.PropertyChanged += Document_PropertyChanged;
        this.projectController.ProjectChanged += ProjectController_ProjectChanged;

        // NavigationForm = new NavigationForm // TODO
        // {
        //     Document = this.document,
        //     SnesNavigation = this,
        // };
        
        Closed += (sender, args) => OnFormClosed?.Invoke(sender, args);
    }

    private void ProjectController_ProjectChanged(object sender, IProjectController.ProjectChangedEventArgs e)
    {
        
    }

    private void Document_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        
    }

    private void CreateGui()
    {
        Title = "Diz";
        ClientSize = new Size(800, 600);
        Content = new Label { Text = "Placeholder form - TODO: IMPLEMENT ME" };
    }

    public ILongRunningTaskHandler.LongRunningTaskHandler TaskHandler =>
        ProgressBarJob.RunAndWaitForCompletion;
    
    public void SelectOffset(int pcOffset, ISnesNavigation.HistoryArgs? historyArgs = null)
    {
        
    }

    public void SelectOffsetWithOvershoot(int pcOffset, int overshootAmount = 0)
    {
        
    }

    public Project Project { get; set; }
    public void OnProjectOpenFail(string errorMsg)
    {

    }

    public void OnProjectSaved()
    {
        
    }

    public void OnExportFinished(LogCreatorOutput.OutputResult result)
    {
        
    }

    public string AskToSelectNewRomFilename(string promptSubject, string promptText)
    {
        return "";
    }

    public void OnProjectOpenWarnings(IEnumerable<string> warnings)
    {
        
    }

    public void BringFormToTop() => Focus();
}
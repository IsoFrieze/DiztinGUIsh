using System.Windows.Forms;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Controllers.util;
using Diz.LogWriter;
using Diz.Ui.Winforms.util;

namespace DiztinGUIsh.window;

public partial class MainWindow : Form, IMainGridWindowView
{
    public MainWindow(
        IProjectController projectController, 
        IDizAppSettings appSettings, 
        IDizDocument document)
    {
        Document = document;
        this.appSettings = appSettings;
        ProjectController = projectController;
        ProjectController.ProjectView = this;

        AliasList = projectController.ViewFactory.GetLabelEditorView();
        AliasList.ProjectController = ProjectController;
        
        Document.PropertyChanged += Document_PropertyChanged;
        ProjectController.ProjectChanged += ProjectController_ProjectChanged;

        NavigationForm = new NavigationForm
        {
            Document = Document,
            SnesNavigation = this,
        };

        InitializeComponent();
    }
        
    private void Init()
    {
        InitMainTable();

        UpdatePanels();
        UpdateUiFromSettings();

        if (appSettings.OpenLastFileAutomatically)
            OpenLastProject();
    }


    private void Document_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DizDocument.LastProjectFilename))
        {
            UpdateUiFromSettings();
        }
    }

    private void ProjectController_ProjectChanged(object sender, IProjectController.ProjectChangedEventArgs e)
    {
        switch (e.ChangeType)
        {
            case IProjectController.ProjectChangedEventArgs.ProjectChangedType.Saved:
                OnProjectSaved();
                break;
            case IProjectController.ProjectChangedEventArgs.ProjectChangedType.Opened:
                OnProjectOpened(e.Filename);
                break;
            case IProjectController.ProjectChangedEventArgs.ProjectChangedType.Imported:
                OnImportedProjectSuccess();
                break;
            case IProjectController.ProjectChangedEventArgs.ProjectChangedType.Closing:
                OnProjectClosing();
                break;
        }

        RebindProject();
    }

    private void OnProjectClosing()
    {
        UpdateSaveOptionStates(saveEnabled: false, saveAsEnabled: false, closeEnabled: false);
    }

    public void OnProjectOpenFail(string errorMsg)
    {
        Document.LastProjectFilename = "";
        ShowError(errorMsg, "Error opening project");
    }

    public void OnProjectSaved()
    {
        UpdateSaveOptionStates(saveEnabled: true, saveAsEnabled: true, closeEnabled: true);
        UpdateWindowTitle();
    }

    public void OnExportFinished(LogCreatorOutput.OutputResult result)
    {
        ShowExportResults(result);
    }

    private void RememberNavigationPoint(int pcOffset, ISnesNavigation.HistoryArgs historyArgs)
    {
        var snesAddress = Project.Data.ConvertPCtoSnes(pcOffset);
        var history = Document.NavigationHistory;
            
        // if our last remembered offset IS the new offset, don't record it again
        // (prevents duplication)
        if (history.Count > 0 && history[history.Count-1].SnesOffset == snesAddress)
            return;

        history.Add(
            new NavigationEntry(
                snesAddress, 
                historyArgs,
                Project.Data
            )
        );
    }

    private void timer1_Tick(object sender, System.EventArgs e)
    {
        // the point of this timer is to throttle the ROM% calculator
        // since it is an expensive calculation. letting it happen attached to UI events
        // would significantly slow the user down.
        //
        // TODO: this is the kind of thing that Rx.net's Throttle function, or 
        // an async task would handle much better. For now, this is fine.
        UpdatePercentageCalculatorCooldown();
    }

    private void UpdatePercentageCalculatorCooldown()
    {
        if (_cooldownForPercentUpdate == -1)
            return;

        if (--_cooldownForPercentUpdate == -1)
            UpdatePercent(forceRecalculate: true);
    }
}
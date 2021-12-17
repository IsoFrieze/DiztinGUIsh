using System.Windows.Forms;
using Diz.Controllers.controllers;
using Diz.Core.export;
using Diz.LogWriter;
using DiztinGUIsh.controller;
using DiztinGUIsh.Properties;
using DiztinGUIsh.window.dialog;

namespace DiztinGUIsh.window
{
    public partial class MainWindow : Form, IProjectView
    {
        public MainWindow()
        {
            ProjectController = new ProjectController {
                ProjectView = this,
            };

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

            AliasList = new AliasList(this);

            UpdatePanels();
            UpdateUiFromSettings();

            if (Settings.Default.OpenLastFileAutomatically)
                OpenLastProject();
        }


        private void Document_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DizDocument.LastProjectFilename))
            {
                UpdateUiFromSettings();
            }
        }

        private void ProjectController_ProjectChanged(object sender, ProjectController.ProjectChangedEventArgs e)
        {
            switch (e.ChangeType)
            {
                case ProjectController.ProjectChangedEventArgs.ProjectChangedType.Saved:
                    OnProjectSaved();
                    break;
                case ProjectController.ProjectChangedEventArgs.ProjectChangedType.Opened:
                    OnProjectOpened(e.Filename);
                    break;
                case ProjectController.ProjectChangedEventArgs.ProjectChangedType.Imported:
                    OnImportedProjectSuccess();
                    break;
                case ProjectController.ProjectChangedEventArgs.ProjectChangedType.Closing:
                    OnProjectClosing();
                    break;
            }

            RebindProject();
        }

        private void OnProjectClosing()
        {
            UpdateSaveOptionStates(saveEnabled: false, saveAsEnabled: false, closeEnabled: false);
        }

        public void OnProjectOpened(string filename)
        {
            if (visualForm != null)
                visualForm.Project = Project;

            // TODO: do this with aliaslist too.

            UpdateSaveOptionStates(saveEnabled: true, saveAsEnabled: true, closeEnabled: true);
            RefreshUi();

            Document.LastProjectFilename = filename; // do this last.
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

        /// <summary>
        /// shows the assembly export settings editor UI 
        /// </summary>
        /// <returns>non-null and valid settings object if all user selections are valid</returns>
        private bool PromptForExportSettingsAndConfirmation()
        {
            if (Project == null)
                return false;
            
            // might want to rethink the directory data being passed around, flow is kinda awkward,
            // can surely be simplified.
            var initialPath = Project?.Session?.ProjectDirectory ?? "";
            var exportDialog = new ExportDisassembly(Project.LogWriterSettings)
            {
                InitialDirForOutput = initialPath,
                KeepPathsRelativeToThisPath = initialPath,
            };

            var newValidatedSettings = GetExportSettingsFromDialog(exportDialog);
            if (newValidatedSettings == null) 
                return false;
            
            ProjectController.UpdateExportSettings(newValidatedSettings);
            return true;
        }
        
        private static LogWriterSettings GetExportSettingsFromDialog(ExportDisassembly export)
        {
            return export.ShowDialog() != DialogResult.OK ? null : export.ProposedSettings;
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
}
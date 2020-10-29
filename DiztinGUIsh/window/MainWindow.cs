using System.Windows.Forms;
using Diz.Core.export;
using Diz.Core.model;
using DiztinGUIsh.controller;
using DiztinGUIsh.Properties;

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

        public void OnExportFinished(LogCreator.OutputResult result)
        {
            ShowExportResults(result);
        }

        private LogWriterSettings? PromptForExportSettingsAndConfirmation()
        {
            // TODO: use the controller to update the project settings from a new one we build
            // don't update directly.
            // probably make our Project property be fully readonly/const/whatever [ReadOnly] attribute

            var selectedSettings = ExportDisassembly.ConfirmSettingsAndAskToStart(Project);
            if (!selectedSettings.HasValue)
                return null;

            var settings = selectedSettings.Value;

            ProjectController.UpdateExportSettings(selectedSettings.Value);

            return settings;
        }
    }
}
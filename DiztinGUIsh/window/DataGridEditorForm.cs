using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using Diz.Core.export;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.util;
using DiztinGUIsh.controller;
using DiztinGUIsh.Properties;
using DiztinGUIsh.util;
using DiztinGUIsh.window.dialog;

namespace DiztinGUIsh.window
{
    public interface IDataGridEditorForm : IFormViewer, IProjectView
    {
        
    }
    
    public partial class DataGridEditorForm : Form, IDataGridEditorForm
    {
        // sub windows
        public AliasList AliasList { get; protected set; }
        public VisualizerForm VisualForm { get; protected set; }
        
        private IMainFormController mainFormController;
        public Project Project { get; protected set; }

        // not sure if this will be the final place this lives. OK for now. -Dom
        public IMainFormController MainFormController
        {
            get => mainFormController;
            set
            {
                mainFormController = value;
                if (Project != null)
                {
                    Project.PropertyChanged += Project_PropertyChanged;
                    // TODO
                    //if (Project.Data != null) 
                    //    Project.Data.PropertyChanged += DataOnPropertyChanged;
                }

                mainFormController.ProjectChanged += ProjectController_ProjectChanged;
            }
        }
        
        // a class we create that controls just the data grid usercontrol we host
        private RomByteDataBindingGridController DataGridDataController { get; set; }
        
        public DataGridEditorForm()
        {
            InitializeComponent();
        }

        private void Init()
        {
            DataGridDataController = new RomByteDataBindingGridController
            {
                ViewGrid = dataGridEditorControl1,
                Data = MainFormController.Project?.Data,
            };
            dataGridEditorControl1.DataController = DataGridDataController;
            
            dataGridEditorControl1.SelectedOffsetChanged += DataGridEditorControl1OnSelectedOffsetChanged;

            AliasList = new AliasList(this);
            
            UpdateUiFromSettings();

            if (Settings.Default.OpenLastFileAutomatically)
                OpenLastProject();
        }

        private void DataGridEditorControl1OnSelectedOffsetChanged(object sender, IBytesGridViewer<ByteEntry>.SelectedOffsetChangedEventArgs e)
        {
            // called when the user clicks a different cell in the child data grid
            MainFormController.OnUserChangedSelection(e.Row);
        }

        private void ProjectController_ProjectChanged(object sender, IProjectController.ProjectChangedEventArgs e)
        {
            Project = e.Project;
            
            switch (e.ChangeType)
            {
                case IProjectController.ProjectChangedEventArgs.ProjectChangedType.Saved:
                    UpdateSaveOptionStates(saveEnabled: true, saveAsEnabled: true, closeEnabled: true);
                    break;
                case IProjectController.ProjectChangedEventArgs.ProjectChangedType.Opened:
                    UpdateSaveOptionStates(saveEnabled: true, saveAsEnabled: true, closeEnabled: true);
                    ProjectsController.LastOpenedProjectFilename = e.Filename; // do this last.
                    break;
                case IProjectController.ProjectChangedEventArgs.ProjectChangedType.Imported:
                    OnImportedProjectSuccess();
                    break;
                case IProjectController.ProjectChangedEventArgs.ProjectChangedType.Closing:
                    UpdateSaveOptionStates(saveEnabled: false, saveAsEnabled: false, closeEnabled: false);
                    break;
            }
            
            RebindProject();
        }

        public void OnProjectOpenFail(string errorMsg)
        {
            ProjectsController.LastOpenedProjectFilename = "";
            ShowError(errorMsg, "Error opening project");
        }

        public void OnExportFinished(LogCreatorOutput.OutputResult result)
        {
            ExportDisassembly.ShowExportResults(result);
        }
        
        private LogWriterSettings? PromptForExportSettingsAndConfirmation()
        {
            // TODO: use the controller to update the project settings from a new one we build
            // don't update directly.
            // probably make our Project property be fully readonly/const/whatever [ReadOnly] attribute

            var selectedSettings = ExportDisassembly.ConfirmSettingsAndAskToStart(Project);
            if (selectedSettings == null)
                return null;

            var settings = selectedSettings.Value;

            MainFormController.UpdateExportSettings(selectedSettings.Value);

            return settings;
        }

        private void Project_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Project.Session == null)
                return;
            
            if (e.PropertyName == nameof(IProjectSession.UnsavedChanges) || 
                e.PropertyName == nameof(IProjectSession.ProjectFileName)) 
            {
                Text =
                    (Project.Session.UnsavedChanges ? "*" : "") +
                    (Project.Session.ProjectFileName == "" ? "New Project" : Project.Session.ProjectFileName) +
                    " - DiztinGUIsh";
            }
        }

        private void DataOnPropertyChanged(object _, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ByteEntry.TypeFlag)) 
                return;
            
            percentComplete.Text = "";
            if (Project?.Data == null || Project.Data.GetRomSize() <= 0) 
                return;
                    
            int totalUnreached1 = 0, size1 = Project.Data.GetRomSize();
            for (var i1 = 0; i1 < size1; i1++)
                if (Project.Data.GetFlag(i1) == FlagType.Unreached)
                    totalUnreached1++;
            
            var reached1 = size1 - totalUnreached1;
            percentComplete.Text = $"{reached1 * 100.0 / size1:N3}% ({reached1:D}/{size1:D})";
        }

        #region Actions
        private void OpenLastProject()
        {
            if (ProjectsController.LastOpenedProjectFilename == "")
                return;

            // safeguard: if we crash opening this project,
            // then next time we load make sure we don't try it again.
            // this will be reset later
            var projectToOpen = ProjectsController.LastOpenedProjectFilename;
            ProjectsController.LastOpenedProjectFilename = "";
            MainFormController.OpenProject(projectToOpen);
        }

        private void OpenProject()
        {
            if (!PromptForOpenProjectFilename()) 
                return;

            MainFormController.OpenProject(openProjectFile.FileName);
        }

        private void CreateNewProject()
        {
            if (!PromptContinueEvenIfUnsavedChanges())
                return;

            var romFilename = PromptForOpenFilename();
            if (romFilename == "")
                return;

            MainFormController.ImportRomAndCreateNewProject(openFileDialog.FileName);
        }
        
        private void ExportAssembly()
        {
            var adjustedSettings = PromptForExportSettingsAndConfirmation();
            if (!adjustedSettings.HasValue)
                return;

            MainFormController.UpdateExportSettings(adjustedSettings.Value);
            MainFormController.WriteAssemblyOutput();
        }

        private void FixMisalignedInstructions()
        {
            if (!PromptForMisalignmentCheck())
                return;
            
            var count = Project.Data.FixMisalignedFlags();

            if (count > 0)
                MainFormController.MarkProjectAsUnsaved();

            ShowInfo($"Modified {count} flags!", "Done!");
        }

        private void RescanForInOut()
        {
            if (!PromptForInOutChecking()) 
                return;

            Project.Data.RescanInOutPoints();
            MainFormController.MarkProjectAsUnsaved();
            
            ShowInfo("Scan complete!", "Done!");
        }

        private void SaveProject()
        {
            MainFormController.SaveProject(Project?.Session?.ProjectFileName);
        }

        private void ShowVisualizerForm()
        {
            VisualForm ??= new VisualizerForm(this);
            VisualForm.Show();
        }

        private void ShowCommentList()
        {
            AliasList.Show();
        }

        private void ToggleOpenLastProjectEnabled()
        {
            // TODO: Should "Settings" live here?
            Settings.Default.OpenLastFileAutomatically = openLastProjectAutomaticallyToolStripMenuItem.Checked;
            Settings.Default.Save();
            UpdateUiFromSettings();
        }
        #endregion
        
        #region Importers

        public void SelectOffset(int offset)
        {
            MainFormController.SelectedSnesOffset = offset;
        }

        public IImportRomDialogView GetImportView() => new ImportRomDialog();

        private void ImportBizhawkCdl()
        {
            var filename = PromptOpenBizhawkCDLFile();
            if (filename != null && filename == "") return;
            ImportBizHawkCdl(filename);
        }

        private void ImportBizHawkCdl(string filename)
        {
            try
            {
                MainFormController.ImportBizHawkCdl(filename);
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void ImportBsnesTraceLogText()
        {
            if (!PromptForImportBsnesTraceLogFile()) return;
            var (numModifiedFlags, numFiles) = ImportBsnesTraceLogs();
            
            ReportNumberFlagsModified(numModifiedFlags, numFiles);
        }

        private void ImportBsnesUsageMap()
        {
            if (openUsageMapFile.ShowDialog() != DialogResult.OK)
                return;

            var numModifiedFlags = MainFormController.ImportBsnesUsageMap(openUsageMapFile.FileName);

            ShowInfo($"Modified total {numModifiedFlags} flags!", "Done");
        }

        private (long numBytesModified, int numFiles) ImportBsnesTraceLogs()
        {
            var numBytesModified = MainFormController.ImportBsnesTraceLogs(openTraceLogDialog.FileNames);
            return (numBytesModified, openTraceLogDialog.FileNames.Length);
        }

        private void ImportBsnesBinaryTraceLog()
        {
            new BsnesTraceLogBinaryMonitorForm(this).ShowDialog();
            importCDLToolStripMenuItem.Enabled = true;
            labelListToolStripMenuItem.Enabled = true;
        }

        private void OnImportedProjectSuccess()
        {
            UpdateSaveOptionStates(saveEnabled: false, saveAsEnabled: true, closeEnabled: true);
            importCDLToolStripMenuItem.Enabled = true;
            labelListToolStripMenuItem.Enabled = true;
        }
        #endregion
        
        #region ReadOnlyHelpers
        
        private bool RomDataPresent()
        {
            return Project?.Data?.GetRomSize() > 0;
        }
        #endregion
        
        private void RebindProject()
        {
            // TODO: replace all this with OnNotifyPropertyChanged stuff eventually
            
            if (DataGridDataController != null)
                DataGridDataController.Data = Project?.Data;
            
            AliasList?.RebindProject();
            
            if (VisualForm != null) 
                VisualForm.Project = Project;
        }

        private void UpdateUiFromSettings()
        {
            var lastOpenedFilePresent = Settings.Default.LastOpenedFile != "";

            toolStripOpenLast.Enabled = lastOpenedFilePresent;
            toolStripOpenLast.Text = "Open Last File";
            if (lastOpenedFilePresent)
                toolStripOpenLast.Text += $" ({Path.GetFileNameWithoutExtension(Settings.Default.LastOpenedFile)})";

            openLastProjectAutomaticallyToolStripMenuItem.Checked = Settings.Default.OpenLastFileAutomatically;
        }

        private void UpdateBase(Util.NumberBase noBase)
        {
            // tableControl.DisplayBase = noBase; // TODO: move this out of mainwindow, into table class.
            
            decimalToolStripMenuItem.Checked = noBase == Util.NumberBase.Decimal;
            hexadecimalToolStripMenuItem.Checked = noBase == Util.NumberBase.Hexadecimal;
            binaryToolStripMenuItem.Checked = noBase == Util.NumberBase.Binary;
        }

        public void UpdateMarkerLabel()
        {
            currentMarker.Text = $"Marker: {mainFormController.CurrentMarkFlag.ToString()}";
        }

        /*private void UpdateImporterEnabledStatus()
        {
            importUsageMapToolStripMenuItem.Enabled = importerMenuItemsEnabled;
            importCDLToolStripMenuItem.Enabled = importerMenuItemsEnabled;
            importTraceLogBinary.Enabled = importerMenuItemsEnabled;
            importTraceLogText.Enabled = importerMenuItemsEnabled;
        }*/

        public void UpdateSaveOptionStates(bool saveEnabled, bool saveAsEnabled, bool closeEnabled)
        {
            saveProjectToolStripMenuItem.Enabled = saveEnabled;
            saveProjectAsToolStripMenuItem.Enabled = saveAsEnabled;
            closeProjectToolStripMenuItem.Enabled = closeEnabled;

            exportLogToolStripMenuItem.Enabled = RomDataPresent();
            importCDLToolStripMenuItem.Enabled = RomDataPresent();
            labelListToolStripMenuItem.Enabled = RomDataPresent();
        }

        #region Simple Event Handlers
        private void MainWindow_FormClosing(object sender, FormClosingEventArgs e) =>
            e.Cancel = !PromptContinueEvenIfUnsavedChanges();

        private void MainWindow_Load(object sender, EventArgs e) => Init();
        private void newProjectToolStripMenuItem_Click(object sender, EventArgs e) => CreateNewProject();
        private void openProjectToolStripMenuItem_Click(object sender, EventArgs e) => OpenProject();

        private void saveProjectToolStripMenuItem_Click(object sender, EventArgs e) => SaveProject();

        private void saveProjectAsToolStripMenuItem_Click(object sender, EventArgs e) => PromptForFilenameToSave();
        private void exportLogToolStripMenuItem_Click(object sender, EventArgs e) => ExportAssembly();
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e) => new About().ShowDialog();
        private void exitToolStripMenuItem_Click(object sender, EventArgs e) => Application.Exit();
        
        private void decimalToolStripMenuItem_Click(object sender, EventArgs e) => 
            UpdateBase(Util.NumberBase.Decimal);

        private void hexadecimalToolStripMenuItem_Click(object sender, EventArgs e) =>
            UpdateBase(Util.NumberBase.Hexadecimal);

        private void binaryToolStripMenuItem_Click(object sender, EventArgs e) => 
            UpdateBase(Util.NumberBase.Binary);
        
        private void importTraceLogBinary_Click(object sender, EventArgs e) => ImportBsnesBinaryTraceLog();
        private void addLabelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // BeginAddingLabel();
        }

        private void visualMapToolStripMenuItem_Click(object sender, EventArgs e) => ShowVisualizerForm();
        
        #endregion
        
        #region Simple Event Handlers Part 2

        public int SelectedSnesOffset
        {
            get => MainFormController.SelectedSnesOffset;
            set => MainFormController.SelectedSnesOffset = value;
        }
        
        private void stepOverToolStripMenuItem_Click(object sender, EventArgs e) 
            => MainFormController.Step(SelectedSnesOffset);

        private void stepInToolStripMenuItem_Click(object sender, EventArgs e) => MainFormController.StepIn(SelectedSnesOffset);
        private void autoStepSafeToolStripMenuItem_Click(object sender, EventArgs e) => MainFormController.AutoStepSafe(SelectedSnesOffset);
        private void autoStepHarshToolStripMenuItem_Click(object sender, EventArgs e) => MainFormController.AutoStepHarsh(SelectedSnesOffset);
        private void gotoToolStripMenuItem_Click(object sender, EventArgs e) => MainFormController.GoTo(PromptForGotoOffset());

        private void gotoIntermediateAddressToolStripMenuItem_Click(object sender, EventArgs e) =>
            MainFormController.GoToIntermediateAddress(SelectedSnesOffset);

        private void gotoFirstUnreachedToolStripMenuItem_Click(object sender, EventArgs e) => 
            MainFormController.GoToUnreached(true, true);

        private void gotoNearUnreachedToolStripMenuItem_Click(object sender, EventArgs e) =>
            MainFormController.GoToUnreached(false, false);

        private void gotoNextUnreachedToolStripMenuItem_Click(object sender, EventArgs e) => 
            MainFormController.GoToUnreached(false, true);
        
        private void markOneToolStripMenuItem_Click(object sender, EventArgs e) => 
            MainFormController.Mark(SelectedSnesOffset);
        
        private void markManyToolStripMenuItem_Click(object sender, EventArgs e) => 
            MainFormController.MarkMany(SelectedSnesOffset, 7);
        private void setDataBankToolStripMenuItem_Click(object sender, EventArgs e) => 
            MainFormController.MarkMany(SelectedSnesOffset, 8);
        private void setDirectPageToolStripMenuItem_Click(object sender, EventArgs e) => 
            MainFormController.MarkMany(SelectedSnesOffset, 9);
        private void toggleAccumulatorSizeMToolStripMenuItem_Click(object sender, EventArgs e) => 
            MainFormController.MarkMany(SelectedSnesOffset, 10);
        private void toggleIndexSizeToolStripMenuItem_Click(object sender, EventArgs e) => 
            MainFormController.MarkMany(SelectedSnesOffset, 11);
        
        private void addCommentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // BeginEditingSelectionComment();
        }
        
        private void SetMarkerLabel(FlagType flag)
        {
            MainFormController.CurrentMarkFlag = flag;
            UpdateMarkerLabel(); // TODO: get this from an event fire
        }

        private void unreachedToolStripMenuItem_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Unreached);
        private void opcodeToolStripMenuItem_Click(object sender, EventArgs e) => 
            SetMarkerLabel(FlagType.Opcode);
        private void operandToolStripMenuItem_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Operand);
        private void bitDataToolStripMenuItem_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Data8Bit);
        private void graphicsToolStripMenuItem_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Graphics);
        private void musicToolStripMenuItem_Click(object sender, EventArgs e) => 
            SetMarkerLabel(FlagType.Music);
        private void emptyToolStripMenuItem_Click(object sender, EventArgs e) => 
            SetMarkerLabel(FlagType.Empty);
        private void bitDataToolStripMenuItem1_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Data16Bit);
        private void wordPointerToolStripMenuItem_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Pointer16Bit);
        private void bitDataToolStripMenuItem2_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Data24Bit);
        private void longPointerToolStripMenuItem_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Pointer24Bit);
        private void bitDataToolStripMenuItem3_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Data32Bit);
        private void dWordPointerToolStripMenuItem_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Pointer32Bit);
        private void textToolStripMenuItem_Click(object sender, EventArgs e) 
            => SetMarkerLabel(FlagType.Text);
        
        private void fixMisalignedInstructionsToolStripMenuItem_Click(object sender, EventArgs e) =>
            FixMisalignedInstructions();

        private void moveWithStepToolStripMenuItem_Click(object sender, EventArgs e) => 
            mainFormController.MoveWithStep = !mainFormController.MoveWithStep;
        
        private void labelListToolStripMenuItem_Click(object sender, EventArgs e) => 
            ShowCommentList();

        private void openLastProjectAutomaticallyToolStripMenuItem_Click(object sender, EventArgs e) =>
            ToggleOpenLastProjectEnabled();

        private void closeProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
        }

        private void importCDLToolStripMenuItem_Click_1(object sender, EventArgs e) => 
            ImportBizhawkCdl();
        private void importBsnesTracelogText_Click(object sender, EventArgs e) => 
            ImportBsnesTraceLogText();
        private void importUsageMapToolStripMenuItem_Click_1(object sender, EventArgs e) => 
            ImportBsnesUsageMap();

        private void graphicsWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
            // graphics view window
        }

        private void toolStripOpenLast_Click(object sender, EventArgs e) => 
            OpenLastProject();

        private void rescanForInOutPointsToolStripMenuItem_Click(object sender, EventArgs e) => 
            RescanForInOut();

        #endregion
        
        #region Prompts
        private bool PromptContinueEvenIfUnsavedChanges()
        {
            if (Project == null || !(Project?.Session?.UnsavedChanges ?? false))
                return true;

            return DialogResult.OK == MessageBox.Show(
                "You have unsaved changes. They will be lost if you continue.",
                "Unsaved Changes", MessageBoxButtons.OKCancel);
        }

        private string PromptForOpenFilename()
        {
            // TODO: combine with another function here that does similar
            openFileDialog.InitialDirectory = Project?.Session?.ProjectFileName ?? "";
            return openFileDialog.ShowDialog() == DialogResult.OK ? openFileDialog.FileName : "";
        }

        private void PromptForFilenameToSave()
        {
            saveProjectFile.InitialDirectory = Project?.Session?.ProjectFileName ?? "";
            if (saveProjectFile.ShowDialog() == DialogResult.OK && saveProjectFile.FileName != "")
            {
                MainFormController.SaveProject(saveProjectFile.FileName);
            }
        }

        private bool PromptForOpenProjectFilename()
        {
            if (!PromptContinueEvenIfUnsavedChanges())
                return false;

            openProjectFile.InitialDirectory = Project?.Session?.ProjectFileName;
            return openProjectFile.ShowDialog() == DialogResult.OK;
        }

        // ReSharper disable once InconsistentNaming
        private string PromptOpenBizhawkCDLFile()
        {
            openCDLDialog.InitialDirectory = Project?.Session?.ProjectFileName;
            if (openCDLDialog.ShowDialog() != DialogResult.OK)
                return "";

            return !PromptContinueEvenIfUnsavedChanges() ? "" : openCDLDialog.FileName;
        }

        private static void ReportNumberFlagsModified(long numModifiedFlags, int numFiles = 1)
        {
            MessageBox.Show($"Modified total {numModifiedFlags} flags from {numFiles} files!",
                "Done",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private bool PromptForImportBsnesTraceLogFile()
        {
            openTraceLogDialog.Multiselect = true;
            return openTraceLogDialog.ShowDialog() == DialogResult.OK;
        }
        
        private static void ShowOffsetOutOfRangeMsg()
        {
            ShowError("That offset is out of range.");
        }

        private int PromptForGotoOffset()
        {
            if (!RomDataPresent())
                return -1;

            var go = new GotoDialog(SelectedSnesOffset, Project.Data);
            var result = go.ShowDialog();
            if (result != DialogResult.OK)
                return -1;
            
            return go.GetPcOffset();
        }

        private static void ShowError(string errorMsg, string caption = "Error") => 
            MessageBox.Show(errorMsg, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);

        public bool PromptHarshAutoStep(int offset, out int newOffset, out int count)
        {
            newOffset = count = -1;
            
            var harsh = new HarshAutoStep(offset, Project.Data);
            if (harsh.ShowDialog() != DialogResult.OK)
                return false;
            
            newOffset = harsh.Start;
            count = harsh.Count;
            return true;
        }

        public MarkManyDialog PromptMarkMany(int offset, int whichIndex)
        {
            var mark = new MarkManyDialog(offset, whichIndex, Project.Data);
            return mark.ShowDialog() == DialogResult.OK ? mark : null;
        }

        void IProjectView.ShowOffsetOutOfRangeMsg() => ShowOffsetOutOfRangeMsg();

        private bool PromptForMisalignmentCheck()
        {
            if (!RomDataPresent())
                return false;

            return new MisalignmentChecker(Project.Data).ShowDialog() == DialogResult.OK;
        }

        private static void ShowInfo(string s, string caption) => 
            MessageBox.Show(s, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);

        private bool PromptForInOutChecking()
        {
            if (!RomDataPresent())
                return false;

            return new InOutPointChecker().ShowDialog() == DialogResult.OK;
        }

        public string AskToSelectNewRomFilename(string promptSubject, string promptText)
        {
            string initialDir = null; // TODO: Project.ProjectFileName
            return GuiUtil.PromptToConfirmAction(promptSubject, promptText, 
                () => GuiUtil.PromptToSelectFile(initialDir)
            );
        }

        public void OnProjectOpenWarning(string warningMsg) => 
            MessageBox.Show(warningMsg, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        private void viewHelpToolStripMenuItem_Click(object sender, EventArgs e) =>
            GuiUtil.OpenExternalProcess("help.html");

        private void githubToolStripMenuItem_Click(object sender, EventArgs e) =>
            GuiUtil.OpenExternalProcess("https://github.com/Dotsarecool/DiztinGUIsh");
        
        public ILongRunningTaskHandler.LongRunningTaskHandler TaskHandler =>
            ProgressBarJob.RunAndWaitForCompletion;

        private void viewOpcodesOnly_click(object sender, EventArgs e) => 
            DataGridDataController.FilterShowOpcodesOnly = !DataGridDataController.FilterShowOpcodesOnly;

        #endregion

        public void BeginAddingLabel()
        {
            if (!RomDataPresent())
                return;
            
            DataGridDataController.BeginEditingLabel();
        }
            
        public void BeginEditingComment()
        {
            if (!RomDataPresent())
                return;
                
            DataGridDataController.BeginEditingComment();
        }

        private void DataGridEditorForm_KeyDown(object sender, KeyEventArgs e)
        {
            var offset = SelectedSnesOffset;
                
            switch (e.KeyCode)
            {
                // actions
                case Keys.S:
                    MainFormController.Step(offset);
                    break;
                case Keys.I:
                    MainFormController.StepIn(offset);
                    break;
                case Keys.A:
                    MainFormController.AutoStepSafe(offset);
                    break;
                case Keys.T:
                    MainFormController.GoToIntermediateAddress(offset);
                    break;
                case Keys.U:
                    MainFormController.GoToUnreached(true, true);
                    break;
                case Keys.H:
                    MainFormController.GoToUnreached(false, false);
                    break;
                case Keys.N:
                    MainFormController.GoToUnreached(false, true);
                    break;
                case Keys.K:
                    MainFormController.Mark(offset);
                    break;
                case Keys.M:
                    MainFormController.SetMFlag(offset, !Project.Data.GetMFlag(offset));
                    break;
                case Keys.X:
                    MainFormController.SetXFlag(offset, !Project.Data.GetXFlag(offset));
                    break;
            }
        }
    }
}
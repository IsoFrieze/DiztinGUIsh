using System;
using System.IO;
using System.Windows.Forms;
using Diz.Core.export;
using Diz.Core.model;
using Diz.Core.util;
using DiztinGUIsh.controller;
using DiztinGUIsh.Properties;
using DiztinGUIsh.util;
using DiztinGUIsh.window.dialog;
using DiztinGUIsh.window2;

namespace DiztinGUIsh.window
{
    public partial class DataGridEditorForm : Form, IBytesFormViewer, IProjectView
    {
        // a class we create that controls just the data grid usercontrol we host
        private RomByteDataBindingGridController dataGridDataController;
        
        #region Main
        public DataGridEditorForm()
        {
            InitializeComponent();
        }

        private void Init()
        {
            // 
            // DataGridEditorForm itself, old designer stuff migrated. keep or kill
            // 
            // this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            // this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            // this.ClientSize = new System.Drawing.Size(930, 538);
            // this.MinimumSize = new System.Drawing.Size(780, 196);

            dataGridDataController = new RomByteDataBindingGridController
            {
                ViewGrid = dataGridEditorControl1,
                Data = MainFormController.Data,
            };
            dataGridEditorControl1.DataController = dataGridDataController;
            
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

        private void ProjectController_ProjectChanged(object sender, MainFormController.ProjectChangedEventArgs e)
        {
            switch (e.ChangeType)
            {
                case MainFormController.ProjectChangedEventArgs.ProjectChangedType.Saved:
                    OnProjectSaved();
                    break;
                case MainFormController.ProjectChangedEventArgs.ProjectChangedType.Opened:
                    OnProjectOpened(e.Filename);
                    break;
                case MainFormController.ProjectChangedEventArgs.ProjectChangedType.Imported:
                    OnImportedProjectSuccess();
                    break;
                case MainFormController.ProjectChangedEventArgs.ProjectChangedType.Closing:
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
            ExportDisassembly.ShowExportResults(result);
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

            MainFormController.UpdateExportSettings(selectedSettings.Value);

            return settings;
        }
        #endregion

        #region Properties

        public DizDocument Document => MainFormController.Document;

        public Project Project
        {
            get => Document.Project;
            // set => Document.Project = value;
        }

        // not sure if this will be the final place this lives. OK for now. -Dom
        public MainFormController MainFormController
        {
            get => _mainFormController;
            set
            {
                _mainFormController = value;
                // _mainFormController.ProjectView = this;
                Document.PropertyChanged += Document_PropertyChanged;
                MainFormController.ProjectChanged += ProjectController_ProjectChanged;
            }
        }

        // sub windows
        public AliasList AliasList;
        private VisualizerForm visualForm;

        // TODO: add a handler so we get notified when CurrentViewOffset changes.
        // then, we split most of our functions up into
        // 1. things that change ViewOffset
        // 2. things that react to ViewOffset changes.
        //
        // This will allow more flexibility and synchronizing different views (i.e. main table, graphics, layout, etc)
        // and this lets us save this value with the project file itself.

        // Data offset of the "view" i.e. the top of the table
        /*private int ViewOffset
        {
            get => Project?.CurrentViewOffset ?? 0;
            set => Project.CurrentViewOffset = value;
        }*/

        private bool importerMenuItemsEnabled;
        
        private MainFormController _mainFormController;
        
        public void InvalidateTable()
        {
            // tableController.Invalidate();
        }

        #endregion
        
        /*
        #region Table_stuff
        private MemoryTableUserControl tableControl;
        private MemoryTableController tableController;

        private void InitTableControl()
        {
            tableControl = new MemoryTableUserControl
            {
                Table = table, 
                Controller = tableController, 
                vScrollBar1 = vScrollBar1, // hack for now.
            };
            tableController = new MemoryTableController
            {
                TableControl = tableControl,
            };
        }
        
        private void vScrollBar1_ValueChanged(object sender, EventArgs e)
        {
            tableControl.ScrollTo(vScrollBar1.Value);
        }

        // private void SetCurrentCellTo(int i)
        // {
        //     tableController.SetCurrentCellTo(i);
        // }
        //
        // private int GetSelectedOffset()
        // {
        //     return tableController.GetSelectedOffset();
        // }

        private void table_MouseDown(object sender, MouseEventArgs e)
        {
            tableController.InvalidateTable();
        }

        private void table_KeyDown(object sender, KeyEventArgs e)
        {
            // HACK. eventually, the KeyDown() msg will come from the control itself so,
            // forward the handler there manually for now. eventually, this call will go away from MainForm.

            tableController.TableControl.table_KeyDown(sender, e);
        }
        
        // -------------

        private void InitMainTable()
        {
            InitTableControl();
        }

        private void BeginEditingComment()
        {
            tableController.BeginEditingComment();
        }

        private void BeginAddingLabel()
        {
            tableController.BeginAddingLabel();
        }
        #endregion*/
        
        #region Actions
        private void OpenLastProject()
        {
            if (Document.LastProjectFilename == "")
                return;

            // safeguard: if we crash opening this project,
            // then next time we load make sure we don't try it again.
            // this will be reset later
            var projectToOpen = Document.LastProjectFilename;
            Document.LastProjectFilename = "";

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
                MainFormController.MarkChanged();
            InvalidateTable();
            
            ShowInfo($"Modified {count} flags!", "Done!");
        }

        private void RescanForInOut()
        {
            if (!PromptForInOutChecking()) 
                return;

            Project.Data.RescanInOutPoints();
            MainFormController.MarkChanged();
            
            InvalidateTable();
            ShowInfo("Scan complete!", "Done!");
        }

        private void SaveProject()
        {
            MainFormController.SaveProject(Project.ProjectFileName);
        }

        private void ShowVisualizerForm()
        {
            visualForm ??= new VisualizerForm(this);
            visualForm.Show();
        }

        private void ShowCommentList()
        {
            AliasList.Show();
        }

        private void SetMarkerLabel(FlagType flagType)
        {
            _mainFormController.CurrentMarkFlag = flagType;
            UpdateMarkerLabel();
        }

        private void ToggleMoveWithStep()
        {
            _mainFormController.ToggleMoveWithStep();
        }

        private void ToggleOpenLastProjectEnabled()
        {
            Settings.Default.OpenLastFileAutomatically = openLastProjectAutomaticallyToolStripMenuItem.Checked;
            Settings.Default.Save();
            UpdateUiFromSettings();
        }
        #endregion
        
        #region Importers

        public void SelectOffset(int offset)
        {
            MainFormController.SelectOffset(offset);
        }

        public IImportRomDialogView GetImportView() => new ImportRomDialog();

        // void IProjectView.ShowOffsetOutOfRangeMsg() => ShowOffsetOutOfRangeMsg();

        private void ImportBizhawkCDL()
        {
            var filename = PromptOpenBizhawkCDLFile();
            if (filename != null && filename == "") return;
            ImportBizHawkCdl(filename);
            RefreshTablePercentAndWindowTitle();
        }

        private void ImportBizHawkCdl(string filename)
        {
            try
            {
                MainFormController.ImportBizHawkCdl(filename);
            }
            catch (Exception ex)
            {
                ShowError(ex.Message, "Error");
            }
        }

        private void ImportBsnesTraceLogText()
        {
            if (!PromptForImportBSNESTraceLogFile()) return;
            var (numModifiedFlags, numFiles) = ImportBSNESTraceLogs();
            ReportNumberFlagsModified(numModifiedFlags, numFiles);
        }

        private void ImportBSNESUsageMap()
        {
            if (openUsageMapFile.ShowDialog() != DialogResult.OK)
                return;

            var numModifiedFlags = MainFormController.ImportBsnesUsageMap(openUsageMapFile.FileName);

            ShowInfo($"Modified total {numModifiedFlags} flags!", "Done");
        }

        private (long numBytesModified, int numFiles) ImportBSNESTraceLogs()
        {
            var numBytesModified = MainFormController.ImportBsnesTraceLogs(openTraceLogDialog.FileNames);
            return (numBytesModified, openTraceLogDialog.FileNames.Length);
        }

        private void ImportBsnesBinaryTraceLog()
        {
            new BsnesTraceLogBinaryMonitorForm(this).ShowDialog();
            RefreshUi();
        }

        private void OnImportedProjectSuccess()
        {
            UpdateSaveOptionStates(saveEnabled: false, saveAsEnabled: true, closeEnabled: true);
            RefreshUi();
        }
        #endregion
        
        #region ReadOnlyHelpers
       

        private bool RomDataPresent()
        {
            return Project?.Data?.GetRomSize() > 0;
        }
        #endregion
        
        #region State updates
        private void RebindProject()
        {
            // TODO: replace all this with OnNotifyPropertyChanged stuff eventually

            // TODO dataGridDataController.Data = Project.Data;
            dataGridDataController.Data = Project.Data;
            
            AliasList?.RebindProject();
            
            if (visualForm != null) 
                visualForm.Project = Project;
        }

        private void UpdatePanels()
        {
            /*table.Height = Height - 85;
            table.Width = Width - 33;
            vScrollBar1.Height = Height - 85;
            vScrollBar1.Left = Width - 33;
            if (WindowState == FormWindowState.Maximized) 
                tableController.UpdateDataGridView();*/
        }

        public void UpdateWindowTitle()
        {
            Text =
                (Project.UnsavedChanges ? "*" : "") +
                (Project.ProjectFileName ?? "New Project") +
                " - DiztinGUIsh";
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

        private void RefreshUi()
        {
            importCDLToolStripMenuItem.Enabled = true;
            // tableController.UpdateDataGridView();

            UpdateWindowTitle();
            UpdatePercent();
            // table.Invalidate();
            EnableSubWindows();
        }

        private void UpdateBase(Util.NumberBase noBase)
        {
            // tableControl.DisplayBase = noBase; // TODO: move this out of mainwindow, into table class.
            
            decimalToolStripMenuItem.Checked = noBase == Util.NumberBase.Decimal;
            hexadecimalToolStripMenuItem.Checked = noBase == Util.NumberBase.Hexadecimal;
            binaryToolStripMenuItem.Checked = noBase == Util.NumberBase.Binary;
            InvalidateTable();
        }

        public void UpdatePercent()
        {
            if (Project?.Data == null || Project.Data.GetRomSize() <= 0)
                return;

            int totalUnreached = 0, size = Project.Data.GetRomSize();
            for (var i = 0; i < size; i++)
                if (Project.Data.GetFlag(i) == FlagType.Unreached)
                    totalUnreached++;
            var reached = size - totalUnreached;
            percentComplete.Text = $"{reached * 100.0 / size:N3}% ({reached:D}/{size:D})";
        }

        public void UpdateMarkerLabel()
        {
            currentMarker.Text = $"Marker: {_mainFormController.CurrentMarkFlag.ToString()}";
        }

        private void UpdateImporterEnabledStatus()
        {
            importUsageMapToolStripMenuItem.Enabled = importerMenuItemsEnabled;
            importCDLToolStripMenuItem.Enabled = importerMenuItemsEnabled;
            importTraceLogBinary.Enabled = importerMenuItemsEnabled;
            importTraceLogText.Enabled = importerMenuItemsEnabled;
        }

        private void EnableSubWindows()
        {
            labelListToolStripMenuItem.Enabled = true;
        }

        public void UpdateSaveOptionStates(bool saveEnabled, bool saveAsEnabled, bool closeEnabled)
        {
            saveProjectToolStripMenuItem.Enabled = saveEnabled;
            saveProjectAsToolStripMenuItem.Enabled = saveAsEnabled;
            closeProjectToolStripMenuItem.Enabled = closeEnabled;

            exportLogToolStripMenuItem.Enabled = true;
        }

        private void RefreshTablePercentAndWindowTitle()
        {
            // refactor this somewhere else, use property change notifications if possible
            RefreshPercentAndWindowTitle();
            InvalidateTable();
        }

        private void RefreshPercentAndWindowTitle()
        {
            // refactor this somewhere else, use property change notifications if possible
            UpdatePercent();
            UpdateWindowTitle();
        }
        #endregion

        #region Simple Event Handlers
        private void MainWindow_FormClosing(object sender, FormClosingEventArgs e) =>
            e.Cancel = !PromptContinueEvenIfUnsavedChanges();

        private void MainWindow_SizeChanged(object sender, EventArgs e) => UpdatePanels();
        private void MainWindow_ResizeEnd(object sender, EventArgs e)
        {
            // tableController.UpdateDataGridView();
        }

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

        public int SelectedOffset
        {
            get => throw new NotImplementedException(); //tableController.SelectedOffset);
            set => throw new NotImplementedException(); // tableController.SelectedOffset = value;
        }
        
        private void stepOverToolStripMenuItem_Click(object sender, EventArgs e) 
            => MainFormController.Step(SelectedOffset);

        private void stepInToolStripMenuItem_Click(object sender, EventArgs e) => MainFormController.StepIn(SelectedOffset);
        private void autoStepSafeToolStripMenuItem_Click(object sender, EventArgs e) => MainFormController.AutoStepSafe(SelectedOffset);
        private void autoStepHarshToolStripMenuItem_Click(object sender, EventArgs e) => MainFormController.AutoStepHarsh(SelectedOffset);
        private void gotoToolStripMenuItem_Click(object sender, EventArgs e) => MainFormController.GoTo(PromptForGotoOffset());

        private void gotoIntermediateAddressToolStripMenuItem_Click(object sender, EventArgs e) =>
            MainFormController.GoToIntermediateAddress(SelectedOffset);

        private void gotoFirstUnreachedToolStripMenuItem_Click(object sender, EventArgs e) => 
            MainFormController.GoToUnreached(true, true);

        private void gotoNearUnreachedToolStripMenuItem_Click(object sender, EventArgs e) =>
            MainFormController.GoToUnreached(false, false);

        private void gotoNextUnreachedToolStripMenuItem_Click(object sender, EventArgs e) => 
            MainFormController.GoToUnreached(false, true);
        
        private void markOneToolStripMenuItem_Click(object sender, EventArgs e) => MainFormController.Mark(SelectedOffset);
        private void markManyToolStripMenuItem_Click(object sender, EventArgs e) => MainFormController.MarkMany(SelectedOffset, 7);
        private void setDataBankToolStripMenuItem_Click(object sender, EventArgs e) => MainFormController.MarkMany(SelectedOffset, 8);
        private void setDirectPageToolStripMenuItem_Click(object sender, EventArgs e) => MainFormController.MarkMany(SelectedOffset, 9);

        private void toggleAccumulatorSizeMToolStripMenuItem_Click(object sender, EventArgs e) => MainFormController.MarkMany(SelectedOffset, 10);

        private void toggleIndexSizeToolStripMenuItem_Click(object sender, EventArgs e) => MainFormController.MarkMany(SelectedOffset, 11);
        private void addCommentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // BeginEditingComment();
        }

        private void unreachedToolStripMenuItem_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Unreached);

        private void opcodeToolStripMenuItem_Click(object sender, EventArgs e) => SetMarkerLabel(FlagType.Opcode);

        private void operandToolStripMenuItem_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Operand);

        private void bitDataToolStripMenuItem_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Data8Bit);

        private void graphicsToolStripMenuItem_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Graphics);

        private void musicToolStripMenuItem_Click(object sender, EventArgs e) => SetMarkerLabel(FlagType.Music);
        private void emptyToolStripMenuItem_Click(object sender, EventArgs e) => SetMarkerLabel(FlagType.Empty);

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

        private void textToolStripMenuItem_Click(object sender, EventArgs e) => SetMarkerLabel(FlagType.Text);

        private void fixMisalignedInstructionsToolStripMenuItem_Click(object sender, EventArgs e) =>
            FixMisalignedInstructions();

        private void moveWithStepToolStripMenuItem_Click(object sender, EventArgs e) => _mainFormController.ToggleMoveWithStep();
        private void labelListToolStripMenuItem_Click(object sender, EventArgs e) => ShowCommentList();

        private void openLastProjectAutomaticallyToolStripMenuItem_Click(object sender, EventArgs e) =>
            ToggleOpenLastProjectEnabled();

        private void closeProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
        }

        private void importCDLToolStripMenuItem_Click_1(object sender, EventArgs e) => ImportBizhawkCDL();

        private void importBsnesTracelogText_Click(object sender, EventArgs e) => ImportBsnesTraceLogText();

        private void graphicsWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
            // graphics view window
        }

        private void toolStripOpenLast_Click(object sender, EventArgs e)
        {
            OpenLastProject();
        }

        private void rescanForInOutPointsToolStripMenuItem_Click(object sender, EventArgs e) => RescanForInOut();
        private void importUsageMapToolStripMenuItem_Click_1(object sender, EventArgs e) => ImportBSNESUsageMap();
        private void table_MouseWheel(object sender, MouseEventArgs e)
        {
            // tableControl.table_MouseWheel(sender, e);
        }

        #endregion
        
        #region Prompts
        private bool PromptContinueEvenIfUnsavedChanges()
        {
            if (Project == null || !Project.UnsavedChanges)
                return true;

            return DialogResult.OK == MessageBox.Show(
                "You have unsaved changes. They will be lost if you continue.",
                "Unsaved Changes", MessageBoxButtons.OKCancel);
        }

        private string PromptForOpenFilename()
        {
            // TODO: combine with another function here that does similar
            openFileDialog.InitialDirectory = Project?.ProjectFileName ?? "";
            return openFileDialog.ShowDialog() == DialogResult.OK ? openFileDialog.FileName : "";
        }

        private void PromptForFilenameToSave()
        {
            saveProjectFile.InitialDirectory = Project.AttachedRomFilename;
            if (saveProjectFile.ShowDialog() == DialogResult.OK && saveProjectFile.FileName != "")
            {
                MainFormController.SaveProject(saveProjectFile.FileName);
            }
        }

        private bool PromptForOpenProjectFilename()
        {
            if (!PromptContinueEvenIfUnsavedChanges())
                return false;

            openProjectFile.InitialDirectory = Project?.ProjectFileName;
            return openProjectFile.ShowDialog() == DialogResult.OK;
        }

        private string PromptOpenBizhawkCDLFile()
        {
            openCDLDialog.InitialDirectory = Project.ProjectFileName;
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

        private bool PromptForImportBSNESTraceLogFile()
        {
            openTraceLogDialog.Multiselect = true;
            return openTraceLogDialog.ShowDialog() == DialogResult.OK;
        }
        
        private static void ShowOffsetOutOfRangeMsg()
        {
            ShowError("That offset is out of range.", "Error");
        }

        private int PromptForGotoOffset()
        {
            if (!RomDataPresent())
                return -1;

            throw new NotImplementedException();

            /*var go = new GotoDialog(tableControl.ViewOffset + table.CurrentCell.RowIndex, Project.Data);
            var result = go.ShowDialog();
            if (result != DialogResult.OK)
                return -1;
            
            return go.GetPcOffset();*/
        }

        private static void ShowError(string errorMsg, string caption = "Error")
        {
            MessageBox.Show(errorMsg, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

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

        public MarkManyDialog PromptMarkMany(int offset, int column)
        {
            var mark = new MarkManyDialog(offset, column, Project.Data);
            return mark.ShowDialog() == DialogResult.OK ? mark : null;
        }

        void IProjectView.ShowOffsetOutOfRangeMsg()
        {
            ShowOffsetOutOfRangeMsg();
        }

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
        
        #endregion
    }
}
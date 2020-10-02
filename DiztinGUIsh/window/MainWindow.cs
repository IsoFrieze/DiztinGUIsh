using DiztinGUIsh.window;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using DiztinGUIsh.core.util;
using DiztinGUIsh.Properties;
using DiztinGUIsh.window.dialog;

namespace DiztinGUIsh
{
    public partial class MainWindow : Form, IProjectView
    {
        // temp: readonly project data model. eventually, get this ONLY from the controller.
        // right now, it returns Project2 which will go away
        public Project Project { get; set; }

        public MainWindow()
        {
            ProjectController = new ProjectController {
                ProjectView = this,
            };
            ProjectController.ProjectChanged += ProjectController_ProjectChanged;

            InitializeComponent();
        }

        private void ProjectController_ProjectChanged(object sender, ProjectController.ProjectChangedEventArgs e)
        {
            RebindProject();

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
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void RebindProject()
        {
            aliasList.RebindProject();
        }

        private void MainWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = !ContinueUnsavedChanges();
        }

        private void MainWindow_SizeChanged(object sender, EventArgs e)
        {
            UpdatePanels();
        }

        private void UpdatePanels()
        {
            table.Height = this.Height - 85;
            table.Width = this.Width - 33;
            vScrollBar1.Height = this.Height - 85;
            vScrollBar1.Left = this.Width - 33;
            if (WindowState == FormWindowState.Maximized) UpdateDataGridView();
        }

        private void MainWindow_ResizeEnd(object sender, EventArgs e)
        {
            UpdateDataGridView();
        }

        private void MainWindow_Load(object sender, EventArgs e)
        {
            table.CellValueNeeded += new DataGridViewCellValueEventHandler(table_CellValueNeeded);
            table.CellValuePushed += new DataGridViewCellValueEventHandler(table_CellValuePushed);
            table.CellPainting += new DataGridViewCellPaintingEventHandler(table_CellPainting);

            rowsToShow = ((table.Height - table.ColumnHeadersHeight) / table.RowTemplate.Height);

            // https://stackoverflow.com/a/1506066
            typeof(DataGridView).InvokeMember(
                "DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                null,
                table,
                new object[] { true });

            aliasList = new AliasList(this);

            UpdatePanels();
            UpdateUIFromSettings();

            if (Settings.Default.OpenLastFileAutomatically)
                openLastProject();
        }

        private void openLastProject()
        {
            if (LastProjectFilename == "") 
                return;

            // safeguard: if we crash opening this project,
            // then next time we load make sure we don't try it again.
            // this will be reset later
            var projectToOpen = LastProjectFilename;
            LastProjectFilename = "";

            OpenProject(projectToOpen);
        }

        public void UpdateWindowTitle()
        {
            this.Text =
                (Project.UnsavedChanges ? "*" : "") +
                (Project.ProjectFileName ?? "New Project") +
                " - DiztinGUIsh";
        }

        private bool ContinueUnsavedChanges()
        {
            if (Project == null || !Project.UnsavedChanges)
                return true;

            return DialogResult.OK == MessageBox.Show(
                "You have unsaved changes. They will be lost if you continue.",
                "Unsaved Changes", MessageBoxButtons.OKCancel);

        }

        public void UpdateSaveOptionStates(bool save, bool saveas)
        {
            saveProjectToolStripMenuItem.Enabled = save;
            saveProjectAsToolStripMenuItem.Enabled = saveas;
            exportLogToolStripMenuItem.Enabled = true;
        }

        private void newProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!ContinueUnsavedChanges()) 
                return;

            var romFilename = PromptForOpenFilename();
            if (string.IsNullOrEmpty(romFilename))
                return;

            if (!TryImportProject(openFileDialog.FileName))
                return;

            OnImportedProjectSuccess();
        }

        private string PromptForOpenFilename()
        {
            // TODO: combine with another function in Project that looks like this
            openFileDialog.InitialDirectory = Project?.ProjectFileName ?? "";
            return openFileDialog.ShowDialog() == DialogResult.OK ? openFileDialog.FileName : null;
        }

        private bool TryImportProject(string romFileToOpen)
        {
            try
            {
                var importSettings = PromptForImportSettings(romFileToOpen);
                if (importSettings == null)
                    return false;

                ProjectController.ImportRomAndCreateNewProject(importSettings);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error importing project", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return false;
        }

        private static Project.ImportRomSettings PromptForImportSettings(string romFileToOpen)
        {
            return new ImportRomDialog().PromptForImportSettings(romFileToOpen);
        }

        private void openProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!ContinueUnsavedChanges()) 
                return;

            openProjectFile.InitialDirectory = Project?.ProjectFileName;
            if (openProjectFile.ShowDialog() != DialogResult.OK) 
                return;

            OpenProject(openProjectFile.FileName);
        }

        // TODO: state change needs to go in controller
        public string LastProjectFilename
        {
            get => Settings.Default.LastOpenedFile;
            set
            {
                Settings.Default.LastOpenedFile = value;
                Settings.Default.Save();

                UpdateUIFromSettings();
            }
        }

        private void UpdateUIFromSettings()
        {
            bool lastOpenedFilePresent = Settings.Default.LastOpenedFile != "";
            toolStripOpenLast.Enabled = lastOpenedFilePresent;
            toolStripOpenLast.Text = "Open Last File";
            if (lastOpenedFilePresent)
                toolStripOpenLast.Text += $" ({Path.GetFileNameWithoutExtension(Settings.Default.LastOpenedFile)})";

            openLastProjectAutomaticallyToolStripMenuItem.Checked = Settings.Default.OpenLastFileAutomatically;
        }

        public void OnProjectOpened(string filename)
        {
            UpdateSaveOptionStates(true, true);
            RefreshUI();

            LastProjectFilename = filename; // do this last.
        }

        private void OnImportedProjectSuccess()
        {
            UpdateSaveOptionStates(false, true);
            RefreshUI();
        }

        private void RefreshUI()
        {
            importCDLToolStripMenuItem.Enabled = true;
            UpdateWindowTitle();
            UpdateDataGridView();
            UpdatePercent();
            table.Invalidate();
            EnableSubWindows();
        }

        public void OnProjectOpenFail()
        {
            LastProjectFilename = "";
        }


        public void OpenProject(string filename)
        {
            ProjectController.OpenProject(filename);
        }

        private void saveProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveProject(Project.ProjectFileName);
        }

        public void SaveProject(string filename)
        {
            ProjectController.SaveProject(filename);
        }

        public void OnProjectSaved()
        {
            UpdateSaveOptionStates(true, true);
            UpdateWindowTitle();
        }

        public void OnExportFinished(LogCreator.OutputResult result)
        {
            if (result.error_count > 0)
                MessageBox.Show("Disassembly created with errors. See errors.txt for details.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
                MessageBox.Show("Disassembly created successfully!", "Complete", MessageBoxButtons.OK,
                    MessageBoxIcon.Asterisk);
        }

        IProjectView.LongRunningTaskHandler IProjectView.TaskHandler => 
            ProgressBarJob.RunAndWaitForCompletion;

        private void saveProjectAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveProjectFile.InitialDirectory = Project.AttachedRomFilename;
            if (saveProjectFile.ShowDialog() == DialogResult.OK && saveProjectFile.FileName != "")
            {
                SaveProject(saveProjectFile.FileName);
            }
        }

        private void importCDLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openCDLDialog.InitialDirectory = Project.ProjectFileName;
            DialogResult result = openCDLDialog.ShowDialog();
            if (result != DialogResult.OK) return;
            if (!ContinueUnsavedChanges()) return;

            var filename = openCDLDialog.FileName;

            try
            {
                ProjectController.ImportBizHawkCDL(filename);
            }
            catch (InvalidDataException ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (EndOfStreamException ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdatePercent();
            UpdateWindowTitle();
            InvalidateTable();
        }

        public ProjectController ProjectController { get; protected set; }

        private void exportLogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var adjustedSettings = PromptForExportSettingsAndConfirmation();
            if (!adjustedSettings.HasValue)
                return;

            ProjectController.UpdateExportSettings(adjustedSettings.Value);
            ProjectController.WriteAssemblyOutput();
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

        private void viewHelpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(Directory.GetCurrentDirectory() + "/help.html");
            }
            catch (Exception)
            {
                MessageBox.Show("Can't find the help file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void githubToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/Dotsarecool/DiztinGUIsh");
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var about = new About();
            about.ShowDialog();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private Util.NumberBase DisplayBase = Util.NumberBase.Hexadecimal;
        private Data.FlagType markFlag = Data.FlagType.Data8Bit;
        private bool MoveWithStep = true;

        private void decimalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateBase(Util.NumberBase.Decimal);
        }

        private void hexadecimalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateBase(Util.NumberBase.Hexadecimal);
        }

        private void binaryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateBase(Util.NumberBase.Binary);
        }

        private void UpdateBase(Util.NumberBase noBase)
        {
            DisplayBase = noBase;
            decimalToolStripMenuItem.Checked = noBase == Util.NumberBase.Decimal;
            hexadecimalToolStripMenuItem.Checked = noBase == Util.NumberBase.Hexadecimal;
            binaryToolStripMenuItem.Checked = noBase == Util.NumberBase.Binary;
            InvalidateTable();
        }

        public void UpdatePercent()
        {
            if (Project?.Data == null || Project.Data.GetROMSize() <= 0)
                return;

            int totalUnreached = 0, size = Project.Data.GetROMSize();
            for (int i = 0; i < size; i++)
                if (Project.Data.GetFlag(i) == Data.FlagType.Unreached)
                    totalUnreached++;
            int reached = size - totalUnreached;
            percentComplete.Text = string.Format("{0:N3}% ({1:D}/{2:D})", reached * 100.0 / size, reached, size);
        }

        public void UpdateMarkerLabel()
        {
            currentMarker.Text = string.Format("Marker: {0}", markFlag.ToString());
        }

        public void InvalidateTable()
        {
            table.Invalidate();
        }

        // DataGridView

        private int ViewOffset
        {
            get => Project?.CurrentViewOffset ?? 0;
            set => Project.CurrentViewOffset = value;
        }

        private int rowsToShow;

        private void UpdateDataGridView()
        {
            if (Project?.Data == null || Project.Data.GetROMSize() <= 0)
                return;

            rowsToShow = ((table.Height - table.ColumnHeadersHeight) / table.RowTemplate.Height);
            
            if (ViewOffset + rowsToShow > Project.Data.GetROMSize()) 
                ViewOffset = Project.Data.GetROMSize() - rowsToShow;
            
            if (ViewOffset < 0)
                ViewOffset = 0;
            
            vScrollBar1.Enabled = true;
            vScrollBar1.Maximum = Project.Data.GetROMSize() - rowsToShow;
            vScrollBar1.Value = ViewOffset;
            table.RowCount = rowsToShow;

            importTraceLogToolStripMenuItem.Enabled = true;
            importUsageMapToolStripMenuItem.Enabled = true;
        }

        private void table_MouseWheel(object sender, MouseEventArgs e)
        {
            if (Project?.Data == null || Project.Data.GetROMSize() <= 0) 
                return;
            int selRow = table.CurrentCell.RowIndex + ViewOffset, selCol = table.CurrentCell.ColumnIndex;
            int amount = e.Delta / 0x18;
            ViewOffset -= amount;
            UpdateDataGridView();
            if (selRow < ViewOffset) selRow = ViewOffset;
            else if (selRow >= ViewOffset + rowsToShow) selRow = ViewOffset + rowsToShow - 1;
            table.CurrentCell = table.Rows[selRow - ViewOffset].Cells[selCol];
            InvalidateTable();
        }

        private void vScrollBar1_ValueChanged(object sender, EventArgs e)
        {
            int selOffset = table.CurrentCell.RowIndex + ViewOffset;
            ViewOffset = vScrollBar1.Value;
            UpdateDataGridView();

            if (selOffset < ViewOffset) table.CurrentCell = table.Rows[0].Cells[table.CurrentCell.ColumnIndex];
            else if (selOffset >= ViewOffset + rowsToShow)
                table.CurrentCell = table.Rows[rowsToShow - 1].Cells[table.CurrentCell.ColumnIndex];
            else table.CurrentCell = table.Rows[selOffset - ViewOffset].Cells[table.CurrentCell.ColumnIndex];

            InvalidateTable();
        }

        private void table_MouseDown(object sender, MouseEventArgs e)
        {
            InvalidateTable();
        }

        private void table_KeyDown(object sender, KeyEventArgs e)
        {
            if (Project?.Data == null || Project.Data.GetROMSize() <= 0) return;

            int offset = table.CurrentCell.RowIndex + ViewOffset;
            int newOffset = offset;
            int amount = 0x01;

            Console.WriteLine(e.KeyCode);
            switch (e.KeyCode)
            {
                case Keys.Home:
                case Keys.PageUp:
                case Keys.Up:
                    amount = e.KeyCode == Keys.Up ? 0x01 : e.KeyCode == Keys.PageUp ? 0x10 : 0x100;
                    newOffset = offset - amount;
                    if (newOffset < 0) newOffset = 0;
                    SelectOffset(newOffset);
                    break;
                case Keys.End:
                case Keys.PageDown:
                case Keys.Down:
                    amount = e.KeyCode == Keys.Down ? 0x01 : e.KeyCode == Keys.PageDown ? 0x10 : 0x100;
                    newOffset = offset + amount;
                    if (newOffset >= Project.Data.GetROMSize()) newOffset = Project.Data.GetROMSize() - 1;
                    SelectOffset(newOffset);
                    break;
                case Keys.Left:
                    amount = table.CurrentCell.ColumnIndex;
                    amount = amount - 1 < 0 ? 0 : amount - 1;
                    table.CurrentCell = table.Rows[table.CurrentCell.RowIndex].Cells[amount];
                    break;
                case Keys.Right:
                    amount = table.CurrentCell.ColumnIndex;
                    amount = amount + 1 >= table.ColumnCount ? table.ColumnCount - 1 : amount + 1;
                    table.CurrentCell = table.Rows[table.CurrentCell.RowIndex].Cells[amount];
                    break;
                case Keys.S:
                    Step(offset);
                    break;
                case Keys.I:
                    StepIn(offset);
                    break;
                case Keys.A:
                    AutoStepSafe(offset);
                    break;
                case Keys.T:
                    GoToIntermediateAddress(offset);
                    break;
                case Keys.U:
                    GoToUnreached(true, true);
                    break;
                case Keys.H:
                    GoToUnreached(false, false);
                    break;
                case Keys.N:
                    GoToUnreached(false, true);
                    break;
                case Keys.K:
                    Mark(offset);
                    break;
                case Keys.L:
                    table.CurrentCell = table.Rows[table.CurrentCell.RowIndex].Cells[0];
                    table.BeginEdit(true);
                    break;
                case Keys.B:
                    table.CurrentCell = table.Rows[table.CurrentCell.RowIndex].Cells[8];
                    table.BeginEdit(true);
                    break;
                case Keys.D:
                    table.CurrentCell = table.Rows[table.CurrentCell.RowIndex].Cells[9];
                    table.BeginEdit(true);
                    break;
                case Keys.M:
                    Project.Data.SetMFlag(offset, !Project.Data.GetMFlag(offset));
                    break;
                case Keys.X:
                    Project.Data.SetXFlag(offset, !Project.Data.GetXFlag(offset));
                    break;
                case Keys.C:
                    table.CurrentCell = table.Rows[table.CurrentCell.RowIndex].Cells[12];
                    table.BeginEdit(true);
                    break;
            }

            e.Handled = true;
            InvalidateTable();
        }

        private void table_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            int row = e.RowIndex + ViewOffset;
            if (row >= Project.Data.GetROMSize()) return;
            switch (e.ColumnIndex)
            {
                case 0:
                    e.Value = Project.Data.GetLabelName(Project.Data.ConvertPCtoSNES(row));
                    break;
                case 1:
                    e.Value = Util.NumberToBaseString(Project.Data.ConvertPCtoSNES(row), Util.NumberBase.Hexadecimal, 6);
                    break;
                case 2:
                    e.Value = (char) Project.Data.GetROMByte(row);
                    break;
                case 3:
                    e.Value = Util.NumberToBaseString(Project.Data.GetROMByte(row), DisplayBase);
                    break;
                case 4:
                    e.Value = RomUtil.PointToString(Project.Data.GetInOutPoint(row));
                    break;
                case 5:
                    int len = Project.Data.GetInstructionLength(row);
                    if (row + len <= Project.Data.GetROMSize()) e.Value = Project.Data.GetInstruction(row);
                    else e.Value = "";
                    break;
                case 6:
                    int ia = Project.Data.GetIntermediateAddressOrPointer(row);
                    if (ia >= 0) e.Value = Util.NumberToBaseString(ia, Util.NumberBase.Hexadecimal, 6);
                    else e.Value = "";
                    break;
                case 7:
                    e.Value = RomUtil.TypeToString(Project.Data.GetFlag(row));
                    break;
                case 8:
                    e.Value = Util.NumberToBaseString(Project.Data.GetDataBank(row), Util.NumberBase.Hexadecimal, 2);
                    break;
                case 9:
                    e.Value = Util.NumberToBaseString(Project.Data.GetDirectPage(row), Util.NumberBase.Hexadecimal, 4);
                    break;
                case 10:
                    e.Value = RomUtil.BoolToSize(Project.Data.GetMFlag(row));
                    break;
                case 11:
                    e.Value = RomUtil.BoolToSize(Project.Data.GetXFlag(row));
                    break;
                case 12:
                    e.Value = Project.Data.GetComment(Project.Data.ConvertPCtoSNES(row));
                    break;
            }
        }

        private void table_CellValuePushed(object sender, DataGridViewCellValueEventArgs e)
        {
            string value = e.Value as string;
            int result;
            int row = e.RowIndex + ViewOffset;
            if (row >= Project.Data.GetROMSize()) return;
            switch (e.ColumnIndex)
            {
                case 0:
                    Project.Data.AddLabel(Project.Data.ConvertPCtoSNES(row), new Label() {name = value}, true);
                    break; // todo (validate for valid label characters)
                case 8:
                    if (int.TryParse(value, NumberStyles.HexNumber, null, out result)) Project.Data.SetDataBank(row, result);
                    break;
                case 9:
                    if (int.TryParse(value, NumberStyles.HexNumber, null, out result)) Project.Data.SetDirectPage(row, result);
                    break;
                case 10:
                    Project.Data.SetMFlag(row, (value == "8" || value == "M"));
                    break;
                case 11:
                    Project.Data.SetXFlag(row, (value == "8" || value == "X"));
                    break;
                case 12:
                    Project.Data.AddComment(Project.Data.ConvertPCtoSNES(row), value, true);
                    break;
            }

            table.InvalidateRow(e.RowIndex);
        }

        public void PaintCell(int offset, DataGridViewCellStyle style, int column, int selOffset)
        {
            // editable cells show up green
            if (column == 0 || column == 8 || column == 9 || column == 12) style.SelectionBackColor = Color.Chartreuse;

            switch (Project.Data.GetFlag(offset))
            {
                case Data.FlagType.Unreached:
                    style.BackColor = Color.LightGray;
                    style.ForeColor = Color.DarkSlateGray;
                    break;
                case Data.FlagType.Opcode:
                    int opcode = Project.Data.GetROMByte(offset);
                    switch (column)
                    {
                        case 4: // <*>
                            Data.InOutPoint point = Project.Data.GetInOutPoint(offset);
                            int r = 255, g = 255, b = 255;
                            if ((point & (Data.InOutPoint.EndPoint | Data.InOutPoint.OutPoint)) != 0) g -= 50;
                            if ((point & (Data.InOutPoint.InPoint)) != 0) r -= 50;
                            if ((point & (Data.InOutPoint.ReadPoint)) != 0) b -= 50;
                            style.BackColor = Color.FromArgb(r, g, b);
                            break;
                        case 5: // Instruction
                            if (opcode == 0x40 || opcode == 0xCB || opcode == 0xDB || opcode == 0xF8 // RTI WAI STP SED
                                || opcode == 0xFB || opcode == 0x00 || opcode == 0x02 || opcode == 0x42 // XCE BRK COP WDM
                            ) style.BackColor = Color.Yellow;
                            break;
                        case 8: // Data Bank
                            if (opcode == 0xAB || opcode == 0x44 || opcode == 0x54) // PLB MVP MVN
                                style.BackColor = Color.OrangeRed;
                            else if (opcode == 0x8B) // PHB
                                style.BackColor = Color.Yellow;
                            break;
                        case 9: // Direct Page
                            if (opcode == 0x2B || opcode == 0x5B) // PLD TCD
                                style.BackColor = Color.OrangeRed;
                            if (opcode == 0x0B || opcode == 0x7B) // PHD TDC
                                style.BackColor = Color.Yellow;
                            break;
                        case 10: // M Flag
                        case 11: // X Flag
                            int mask = column == 10 ? 0x20 : 0x10;
                            if (opcode == 0x28 || ((opcode == 0xC2 || opcode == 0xE2) // PLP SEP REP
                                && (Project.Data.GetROMByte(offset + 1) & mask) != 0)) // relevant bit set
                                style.BackColor = Color.OrangeRed;
                            if (opcode == 0x08) // PHP
                                style.BackColor = Color.Yellow;
                            break;
                    }
                    break;
                case Data.FlagType.Operand:
                    style.ForeColor = Color.LightGray;
                    break;
                case Data.FlagType.Graphics:
                    style.BackColor = Color.LightPink;
                    break;
                case Data.FlagType.Music:
                    style.BackColor = Color.PowderBlue;
                    break;
                case Data.FlagType.Data8Bit:
                case Data.FlagType.Data16Bit:
                case Data.FlagType.Data24Bit:
                case Data.FlagType.Data32Bit:
                    style.BackColor = Color.NavajoWhite;
                    break;
                case Data.FlagType.Pointer16Bit:
                case Data.FlagType.Pointer24Bit:
                case Data.FlagType.Pointer32Bit:
                    style.BackColor = Color.Orchid;
                    break;
                case Data.FlagType.Text:
                    style.BackColor = Color.Aquamarine;
                    break;
                case Data.FlagType.Empty:
                    style.BackColor = Color.DarkSlateGray;
                    style.ForeColor = Color.LightGray;
                    break;
            }

            if (selOffset >= 0 && selOffset < Project.Data.GetROMSize())
            {
                if (column == 1
                    //&& (Project.Data.GetFlag(selOffset) == Data.FlagType.Opcode || Project.Data.GetFlag(selOffset) == Data.FlagType.Unreached)
                    && Project.Data.ConvertSNEStoPC(Project.Data.GetIntermediateAddressOrPointer(selOffset)) == offset
                ) style.BackColor = Color.DeepPink;

                if (column == 6
                    //&& (Project.Data.GetFlag(offset) == Data.FlagType.Opcode || Project.Data.GetFlag(offset) == Data.FlagType.Unreached)
                    && Project.Data.ConvertSNEStoPC(Project.Data.GetIntermediateAddressOrPointer(offset)) == selOffset
                ) style.BackColor = Color.DeepPink;
            }
        }

        private void table_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            int row = e.RowIndex + ViewOffset;
            if (row < 0 || row >= Project.Data.GetROMSize()) return;
            PaintCell(row, e.CellStyle, e.ColumnIndex, table.CurrentCell.RowIndex + ViewOffset);
        }

        public void SelectOffset(int offset, int column = -1)
        {
            var col = column == -1 ? table.CurrentCell.ColumnIndex : column;
            if (offset < ViewOffset)
            {
                ViewOffset = offset;
                UpdateDataGridView();
                table.CurrentCell = table.Rows[0].Cells[col];
            }
            else if (offset >= ViewOffset + rowsToShow)
            {
                ViewOffset = offset - rowsToShow + 1;
                UpdateDataGridView();
                table.CurrentCell = table.Rows[rowsToShow - 1].Cells[col];
            }
            else
            {
                table.CurrentCell = table.Rows[offset - ViewOffset].Cells[col];
            }
        }

        private void Step(int offset)
        {
            ProjectController.MarkChanged();;
            SelectOffset(Project.Data.Step(offset, false, false, offset - 1));
            UpdatePercent();
            UpdateWindowTitle();
        }

        private void StepIn(int offset)
        {
            ProjectController.MarkChanged();
            SelectOffset(Project.Data.Step(offset, true, false, offset - 1));
            UpdatePercent();
            UpdateWindowTitle();
        }

        private void AutoStepSafe(int offset)
        {
            ProjectController.MarkChanged();
            var destination = Project.Data.AutoStep(offset, false, 0);
            if (MoveWithStep) 
                SelectOffset(destination);
            UpdatePercent();
            UpdateWindowTitle();
        }

        private void AutoStepHarsh(int offset)
        {
            HarshAutoStep harsh = new HarshAutoStep(offset, Project.Data);
            DialogResult result = harsh.ShowDialog();
            if (result == DialogResult.OK)
            {
                ProjectController.MarkChanged();
                int destination = Project.Data.AutoStep(harsh.GetOffset(), true, harsh.GetCount());
                if (MoveWithStep) SelectOffset(destination);
                UpdatePercent();
                UpdateWindowTitle();
            }
        }

        private void Mark(int offset)
        {
            ProjectController.MarkChanged();
            SelectOffset(Project.Data.Mark(offset, markFlag, RomUtil.TypeStepSize(markFlag)));
            UpdatePercent();
            UpdateWindowTitle();
        }

        private void MarkMany(int offset, int column)
        {
            var mark = new MarkManyDialog(offset, column, Project.Data);
            var result = mark.ShowDialog();
            if (result != DialogResult.OK) 
                return;

            ProjectController.MarkChanged();

            var destination = 0;
            var col = mark.GetProperty();
            switch (col)
            {
                case 0:
                    destination = Project.Data.Mark(mark.GetOffset(), (Data.FlagType) mark.GetValue(), mark.GetCount());
                    break;
                case 1:
                    destination = Project.Data.MarkDataBank(mark.GetOffset(), (int) mark.GetValue(), mark.GetCount());
                    break;
                case 2:
                    destination = Project.Data.MarkDirectPage(mark.GetOffset(), (int) mark.GetValue(), mark.GetCount());
                    break;
                case 3:
                    destination = Project.Data.MarkMFlag(mark.GetOffset(), (bool) mark.GetValue(), mark.GetCount());
                    break;
                case 4:
                    destination = Project.Data.MarkXFlag(mark.GetOffset(), (bool) mark.GetValue(), mark.GetCount());
                    break;
                case 5:
                    destination = Project.Data.MarkArchitechture(mark.GetOffset(), (Data.Architecture) mark.GetValue(),
                        mark.GetCount());
                    break;
            }

            if (MoveWithStep) 
                SelectOffset(destination);

            UpdatePercent();
            UpdateWindowTitle();
            InvalidateTable();
        }

        private void GoToIntermediateAddress(int offset)
        {
            var ia = Project.Data.GetIntermediateAddressOrPointer(offset);
            if (ia < 0) 
                return;

            var pc = Project.Data.ConvertSNEStoPC(ia);
            if (pc < 0) 
                return;

            SelectOffset(pc, 1);
        }

        private void GoToUnreached(bool end, bool direction)
        {
            int offset = table.CurrentCell.RowIndex + ViewOffset;
            int size = Project.Data.GetROMSize();
            int unreached = end ? (direction ? 0 : size - 1) : offset;

            if (direction)
            {
                if (!end)
                    while (unreached < size - 1 && Project.Data.GetFlag(unreached) == Data.FlagType.Unreached)
                        unreached++;
                while (unreached < size - 1 && Project.Data.GetFlag(unreached) != Data.FlagType.Unreached) unreached++;
            }
            else
            {
                if (unreached > 0) unreached--;
                while (unreached > 0 && Project.Data.GetFlag(unreached) != Data.FlagType.Unreached) unreached--;
            }

            while (unreached > 0 && Project.Data.GetFlag(unreached - 1) == Data.FlagType.Unreached) unreached--;

            if (Project.Data.GetFlag(unreached) == Data.FlagType.Unreached) SelectOffset(unreached, 1);
        }

        private void visualMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
            // visual map window
        }

        private void graphicsWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
            // graphics view window
        }

        private void stepOverToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Project?.Data == null || Project.Data.GetROMSize() <= 0) return;
            Step(table.CurrentCell.RowIndex + ViewOffset);
        }

        private void stepInToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Project?.Data == null || Project.Data.GetROMSize() <= 0) return;
            StepIn(table.CurrentCell.RowIndex + ViewOffset);
        }

        private void autoStepSafeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Project?.Data == null || Project.Data.GetROMSize() <= 0) return;
            AutoStepSafe(table.CurrentCell.RowIndex + ViewOffset);
        }

        private void autoStepHarshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Project?.Data == null || Project.Data.GetROMSize() <= 0) return;
            AutoStepHarsh(table.CurrentCell.RowIndex + ViewOffset);
        }

        private void gotoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Project?.Data == null || Project.Data.GetROMSize() <= 0) 
                return;

            var go = new GotoDialog(ViewOffset + table.CurrentCell.RowIndex, Project.Data);
            var result = go.ShowDialog();
            if (result != DialogResult.OK) 
                return;

            int offset = go.GetPcOffset();
            if (offset >= 0 && offset < Project.Data.GetROMSize()) 
                SelectOffset(offset);
            else
                MessageBox.Show("That offset is out of range.", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
        }

        private void gotoIntermediateAddressToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Project?.Data == null || Project.Data.GetROMSize() <= 0) return;
            GoToIntermediateAddress(table.CurrentCell.RowIndex + ViewOffset);
        }

        private void gotoFirstUnreachedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GoToUnreached(true, true);
        }

        private void gotoNearUnreachedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GoToUnreached(false, false);
        }

        private void gotoNextUnreachedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GoToUnreached(false, true);
        }

        private void markOneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Project?.Data == null || Project.Data.GetROMSize() <= 0) return;
            Mark(table.CurrentCell.RowIndex + ViewOffset);
        }

        private void markManyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Project?.Data == null || Project.Data.GetROMSize() <= 0) return;
            MarkMany(table.CurrentCell.RowIndex + ViewOffset, 7);
        }

        private void addLabelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            table.CurrentCell = table.Rows[table.CurrentCell.RowIndex].Cells[0];
            table.BeginEdit(true);
        }

        private void setDataBankToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Project?.Data == null || Project.Data.GetROMSize() <= 0) return;
            MarkMany(table.CurrentCell.RowIndex + ViewOffset, 8);
        }

        private void setDirectPageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Project?.Data == null || Project.Data.GetROMSize() <= 0) return;
            MarkMany(table.CurrentCell.RowIndex + ViewOffset, 9);
        }

        private void toggleAccumulatorSizeMToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Project?.Data == null || Project.Data.GetROMSize() <= 0) return;
            MarkMany(table.CurrentCell.RowIndex + ViewOffset, 10);
        }

        private void toggleIndexSizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Project?.Data == null || Project.Data.GetROMSize() <= 0) return;
            MarkMany(table.CurrentCell.RowIndex + ViewOffset, 11);
        }

        private void addCommentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            table.CurrentCell = table.Rows[table.CurrentCell.RowIndex].Cells[12];
            table.BeginEdit(true);
        }

        private void fixMisalignedInstructionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Project?.Data == null || Project.Data.GetROMSize() <= 0) 
                return;

            var mis = new MisalignmentChecker(Project.Data);
            var result = mis.ShowDialog();

            if (result != DialogResult.OK) 
                return;
            
            int count = Project.Data.FixMisalignedFlags();

            if (count > 0)
                ProjectController.MarkChanged();

            InvalidateTable();
            MessageBox.Show($"Modified {count} flags!", "Done!", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void rescanForInOutPointsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Project?.Data == null || Project.Data.GetROMSize() <= 0) 
                return;

            var point = new InOutPointChecker();
            if (point.ShowDialog() != DialogResult.OK) 
                return;

            Project.Data.RescanInOutPoints();
            ProjectController.MarkChanged();
            InvalidateTable();
            MessageBox.Show("Scan complete!", "Done!", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void unreachedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Unreached;
            UpdateMarkerLabel();
        }

        private void opcodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Opcode;
            UpdateMarkerLabel();
        }

        private void operandToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Operand;
            UpdateMarkerLabel();
        }

        private void bitDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Data8Bit;
            UpdateMarkerLabel();
        }

        private void graphicsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Graphics;
            UpdateMarkerLabel();
        }

        private void musicToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Music;
            UpdateMarkerLabel();
        }

        private void emptyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Empty;
            UpdateMarkerLabel();
        }

        private void bitDataToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Data16Bit;
            UpdateMarkerLabel();
        }

        private void wordPointerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Pointer16Bit;
            UpdateMarkerLabel();
        }

        private void bitDataToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Data24Bit;
            UpdateMarkerLabel();
        }

        private void longPointerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Pointer24Bit;
            UpdateMarkerLabel();
        }

        private void bitDataToolStripMenuItem3_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Data32Bit;
            UpdateMarkerLabel();
        }

        private void dWordPointerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Pointer32Bit;
            UpdateMarkerLabel();
        }

        private void textToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Text;
            UpdateMarkerLabel();
        }

        private void moveWithStepToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MoveWithStep = !MoveWithStep;
            moveWithStepToolStripMenuItem.Checked = MoveWithStep;
        }

        public OpenFileDialog GetRomOpenFileDialog()
        {
            return openFileDialog;
        }

        // sub windows
        public AliasList aliasList;

        private void EnableSubWindows()
        {
            labelListToolStripMenuItem.Enabled = true;
        }

        private void labelListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            aliasList.Show();
        }

        private void ImportUsageMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openUsageMapFile.ShowDialog() != DialogResult.OK) 
                return;

            var num_modified_flags = Project.Data.ImportUsageMap(File.ReadAllBytes(openUsageMapFile.FileName));

            if (num_modified_flags > 0)
                ProjectController.MarkChanged();

            MessageBox.Show($"Modified total {num_modified_flags} flags!", "Done",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ImportTraceLogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openTraceLogDialog.Multiselect = true;
            if (openTraceLogDialog.ShowDialog() != DialogResult.OK)
                return;

            var totalLinesSoFar = 0L;

            // caution: trace logs can be gigantic, even a few seconds can be > 1GB
            LargeFilesReader.ReadFilesLines(openTraceLogDialog.FileNames, delegate (string line)
            {
                totalLinesSoFar += Project.Data.ImportTraceLogLine(line);
            });

            if (totalLinesSoFar > 0)
                ProjectController.MarkChanged();

            MessageBox.Show(
            $"Modified total {totalLinesSoFar} flags from {openTraceLogDialog.FileNames.Length} files!",
            "Done",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void openLastProjectAutomaticallyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.OpenLastFileAutomatically = openLastProjectAutomaticallyToolStripMenuItem.Checked;
            Settings.Default.Save();
            UpdateUIFromSettings();
        }

        private void toolStripOpenLast_Click(object sender, EventArgs e)
        {
            openLastProject();
        }

        public string AskToSelectNewRomFilename(string promptSubject, string promptText)
        {
            return GuiUtil.AskIfWeShouldSelectFilename(promptSubject, promptText, 
                () => GuiUtil.PromptToSelectFile(Project.ProjectFileName)
                );
        }
    }
}
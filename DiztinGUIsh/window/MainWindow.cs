using DiztinGUIsh.window;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiztinGUIsh
{
    public partial class MainWindow : Form
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = !ContinueUnsavedChanges();
        }

        private void MainWindow_SizeChanged(object sender, EventArgs e)
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
            viewOffset = 0;
            rowsToShow = ((table.Height - table.ColumnHeadersHeight) / table.RowTemplate.Height);

            // https://stackoverflow.com/a/1506066

            typeof(DataGridView).InvokeMember(
                "DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                null,
                table,
                new object[] { true });

            aliasList = new AliasList(this);
        }

        public void UpdateWindowTitle()
        {
            this.Text =
                (Project.unsavedChanges ? "*" : "") + 
                (Project.currentFile == null ? "New Project" : Project.currentFile) +
                " - DiztinGUIsh";
        }

        private bool ContinueUnsavedChanges()
        {
            if (Project.unsavedChanges)
            {
                DialogResult confirm = MessageBox.Show("You have unsaved changes. They will be lost if you continue.", "Unsaved Changes", MessageBoxButtons.OKCancel);
                return confirm == DialogResult.OK;
            }
            return true;
        }

        public void TriggerSaveOptions(bool save, bool saveas)
        {
            saveProjectToolStripMenuItem.Enabled = save;
            saveProjectAsToolStripMenuItem.Enabled = saveas;
            exportLogToolStripMenuItem.Enabled = true;
        }

        private void newProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ContinueUnsavedChanges())
            {
                openROMFile.InitialDirectory = Project.currentFile;
                DialogResult result = openROMFile.ShowDialog();
                if (result == DialogResult.OK)
                {
                    if (Project.NewProject(openROMFile.FileName))
                    {
                        TriggerSaveOptions(false, true);
                        UpdateWindowTitle();
                        UpdateDataGridView();
                        UpdatePercent();
                        table.Invalidate();
                        EnableSubWindows();
                    }
                }
            }
        }

        private void openProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ContinueUnsavedChanges())
            {
                openProjectFile.InitialDirectory = Project.currentFile;
                DialogResult result = openProjectFile.ShowDialog();
                if (result == DialogResult.OK)
                {
                    openProject(openProjectFile.FileName);
                }
            }
        }

        public void openProject(string filename)
        {
            if (Project.TryOpenProject(filename, openROMFile))
            {
                TriggerSaveOptions(true, true);
                UpdateWindowTitle();
                UpdateDataGridView();
                UpdatePercent();
                table.Invalidate();
                EnableSubWindows();
            }
        }

        private void saveProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Project.SaveProject(Project.currentFile);
            UpdateWindowTitle();
        }

        private void saveProjectAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveProjectFile.InitialDirectory = Project.currentROMFile;
            DialogResult result = saveProjectFile.ShowDialog();
            if (result == DialogResult.OK && saveProjectFile.FileName != "")
            {
                Project.SaveProject(saveProjectFile.FileName);
                TriggerSaveOptions(true, true);
                UpdateWindowTitle();
            }
        }

        private void exportLogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportDisassembly export = new ExportDisassembly();
            DialogResult result = export.ShowDialog();
            if (result == DialogResult.OK)
            {
                string file = null, error = null;
                if (LogCreator.structure == LogCreator.FormatStructure.SingleFile)
                {
                    saveLogSingleFile.InitialDirectory = Project.currentFile;
                    result = saveLogSingleFile.ShowDialog();
                    if (result == DialogResult.OK && saveLogSingleFile.FileName != "")
                    {
                        file = saveLogSingleFile.FileName;
                        error = Path.GetDirectoryName(file) + "/error.txt";
                    }
                } else
                {
                    chooseLogFolder.SelectedPath = Path.GetDirectoryName(Project.currentFile);
                    result = chooseLogFolder.ShowDialog();
                    if (result == DialogResult.OK && chooseLogFolder.SelectedPath != "")
                    {
                        file = chooseLogFolder.SelectedPath + "/main.asm";
                        error = Path.GetDirectoryName(file) + "/error.txt";
                    }
                }

                if (file != null)
                {
                    int errors = 0;
                    using (StreamWriter sw = new StreamWriter(file))
                    using (StreamWriter er = new StreamWriter(error))
                    {
                        errors = LogCreator.CreateLog(sw, er);
                        if (errors > 0) MessageBox.Show("Disassembly created with errors. See errors.txt for details.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        else MessageBox.Show("Disassembly created successfully!", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    }
                    if (errors == 0) File.Delete(error);
                }
            }
        }

        private void viewHelpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var info = new ProcessStartInfo("help.html") { UseShellExecute = true };
                Process.Start(info);
            }
            catch (Exception)
            {
                MessageBox.Show("Can't find the help file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void githubToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var info = new ProcessStartInfo("https://github.com/Dotsarecool/DiztinGUIsh") { UseShellExecute = true };
            Process.Start(info);
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            About about = new About();
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
            int totalUnreached = 0, size = Data.GetROMSize();
            for (int i = 0; i < size; i++) if (Data.GetFlag(i) == Data.FlagType.Unreached) totalUnreached++;
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

        private int viewOffset;
        private int rowsToShow;

        private void UpdateDataGridView()
        {
            if (Data.GetROMSize() > 0)
            {
                rowsToShow = ((table.Height - table.ColumnHeadersHeight) / table.RowTemplate.Height);
                if (viewOffset + rowsToShow > Data.GetROMSize()) viewOffset = Data.GetROMSize() - rowsToShow;
                if (viewOffset < 0) viewOffset = 0;
                vScrollBar1.Enabled = true;
                vScrollBar1.Maximum = Data.GetROMSize() - rowsToShow;
                vScrollBar1.Value = viewOffset;
                table.RowCount = rowsToShow;
            }
        }

        private void table_MouseWheel(object sender, MouseEventArgs e)
        {
            if (Data.GetROMSize() <= 0) return;
            int selRow = table.CurrentCell.RowIndex + viewOffset, selCol = table.CurrentCell.ColumnIndex;
            int amount = e.Delta / 0x18;
            viewOffset -= amount;
            UpdateDataGridView();
            if (selRow < viewOffset) selRow = viewOffset;
            else if (selRow >= viewOffset + rowsToShow) selRow = viewOffset + rowsToShow - 1;
            table.CurrentCell = table.Rows[selRow - viewOffset].Cells[selCol];
            InvalidateTable();
        }

        private void vScrollBar1_ValueChanged(object sender, EventArgs e)
        {
            int selOffset = table.CurrentCell.RowIndex + viewOffset;
            viewOffset = vScrollBar1.Value;
            UpdateDataGridView();

            if (selOffset < viewOffset) table.CurrentCell = table.Rows[0].Cells[table.CurrentCell.ColumnIndex];
            else if (selOffset >= viewOffset + rowsToShow) table.CurrentCell = table.Rows[rowsToShow - 1].Cells[table.CurrentCell.ColumnIndex];
            else table.CurrentCell = table.Rows[selOffset - viewOffset].Cells[table.CurrentCell.ColumnIndex];

            InvalidateTable();
        }

        private void table_MouseDown(object sender, MouseEventArgs e)
        {
            InvalidateTable();
        }

        private void table_KeyDown(object sender, KeyEventArgs e)
        {
            if (Data.GetROMSize() <= 0) return;

            int offset = table.CurrentCell.RowIndex + viewOffset;
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
                    if (newOffset >= Data.GetROMSize()) newOffset = Data.GetROMSize() - 1;
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
                case Keys.S: Step(offset); break;
                case Keys.I: StepIn(offset); break;
                case Keys.A: AutoStepSafe(offset); break;
                case Keys.T: GoToIntermediateAddress(offset); break;
                case Keys.U: GoToUnreached(true, true); break;
                case Keys.H: GoToUnreached(false, false); break;
                case Keys.N: GoToUnreached(false, true); break;
                case Keys.K: Mark(offset); break;
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
                    Data.SetMFlag(offset, !Data.GetMFlag(offset));
                    break;
                case Keys.X:
                    Data.SetXFlag(offset, !Data.GetXFlag(offset));
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
            int row = e.RowIndex + viewOffset;
            if (row >= Data.GetROMSize()) return;
            switch (e.ColumnIndex)
            {
                case 0: e.Value = Data.GetLabel(Util.ConvertPCtoSNES(row)); break;
                case 1: e.Value = Util.NumberToBaseString(Util.ConvertPCtoSNES(row), Util.NumberBase.Hexadecimal, 6); break;
                case 2: e.Value = (char)Data.GetROMByte(row); break;
                case 3: e.Value = Util.NumberToBaseString(Data.GetROMByte(row), DisplayBase); break;
                case 4: e.Value = Util.PointToString(Data.GetInOutPoint(row)); break;
                case 5:
                    int len = Manager.GetInstructionLength(row);
                    if (row + len <= Data.GetROMSize()) e.Value = Util.GetInstruction(row);
                    else e.Value = "";
                    break;
                case 6:
                    int ia = Util.GetIntermediateAddressOrPointer(row);
                    if (ia >= 0) e.Value = Util.NumberToBaseString(ia, Util.NumberBase.Hexadecimal, 6);
                    else e.Value = "";
                    break;
                case 7: e.Value = Util.TypeToString(Data.GetFlag(row)); break;
                case 8: e.Value = Util.NumberToBaseString(Data.GetDataBank(row), Util.NumberBase.Hexadecimal, 2); break;
                case 9: e.Value = Util.NumberToBaseString(Data.GetDirectPage(row), Util.NumberBase.Hexadecimal, 4); break;
                case 10: e.Value = Util.BoolToSize(Data.GetMFlag(row)); break;
                case 11: e.Value = Util.BoolToSize(Data.GetXFlag(row)); break;
                case 12: e.Value = Data.GetComment(Util.ConvertPCtoSNES(row)); break;
            }
        }

        private void table_CellValuePushed(object sender, DataGridViewCellValueEventArgs e)
        {
            string value = e.Value as string;
            int result;
            int row = e.RowIndex + viewOffset;
            if (row >= Data.GetROMSize()) return;
            switch (e.ColumnIndex)
            {
                case 0: Data.AddLabel(Util.ConvertPCtoSNES(row), value, true); break; // todo (validate for valid label characters)
                case 8: if (int.TryParse(value, NumberStyles.HexNumber, null, out result)) Data.SetDataBank(row, result); break;
                case 9: if (int.TryParse(value, NumberStyles.HexNumber, null, out result)) Data.SetDirectPage(row, result); break;
                case 10: Data.SetMFlag(row, (value == "8" || value == "M")); break;
                case 11: Data.SetXFlag(row, (value == "8" || value == "X")); break;
                case 12: Data.AddComment(Util.ConvertPCtoSNES(row), value, true); break;
            }
            table.InvalidateRow(e.RowIndex);
        }

        private void table_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            int row = e.RowIndex + viewOffset;
            if (row < 0 || row >= Data.GetROMSize()) return;
            Util.PaintCell(row, e.CellStyle, e.ColumnIndex, table.CurrentCell.RowIndex + viewOffset);
        }

        public void SelectOffset(int offset, int column = -1)
        {
            int col = column == -1 ? table.CurrentCell.ColumnIndex : column;
            if (offset < viewOffset)
            {
                viewOffset = offset;
                UpdateDataGridView();
                table.CurrentCell = table.Rows[0].Cells[col];
            } else if (offset >= viewOffset + rowsToShow)
            {
                viewOffset = offset - rowsToShow + 1;
                UpdateDataGridView();
                table.CurrentCell = table.Rows[rowsToShow - 1].Cells[col];
            } else
            {
                table.CurrentCell = table.Rows[offset - viewOffset].Cells[col];
            }
        }

        private void Step(int offset)
        {
            SelectOffset(Manager.Step(offset, false, false, offset - 1));
            UpdatePercent();
            UpdateWindowTitle();
        }

        private void StepIn(int offset)
        {
            SelectOffset(Manager.Step(offset, true, false, offset - 1));
            UpdatePercent();
            UpdateWindowTitle();
        }

        private void AutoStepSafe(int offset)
        {
            int destination = Manager.AutoStep(offset, false, 0);
            if (MoveWithStep) SelectOffset(destination);
            UpdatePercent();
            UpdateWindowTitle();
        }

        private void AutoStepHarsh(int offset)
        {
            HarshAutoStep harsh = new HarshAutoStep(offset);
            DialogResult result = harsh.ShowDialog();
            if (result == DialogResult.OK)
            {
                int destination = Manager.AutoStep(harsh.GetOffset(), true, harsh.GetCount());
                if (MoveWithStep) SelectOffset(destination);
                UpdatePercent();
                UpdateWindowTitle();
            }
        }

        private void Mark(int offset)
        {
            SelectOffset(Manager.Mark(offset, markFlag, Util.TypeStepSize(markFlag)));
            UpdatePercent();
            UpdateWindowTitle();
        }

        private void MarkMany(int offset, int column)
        {
            MarkManyDialog mark = new MarkManyDialog(offset, column);
            DialogResult result = mark.ShowDialog();
            if (result == DialogResult.OK)
            {
                int destination = 0;
                int col = mark.GetProperty();
                switch (col)
                {
                    case 0:
                        destination = Manager.Mark(mark.GetOffset(), (Data.FlagType)mark.GetValue(), mark.GetCount());
                        break;
                    case 1:
                        destination = Manager.MarkDataBank(mark.GetOffset(), (int)mark.GetValue(), mark.GetCount());
                        break;
                    case 2:
                        destination = Manager.MarkDirectPage(mark.GetOffset(), (int)mark.GetValue(), mark.GetCount());
                        break;
                    case 3:
                        destination = Manager.MarkMFlag(mark.GetOffset(), (bool)mark.GetValue(), mark.GetCount());
                        break;
                    case 4:
                        destination = Manager.MarkXFlag(mark.GetOffset(), (bool)mark.GetValue(), mark.GetCount());
                        break;
                    case 5:
                        destination = Manager.MarkArchitechture(mark.GetOffset(), (Data.Architechture)mark.GetValue(), mark.GetCount());
                        break;
                }
                if (MoveWithStep) SelectOffset(destination);
                UpdatePercent();
                UpdateWindowTitle();
                InvalidateTable();
            }
        }

        private void GoToIntermediateAddress(int offset)
        {
            int ia = Util.GetIntermediateAddressOrPointer(offset);
            if (ia >= 0)
            {
                int pc = Util.ConvertSNEStoPC(ia);
                if (pc >= 0)
                {
                    SelectOffset(pc, 1);
                }
            }
        }

        private void GoToUnreached(bool end, bool direction)
        {
            int offset = table.CurrentCell.RowIndex + viewOffset;
            int size = Data.GetROMSize();
            int unreached = end ? (direction ? 0 : size - 1) : offset;

            if (direction)
            {
                if (!end) while (unreached < size - 1 && Data.GetFlag(unreached) == Data.FlagType.Unreached) unreached++;
                while (unreached < size - 1 && Data.GetFlag(unreached) != Data.FlagType.Unreached) unreached++;
            } else
            {
                if (unreached > 0) unreached--;
                while (unreached > 0 && Data.GetFlag(unreached) != Data.FlagType.Unreached) unreached--;
            }

            while (unreached > 0 && Data.GetFlag(unreached - 1) == Data.FlagType.Unreached) unreached--;

            if (Data.GetFlag(unreached) == Data.FlagType.Unreached) SelectOffset(unreached, 1);
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
            if (Data.GetROMSize() <= 0) return;
            Step(table.CurrentCell.RowIndex + viewOffset);
        }

        private void stepInToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Data.GetROMSize() <= 0) return;
            StepIn(table.CurrentCell.RowIndex + viewOffset);
        }

        private void autoStepSafeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Data.GetROMSize() <= 0) return;
            AutoStepSafe(table.CurrentCell.RowIndex + viewOffset);
        }

        private void autoStepHarshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Data.GetROMSize() <= 0) return;
            AutoStepHarsh(table.CurrentCell.RowIndex + viewOffset);
        }

        private void gotoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Data.GetROMSize() <= 0) return;
            GotoDialog go = new GotoDialog(viewOffset + table.CurrentCell.RowIndex);
            DialogResult result = go.ShowDialog();
            if (result == DialogResult.OK)
            {
                int offset = go.GetOffset();
                if (offset >= 0 && offset < Data.GetROMSize()) SelectOffset(offset); 
                else MessageBox.Show("That offset is out of range.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void gotoIntermediateAddressToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Data.GetROMSize() <= 0) return;
            GoToIntermediateAddress(table.CurrentCell.RowIndex + viewOffset);
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
            if (Data.GetROMSize() <= 0) return;
            Mark(table.CurrentCell.RowIndex + viewOffset);
        }

        private void markManyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Data.GetROMSize() <= 0) return;
            MarkMany(table.CurrentCell.RowIndex + viewOffset, 7);
        }

        private void addLabelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            table.CurrentCell = table.Rows[table.CurrentCell.RowIndex].Cells[0];
            table.BeginEdit(true);
        }

        private void setDataBankToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Data.GetROMSize() <= 0) return;
            MarkMany(table.CurrentCell.RowIndex + viewOffset, 8);
        }

        private void setDirectPageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Data.GetROMSize() <= 0) return;
            MarkMany(table.CurrentCell.RowIndex + viewOffset, 9);
        }

        private void toggleAccumulatorSizeMToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Data.GetROMSize() <= 0) return;
            MarkMany(table.CurrentCell.RowIndex + viewOffset, 10);
        }

        private void toggleIndexSizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Data.GetROMSize() <= 0) return;
            MarkMany(table.CurrentCell.RowIndex + viewOffset, 11);
        }

        private void addCommentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            table.CurrentCell = table.Rows[table.CurrentCell.RowIndex].Cells[12];
            table.BeginEdit(true);
        }

        private void fixMisalignedInstructionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Data.GetROMSize() <= 0) return;
            MisalignmentChecker mis = new MisalignmentChecker();
            DialogResult result = mis.ShowDialog();
            if (result == DialogResult.OK)
            {
                int count = Manager.FixMisalignedFlags();
                InvalidateTable();
                MessageBox.Show(string.Format("Modified {0} flags!", count), "Done!", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void rescanForInOutPointsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Data.GetROMSize() <= 0) return;
            InOutPointChecker point = new InOutPointChecker();
            DialogResult result = point.ShowDialog();
            if (result == DialogResult.OK)
            {
                Manager.RescanInOutPoints();
                InvalidateTable();
                MessageBox.Show("Scan complete!", "Done!", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
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
            return openROMFile;
        }

        // sub windows
        AliasList aliasList;

        private void EnableSubWindows()
        {
            labelListToolStripMenuItem.Enabled = true;
        }

        private void labelListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            aliasList.Show();
        }
    }
}

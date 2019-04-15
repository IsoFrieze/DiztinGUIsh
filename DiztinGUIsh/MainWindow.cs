using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
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

        private void MainWindow_SizeChanged(object sender, EventArgs e)
        {
            dataGridView1.Height = this.Height - 85;
            dataGridView1.Width = this.Width - 33;
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
            dataGridView1.CellValueNeeded += new DataGridViewCellValueEventHandler(dataGridView1_CellValueNeeded);
            dataGridView1.CellValuePushed += new DataGridViewCellValueEventHandler(dataGridView1_CellValuePushed);
            viewOffset = 0;
            rowsToShow = (dataGridView1.Height / dataGridView1.RowTemplate.Height) + 1;
            typeof(DataGridView).InvokeMember(
                "DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                null,
                dataGridView1,
                new object[] { true });
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
        }

        private void newProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ContinueUnsavedChanges())
            {
                DialogResult result = openFileDialog1.ShowDialog();
                if (result == DialogResult.OK)
                {
                    if (Project.NewProject(openFileDialog1.FileName))
                    {
                        TriggerSaveOptions(false, true);
                        UpdateWindowTitle();
                        UpdateDataGridView();
                        dataGridView1.Invalidate();
                    }
                }
            }
        }

        private void openProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ContinueUnsavedChanges())
            {
                DialogResult result = openFileDialog2.ShowDialog();
                if (result == DialogResult.OK)
                {
                    if (Project.TryOpenProject(openFileDialog2.FileName))
                    {
                        TriggerSaveOptions(true, true);
                        UpdateWindowTitle();
                        UpdateDataGridView();
                        dataGridView1.Invalidate();
                    }
                }
            }
        }

        private void saveProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Project.SaveProject(Project.currentFile);
            UpdateWindowTitle();
        }

        private void saveProjectAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result = saveFileDialog1.ShowDialog();
            if (result == DialogResult.OK && saveFileDialog1.FileName != "")
            {
                Project.SaveProject(saveFileDialog1.FileName);
                TriggerSaveOptions(true, true);
                UpdateWindowTitle();
            }
        }

        private void viewHelpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
            // open help page
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            About about = new About();
            about.ShowDialog();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ContinueUnsavedChanges())
            {
                Application.Exit();
            }
        }

        private Util.NumberBase DisplayBase = Util.NumberBase.Hexadecimal;
        private Data.FlagType markFlag = Data.FlagType.Data8Bit;

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
            dataGridView1.Invalidate();
        }

        public void UpdatePercent()
        {
            int totalUnreached = 0, size = Data.GetROMSize();
            for (int i = 0; i < size; i++) if (Data.GetFlag(i) == Data.FlagType.Unreached) totalUnreached++;
            percentComplete.Text = string.Format("{0:N3}%", (size - totalUnreached) * 100.0 / size);
        }

        // DataGridView

        private int viewOffset;
        private int rowsToShow;

        private void UpdateDataGridView()
        {
            if (Data.GetROMSize() > 0)
            {
                rowsToShow = (dataGridView1.Height / dataGridView1.RowTemplate.Height) + 1;
                if (viewOffset + rowsToShow > Data.GetROMSize()) viewOffset = Data.GetROMSize() - rowsToShow;
                if (viewOffset < 0) viewOffset = 0;
                vScrollBar1.Enabled = true;
                vScrollBar1.Maximum = Data.GetROMSize() - rowsToShow;
                vScrollBar1.Value = viewOffset;
                dataGridView1.RowCount = rowsToShow;
            }
        }

        private void dataGridView1_MouseWheel(object sender, MouseEventArgs e)
        {
            viewOffset -= e.Delta / 0x18;
            UpdateDataGridView();
            dataGridView1.Invalidate();
        }

        private void vScrollBar1_ValueChanged(object sender, EventArgs e)
        {
            viewOffset = vScrollBar1.Value;
            UpdateDataGridView();
            dataGridView1.Invalidate();
        }

        private void dataGridView1_KeyDown(object sender, KeyEventArgs e)
        {
            if (dataGridView1.SelectedRows.Count < 0) return;
            int offset = dataGridView1.SelectedRows[0].Index + viewOffset;
            int amount = 0x01;
            switch (e.KeyCode)
            {
                case Keys.PageUp:
                case Keys.Up:
                    amount = e.KeyCode == Keys.Up ? 0x01 : 0x10;
                    int undershot = dataGridView1.SelectedCells[0].RowIndex - amount;
                    if (undershot < 0) viewOffset += undershot;
                    if (viewOffset < 0) viewOffset = 0;
                    UpdateDataGridView();
                    break;
                case Keys.PageDown:
                case Keys.Down:
                    amount = e.KeyCode == Keys.Down ? 0x01 : 0x10;
                    int overshot = dataGridView1.SelectedCells[0].RowIndex + amount - rowsToShow + 1;
                    if (overshot > 0) viewOffset += overshot;
                    if (viewOffset + rowsToShow > Data.GetROMSize()) viewOffset = Data.GetROMSize() - rowsToShow;
                    UpdateDataGridView();
                    break;
                case Keys.S: Manager.Step(offset, false); break;
                case Keys.I: Manager.Step(offset, true); break;
                case Keys.A: Manager.AutoStep(offset, false); break;
                case Keys.F:
                    // TODO
                    // goto effective address
                    break;
                case Keys.U:
                    // TODO
                    // goto first unreached
                    break;
                case Keys.N:
                    // TODO
                    // goto near unreached
                    break;
                case Keys.K: Manager.Mark(offset, markFlag, 1); break;
                case Keys.L:
                    // TODO
                    // jump to label box
                    break;
                case Keys.B:
                    // TODO
                    // jump to data bank box
                    break;
                case Keys.D:
                    // TODO
                    // jump to direct page box
                    break;
                case Keys.M:
                    // TODO
                    // jump to M flag box
                    break;
                case Keys.X:
                    // TODO
                    // jump to X flag box
                    break;
                case Keys.C:
                    // TODO
                    // jump to comment box
                    break;
            }
            dataGridView1.Invalidate();
        }

        private void dataGridView1_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            int row = e.RowIndex + viewOffset;
            if (row >= Data.GetROMSize()) return;
            switch (e.ColumnIndex)
            {
                case 0: e.Value = Data.GetLabel(row); break;
                case 1: e.Value = Util.NumberToBaseString(Util.ConvertPCtoSNES(row), Util.NumberBase.Hexadecimal, 6); break;
                case 2: e.Value = (char)Data.GetROMByte(row); break;
                case 3: e.Value = Util.NumberToBaseString(Data.GetROMByte(row), DisplayBase); break;
                case 4: e.Value = Util.PointToString(Data.GetInOutPoint(row)); break;
                case 5: e.Value = Util.GetInstruction(row); break; // TODO
                case 6:
                    int ea = Util.GetEffectiveAddress(row);
                    if (ea >= 0) e.Value = Util.NumberToBaseString(ea, Util.NumberBase.Hexadecimal, 6);
                    else e.Value = "";
                    break;
                case 7: e.Value = Util.TypeToString(Data.GetFlag(row)); break;
                case 8: e.Value = Util.NumberToBaseString(Data.GetDataBank(row), Util.NumberBase.Hexadecimal, 2); break;
                case 9: e.Value = Util.NumberToBaseString(Data.GetDirectPage(row), Util.NumberBase.Hexadecimal, 4); break;
                case 10: e.Value = Util.BoolToSize(Data.GetMFlag(row)); break;
                case 11: e.Value = Util.BoolToSize(Data.GetXFlag(row)); break;
                case 12: e.Value = Data.GetComment(row); break;
            }
        }

        private void dataGridView1_CellValuePushed(object sender, DataGridViewCellValueEventArgs e)
        {
            string value = e.Value as string;
            int result;
            int row = e.RowIndex + viewOffset;
            if (row >= Data.GetROMSize()) return;
            switch (e.ColumnIndex)
            {
                case 0: Data.AddLabel(row, value); break;
                case 8: if (int.TryParse(value, NumberStyles.HexNumber, null, out result)) Data.SetDataBank(row, result); break;
                case 9: if (int.TryParse(value, NumberStyles.HexNumber, null, out result)) Data.SetDirectPage(row, result); break;
                case 10: Data.SetMFlag(row, (value == "8" || value == "M")); break;
                case 11: Data.SetXFlag(row, (value == "8" || value == "X")); break;
                case 12: Data.AddComment(row, value); break;
            }
            dataGridView1.InvalidateRow(e.RowIndex);
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
            if (dataGridView1.SelectedRows.Count < 0) return;
            int offset = dataGridView1.SelectedRows[0].Index + viewOffset;
            Manager.Step(offset, false);
        }

        private void stepInToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count < 0) return;
            int offset = dataGridView1.SelectedRows[0].Index + viewOffset;
            Manager.Step(offset, true);
        }

        private void autoStepSafeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count < 0) return;
            int offset = dataGridView1.SelectedRows[0].Index + viewOffset;
            Manager.AutoStep(offset, false);
        }

        private void autoStepHarshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count < 0) return;
            int offset = dataGridView1.SelectedRows[0].Index + viewOffset;
            Manager.AutoStep(offset, true);
        }

        private void gotoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
            // goto window
        }

        private void gotoEffectiveAddressToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
            // goto effective address
        }

        private void gotoFirstUnreachedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
            // goto first unreached
        }

        private void gotoNearUnreachedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
            // goto near unreached
        }

        private void markOneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count < 0) return;
            int offset = dataGridView1.SelectedRows[0].Index + viewOffset;
            Manager.Mark(offset, markFlag, 1);
        }

        private void markManyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
            // mark many window
        }

        private void addLabelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
            // jump to label box
        }

        private void setDataBankToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
            // set many data bank window
        }

        private void setDirectPageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
            // set many direct page window
        }

        private void toggleAccumulatorSizeMToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
            // set many M flag window
        }

        private void toggleIndexSizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
            // set many X flag window
        }

        private void addCommentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
            // jump to comment box
        }

        private void fixMisalignedInstructionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
            // fix misaligned instructions dialog
        }

        private void unreachedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Unreached;
        }

        private void opcodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Opcode;
        }

        private void operandToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Operand;
        }

        private void bitDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Data8Bit;
        }

        private void graphicsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Graphics;
        }

        private void musicToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Music;
        }

        private void emptyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Empty;
        }

        private void bitDataToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Data16Bit;
        }

        private void wordPointerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Pointer16Bit;
        }

        private void bitDataToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Data24Bit;
        }

        private void longPointerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Pointer24Bit;
        }

        private void bitDataToolStripMenuItem3_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Data32Bit;
        }

        private void dWordPointerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Pointer32Bit;
        }

        private void textToolStripMenuItem_Click(object sender, EventArgs e)
        {
            markFlag = Data.FlagType.Text;
        }
    }
}

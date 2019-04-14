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

        public static Util.NumberBase DisplayBase = Util.NumberBase.Hexadecimal;

        private void decimalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DisplayBase = Util.NumberBase.Decimal;
            decimalToolStripMenuItem.Checked = true;
            hexadecimalToolStripMenuItem.Checked = false;
            binaryToolStripMenuItem.Checked = false;
        }

        private void hexadecimalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DisplayBase = Util.NumberBase.Hexadecimal;
            decimalToolStripMenuItem.Checked = false;
            hexadecimalToolStripMenuItem.Checked = true;
            binaryToolStripMenuItem.Checked = false;
        }

        private void binaryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DisplayBase = Util.NumberBase.Binary;
            decimalToolStripMenuItem.Checked = false;
            hexadecimalToolStripMenuItem.Checked = false;
            binaryToolStripMenuItem.Checked = true;
        }

        // DataGridView

        private int viewOffset;
        private int rowsToShow;

        private void UpdateDataGridView()
        {
            rowsToShow = (dataGridView1.Height / dataGridView1.RowTemplate.Height) + 1;
            if (viewOffset + rowsToShow > Data.GetROMSize()) viewOffset = Data.GetROMSize() - rowsToShow;
            if (viewOffset < 0) viewOffset = 0;
            vScrollBar1.Enabled = true;
            vScrollBar1.Maximum = Data.GetROMSize() - rowsToShow;
            vScrollBar1.Value = viewOffset;
            if (Data.GetROMSize() > 0) dataGridView1.RowCount = rowsToShow;
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
                case 4: e.Value = "Instruction WIP"; break; // TODO
                case 5: e.Value = Util.NumberToBaseString(row, Util.NumberBase.Hexadecimal, 6); break; // TODO
                case 6: e.Value = Util.TypeToString(Data.GetFlag(row)); break;
                case 7: e.Value = Util.NumberToBaseString(Data.GetDataBank(row), Util.NumberBase.Hexadecimal, 2); break;
                case 8: e.Value = Util.NumberToBaseString(Data.GetDirectPage(row), Util.NumberBase.Hexadecimal, 4); break;
                case 9: e.Value = Util.BoolToSize(Data.GetXFlag(row)); break;
                case 10: e.Value = Util.BoolToSize(Data.GetMFlag(row)); break;
                case 11: e.Value = Data.GetComment(row); break;
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
                case 7: if (int.TryParse(value, NumberStyles.HexNumber, null, out result)) Data.SetDataBank(row, result); break;
                case 8: if (int.TryParse(value, NumberStyles.HexNumber, null, out result)) Data.SetDirectPage(row, result); break;
                case 9: Data.SetXFlag(row, (value == "8" || value == "X")); break;
                case 10: Data.SetXFlag(row, (value == "8" || value == "M")); break;
                case 11: Data.AddComment(row, value); break;
            }
        }
    }
}

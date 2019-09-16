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

namespace DiztinGUIsh.window
{
    public partial class AliasList : Form
    {
        // single instance
        public static AliasList me;

        private MainWindow mw;
        private bool locked = false;
        private int currentlyEditing = -1;

        public AliasList(MainWindow main)
        {
            if (me == null)
            {
                me = this;
                mw = main;
                InitializeComponent();
            }
        }

        private void AliasList_Load(object sender, EventArgs e)
        {

        }

        private void AliasList_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        private void AliasList_Resize(object sender, EventArgs e)
        {
            int h = Height - 68 - 22;
            dataGridView1.Height = h;
        }

        private void jump_Click(object sender, EventArgs e)
        {
            if (int.TryParse((string)dataGridView1.SelectedRows[0].Cells[0].Value, NumberStyles.HexNumber, null, out int val))
            {
                int offset = Util.ConvertSNEStoPC(val);
                if (offset >= 0)
                {
                    mw.SelectOffset(offset);
                }
            }
        }

        private void dataGridView1_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            if (int.TryParse((string)dataGridView1.Rows[e.Row.Index].Cells[0].Value, NumberStyles.HexNumber, null, out int val))
            {
                int offset = Util.ConvertSNEStoPC(val);
                if (offset >= 0)
                {
                    locked = true;
                    Data.AddLabel(offset, null, true);
                    locked = false;
                    mw.InvalidateTable();
                }
            }
        }

        private void dataGridView1_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            currentlyEditing = e.RowIndex;

            // start by entering an address first, not the label
            if (dataGridView1.Rows[e.RowIndex].IsNewRow && e.ColumnIndex == 1)
            {
                dataGridView1.CurrentCell = dataGridView1.Rows[e.RowIndex].Cells[0];
            }
        }

        private void dataGridView1_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (dataGridView1.Rows[e.RowIndex].IsNewRow) return;
            int val = -1;
            string rowOldAddress = (string)dataGridView1.Rows[e.RowIndex].Cells[0].Value;
            int oldOffset = rowOldAddress == null ? -1 : Util.ConvertSNEStoPC(int.Parse(rowOldAddress, NumberStyles.HexNumber, null));
            string label = (string)dataGridView1.Rows[e.RowIndex].Cells[1].Value;

            toolStripStatusLabel1.Text = "";

            switch (e.ColumnIndex)
            {
                case 0:
                    {
                        if (!int.TryParse(e.FormattedValue.ToString(), NumberStyles.HexNumber, null, out val) || Util.ConvertSNEStoPC(val) == -1)
                        {
                            e.Cancel = true;
                            toolStripStatusLabel1.Text = "Must enter a valid hex address.";
                        } else if (oldOffset == -1 && Data.GetAllLabels().ContainsKey(Util.ConvertSNEStoPC(val)))
                        {
                            e.Cancel = true;
                            toolStripStatusLabel1.Text = "This address already has a label.";
                        } else if (dataGridView1.EditingControl != null)
                        {
                            dataGridView1.EditingControl.Text = Util.NumberToBaseString(val, Util.NumberBase.Hexadecimal, 6);
                        }
                        break;
                    }
                case 1:
                    {
                        val = int.Parse(rowOldAddress, NumberStyles.HexNumber, null);
                        label = e.FormattedValue.ToString();
                        // todo (validate for valid label characters)
                        break;
                    }
            }
            int offset = Util.ConvertSNEStoPC(val);

            locked = true;
            if (currentlyEditing >= 0)
            {
                if (oldOffset >= 0) Data.AddLabel(oldOffset, null, true);
                Data.AddLabel(offset, label, true);
            }
            locked = false;

            currentlyEditing = -1;
            mw.InvalidateTable();
        }

        public void AddRow(int offset, string alias)
        {
            if (!locked)
            {
                dataGridView1.Rows.Add(Util.NumberToBaseString(Util.ConvertPCtoSNES(offset), Util.NumberBase.Hexadecimal, 6), alias);
                dataGridView1.Invalidate();
            }
        }

        public void RemoveRow(int offset)
        {
            if (!locked)
            {
                for (int index = 0; index < dataGridView1.Rows.Count; index++)
                {
                    if ((string)dataGridView1.Rows[index].Cells[0].Value == Util.NumberToBaseString(Util.ConvertPCtoSNES(offset), Util.NumberBase.Hexadecimal, 6))
                    {
                        dataGridView1.Rows.RemoveAt(index);
                        dataGridView1.Invalidate();
                        break;
                    }
                }
            }
        }

        public void Reset()
        {
            dataGridView1.Rows.Clear();
            dataGridView1.Invalidate();
        }
    }
}

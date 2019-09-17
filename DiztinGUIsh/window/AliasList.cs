using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
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

        public bool locked = false;
        private MainWindow mw;
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

        private void import_Click(object sender, EventArgs e)
        {
            DialogResult result = openFileDialog1.ShowDialog();
            if (result == DialogResult.OK && openFileDialog1.FileName != "")
            {
                int errLine = 0;
                try
                {
                    Dictionary<int, string> newValues = new Dictionary<int, string>();
                    string[] lines = File.ReadAllLines(openFileDialog1.FileName);

                    for (int i = 0; i < lines.Length; i++) {
                        errLine = i + 1;
                        string addr = lines[i].Substring(0, lines[i].IndexOf(','));
                        string label = lines[i].Substring(lines[i].IndexOf(',') + 1);
                        newValues.Add(int.Parse(addr, NumberStyles.HexNumber, null), label); // todo (validate for valid label characters)
                    }

                    foreach (KeyValuePair<int,string> pair in newValues) Data.AddLabel(pair.Key, pair.Value, true);
                }
                catch (Exception)
                {
                    MessageBox.Show(
                        "An error occurred while parsing the file." +
                        (errLine > 0 ? string.Format(" (Check line {0}.)", errLine) : ""),
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void export_Click(object sender, EventArgs e)
        {
            DialogResult result = saveFileDialog1.ShowDialog();
            if (result == DialogResult.OK && saveFileDialog1.FileName != "")
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter(saveFileDialog1.FileName))
                    {
                        foreach (KeyValuePair<int, string> pair in Data.GetAllLabels())
                        {
                            sw.WriteLine(string.Format("{0},{1}", Util.NumberToBaseString(pair.Key, Util.NumberBase.Hexadecimal, 6), pair.Value));
                        }
                    }
                } catch (Exception)
                {
                    MessageBox.Show("An error occurred while saving the file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void dataGridView1_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            if (int.TryParse((string)dataGridView1.Rows[e.Row.Index].Cells[0].Value, NumberStyles.HexNumber, null, out int val))
            {
                locked = true;
                Data.AddLabel(val, null, true);
                locked = false;
                mw.InvalidateTable();
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
            int val = -1, oldAddress = -1;
            int.TryParse((string)dataGridView1.Rows[e.RowIndex].Cells[0].Value, NumberStyles.HexNumber, null, out oldAddress);
            string label = (string)dataGridView1.Rows[e.RowIndex].Cells[1].Value;

            toolStripStatusLabel1.Text = "";

            switch (e.ColumnIndex)
            {
                case 0:
                    {
                        if (!int.TryParse(e.FormattedValue.ToString(), NumberStyles.HexNumber, null, out val))
                        {
                            e.Cancel = true;
                            toolStripStatusLabel1.Text = "Must enter a valid hex address.";
                        } else if (oldAddress == -1 && Data.GetAllLabels().ContainsKey(val))
                        {
                            e.Cancel = true;
                            toolStripStatusLabel1.Text = "This address already has a label.";
                            var x = Data.GetAllLabels();
                            Console.WriteLine(Util.NumberToBaseString(val, Util.NumberBase.Hexadecimal));
                        } else if (dataGridView1.EditingControl != null)
                        {
                            dataGridView1.EditingControl.Text = Util.NumberToBaseString(val, Util.NumberBase.Hexadecimal, 6);
                        }
                        break;
                    }
                case 1:
                    {
                        val = oldAddress;
                        label = e.FormattedValue.ToString();
                        // todo (validate for valid label characters)
                        break;
                    }
            }

            locked = true;
            if (currentlyEditing >= 0)
            {
                if (val >= 0) Data.AddLabel(oldAddress, null, true);
                Data.AddLabel(val, label, true);
            }
            locked = false;

            currentlyEditing = -1;
            mw.InvalidateTable();
        }

        public void AddRow(int address, string alias)
        {
            if (!locked)
            {
                dataGridView1.Rows.Add(Util.NumberToBaseString(address, Util.NumberBase.Hexadecimal, 6), alias);
                dataGridView1.Invalidate();
            }
        }

        public void RemoveRow(int address)
        {
            if (!locked)
            {
                for (int index = 0; index < dataGridView1.Rows.Count; index++)
                {
                    if ((string)dataGridView1.Rows[index].Cells[0].Value == Util.NumberToBaseString(address, Util.NumberBase.Hexadecimal, 6))
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

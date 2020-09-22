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

        private static void SplitOnFirstComma(string instr, out string first_part, out string remainder)
        {
            if (!instr.Contains(","))
            {
                first_part = instr;
                remainder = "";
                return;
            }

            first_part = instr.Substring(0, instr.IndexOf(','));
            remainder = instr.Substring(instr.IndexOf(',') + 1);
        }

        private void import_Click(object sender, EventArgs e)
        {
            DialogResult result = openFileDialog1.ShowDialog();
            if (result != DialogResult.OK || openFileDialog1.FileName == "") 
                return;
            
            int errLine = 0;
            try
            {
                Dictionary<int, Data.AliasInfo> newValues = new Dictionary<int, Data.AliasInfo>();
                string[] lines = File.ReadAllLines(openFileDialog1.FileName);

                // NOTE: this is kind of a risky way to parse CSV files, won't deal with weirdness in the comments
                // section.
                for (int i = 0; i < lines.Length; i++) {
                    var aliasInfo = new Data.AliasInfo();

                    errLine = i + 1;

                    AliasList.SplitOnFirstComma(lines[i], out var labelAddress, out var remainder);
                    AliasList.SplitOnFirstComma(remainder, out aliasInfo.name, out aliasInfo.comment);

                    // todo (validate for valid label characters)
                    newValues.Add(int.Parse(labelAddress, NumberStyles.HexNumber, null), aliasInfo);
                }

                foreach (KeyValuePair<int, Data.AliasInfo> pair in newValues)
                {
                    pair.Value.CleanUp();
                    Data.AddLabel(pair.Key, pair.Value, true);
                }
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "An error occurred while parsing the file." +
                    (errLine > 0 ? string.Format(" (Check line {0}.)", errLine) : ""),
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                        foreach (var pair in Data.GetAllLabels())
                        {
                            sw.WriteLine(
                                $"{Util.NumberToBaseString(pair.Key, Util.NumberBase.Hexadecimal, 6)},{pair.Value.name},{pair.Value.comment}");
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

            var labelAliasInfo = new Data.AliasInfo
            {
                name = (string) dataGridView1.Rows[e.RowIndex].Cells[1].Value,
                comment = (string)dataGridView1.Rows[e.RowIndex].Cells[2].Value,
            };

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
                        labelAliasInfo.name = e.FormattedValue.ToString();
                        // todo (validate for valid label characters)
                        break;
                    }
                case 2:
                    {
                        val = oldAddress;
                        labelAliasInfo.comment = e.FormattedValue.ToString();
                        // todo (validate for valid comment characters, if any)
                        break;
                    }
            }

            locked = true;
            if (currentlyEditing >= 0)
            {
                if (val >= 0) Data.AddLabel(oldAddress, null, true);
                Data.AddLabel(val, labelAliasInfo, true);
            }
            locked = false;

            currentlyEditing = -1;
            mw.InvalidateTable();
        }

        public void AddRow(int address, Data.AliasInfo alias)
        {
            if (!locked)
            {
                dataGridView1.Rows.Add(Util.NumberToBaseString(address, Util.NumberBase.Hexadecimal, 6), alias.name, alias.comment);
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

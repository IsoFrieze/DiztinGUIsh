using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

        private void ImportLabelsFromCSV(bool replaceAll)
        {
            DialogResult result = openFileDialog1.ShowDialog();
            if (result != DialogResult.OK || openFileDialog1.FileName == "")
                return;

            int errLine = 0;
            try
            {
                Dictionary<int, Data.AliasInfo> newValues = new Dictionary<int, Data.AliasInfo>();
                string[] lines = Util.ReadLines(openFileDialog1.FileName).ToArray();

                Regex valid_label_chars = new Regex(@"^([a-zA-Z0-9_\-]*)$");

                // NOTE: this is kind of a risky way to parse CSV files, won't deal with weirdness in the comments
                // section.
                for (int i = 0; i < lines.Length; i++)
                {
                    var aliasInfo = new Data.AliasInfo();

                    errLine = i + 1;

                    AliasList.SplitOnFirstComma(lines[i], out var labelAddress, out var remainder);
                    AliasList.SplitOnFirstComma(remainder, out aliasInfo.name, out aliasInfo.comment);

                    aliasInfo.CleanUp();

                    aliasInfo.name = aliasInfo.name.Trim();
                    if (!valid_label_chars.Match(aliasInfo.name).Success)
                        throw new InvalidDataException("invalid label name: " + aliasInfo.name);

                    newValues.Add(int.Parse(labelAddress, NumberStyles.HexNumber, null), aliasInfo);
                }

                // everything read OK, modify the existing list now. point of no return
                if (replaceAll)
                    Data.Inst.DeleteAllLabels();

                ResetDataGrid();

                // this will call AddRow() to add items back to the UI datagrid.
                foreach (KeyValuePair<int, Data.AliasInfo> pair in newValues)
                {
                    Data.Inst.AddLabel(pair.Key, pair.Value, true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "An error occurred while parsing the file.\n" + ex.Message +
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
                        foreach (var pair in Data.Inst.GetAllLabels())
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
                Data.Inst.AddLabel(val, null, true);
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
                        } else if (oldAddress == -1 && Data.Inst.GetAllLabels().ContainsKey(val))
                        {
                            e.Cancel = true;
                            toolStripStatusLabel1.Text = "This address already has a label.";
                            var x = Data.Inst.GetAllLabels();
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
                if (val >= 0) Data.Inst.AddLabel(oldAddress, null, true);
                Data.Inst.AddLabel(val, labelAliasInfo, true);
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

        public void ResetDataGrid()
        {
            dataGridView1.Rows.Clear();
            dataGridView1.Invalidate();
        }

        private void importAppend_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Info: Items in CSV will:\n" +
                            "1) CSV items will be added if their address doesn't already exist in this list\n" +
                            "2) CSV items will replace anything with the same address as items in the list\n" +
                            "3) any unmatched addresses in the list will be left alone\n" +
                            "\n" +
                            "Continue?\n", "Warning", MessageBoxButtons.OKCancel) != DialogResult.OK)
                return;

            ImportLabelsFromCSV(false);
        }

        private void btnImportReplace_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Info: All list items will be deleted and replaced with the CSV file.\n" +
                                "\n" +
                                "Continue?\n", "Warning", MessageBoxButtons.OKCancel) != DialogResult.OK)
                return;

            ImportLabelsFromCSV(true);
        }
    }
}

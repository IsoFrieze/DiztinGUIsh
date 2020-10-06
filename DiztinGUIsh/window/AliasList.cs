using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using DiztinGUIsh.core;

namespace DiztinGUIsh.window
{
    public partial class AliasList : Form
    {
        private readonly MainWindow parentWindow;
        private ProjectController ProjectController => parentWindow?.ProjectController;
        private Data Data => ProjectController?.Project?.Data;

        public bool locked;
        private int currentlyEditing = -1;
        
        public AliasList(MainWindow main)
        {
            parentWindow = main;
            InitializeComponent();
        }

        private void AliasList_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.UserClosing) return;
            e.Cancel = true;
            this.Hide();
        }

        private void AliasList_Resize(object sender, EventArgs e)
        {
            var h = Height - 68 - 22;
            dataGridView1.Height = h;
        }

        private void jump_Click(object sender, EventArgs e)
        {
            if (!int.TryParse((string) dataGridView1.SelectedRows[0].Cells[0].Value, NumberStyles.HexNumber, null,
                out var val)) return;

            var offset = Data.ConvertSNEStoPC(val);
            if (offset >= 0)
            {
                ProjectController.SelectOffset(offset);
            }
        }

        private static void SplitOnFirstComma(string instr, out string firstPart, out string remainder)
        {
            if (!instr.Contains(","))
            {
                firstPart = instr;
                remainder = "";
                return;
            }

            firstPart = instr.Substring(0, instr.IndexOf(','));
            remainder = instr.Substring(instr.IndexOf(',') + 1);
        }

        private void ImportLabelsFromCsv(bool replaceAll)
        {
            var result = openFileDialog1.ShowDialog();
            if (result != DialogResult.OK || openFileDialog1.FileName == "")
                return;

            var errLine = 0;
            try
            {
                var newValues = new Dictionary<int, Label>();
                var lines = Util.ReadLines(openFileDialog1.FileName).ToArray();

                var validLabelChars = new Regex(@"^([a-zA-Z0-9_\-]*)$");

                // NOTE: this is kind of a risky way to parse CSV files, won't deal with weirdness in the comments
                // section.
                for (var i = 0; i < lines.Length; i++)
                {
                    var label = new Label();

                    errLine = i + 1;

                    SplitOnFirstComma(lines[i], out var labelAddress, out var remainder);
                    SplitOnFirstComma(remainder, out label.name, out label.comment);

                    label.CleanUp();

                    label.name = label.name.Trim();
                    if (!validLabelChars.Match(label.name).Success)
                        throw new InvalidDataException("invalid label name: " + label.name);

                    newValues.Add(int.Parse(labelAddress, NumberStyles.HexNumber, null), label);
                }

                // everything read OK, modify the existing list now. point of no return
                if (replaceAll)
                    Data.DeleteAllLabels();

                ClearAndInvalidateDataGrid();

                // this will call AddRow() to add items back to the UI datagrid.
                foreach (var pair in newValues)
                {
                    Data.AddLabel(pair.Key, pair.Value, true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "An error occurred while parsing the file.\n" + ex.Message +
                    (errLine > 0 ? $" (Check line {errLine}.)" : ""),
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void export_Click(object sender, EventArgs e)
        {
            var result = saveFileDialog1.ShowDialog();
            if (result != DialogResult.OK || saveFileDialog1.FileName == "") return;
            
            try
            {
                using var sw = new StreamWriter(saveFileDialog1.FileName);
                foreach (KeyValuePair<int, Label> pair in Data.Labels.Dict)
                {
                    sw.WriteLine(
                        $"{Util.NumberToBaseString(pair.Key, Util.NumberBase.Hexadecimal, 6)},{pair.Value.name},{pair.Value.comment}");
                }
            } catch (Exception)
            {
                MessageBox.Show("An error occurred while saving the file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void dataGridView1_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            if (!int.TryParse((string) dataGridView1.Rows[e.Row.Index].Cells[0].Value, NumberStyles.HexNumber, null,
                out var val)) return;
            locked = true;
            Data.AddLabel(val, null, true);
            locked = false;
            parentWindow.InvalidateTable(); // TODO: move to mainwindow, use notifychanged in mainwindow for this
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
            var val = -1;
            int.TryParse((string)dataGridView1.Rows[e.RowIndex].Cells[0].Value, NumberStyles.HexNumber, null, out var oldAddress);

            var labelLabel = new Label
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
                        } else if (oldAddress == -1 && Data.Labels.Dict.ContainsKey(val))
                        {
                            e.Cancel = true;
                            toolStripStatusLabel1.Text = "This address already has a label.";
                            var x = Data.Labels;
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
                        labelLabel.name = e.FormattedValue.ToString();
                        // todo (validate for valid label characters)
                        break;
                    }
                case 2:
                    {
                        val = oldAddress;
                        labelLabel.comment = e.FormattedValue.ToString();
                        // todo (validate for valid comment characters, if any)
                        break;
                    }
            }

            locked = true;
            if (currentlyEditing >= 0)
            {
                if (val >= 0) Data.AddLabel(oldAddress, null, true);
                Data.AddLabel(val, labelLabel, true);
            }
            locked = false;

            currentlyEditing = -1;
            parentWindow.InvalidateTable();  // TODO: move to mainwindow, use notifychanged in mainwindow for this
        }

        public void AddRow(int address, Label alias)
        {
            if (locked) 
                return;
            RawAdd(address, alias);
            dataGridView1.Invalidate();
        }

        private void RawAdd(int address, Label alias)
        {
            dataGridView1.Rows.Add(Util.NumberToBaseString(address, Util.NumberBase.Hexadecimal, 6), alias.name, alias.comment);
        }

        public void RemoveRow(int address)
        {
            if (locked) 
                return;

            for (var index = 0; index < dataGridView1.Rows.Count; index++)
            {
                if ((string) dataGridView1.Rows[index].Cells[0].Value !=
                    Util.NumberToBaseString(address, Util.NumberBase.Hexadecimal, 6)) continue;

                dataGridView1.Rows.RemoveAt(index);
                dataGridView1.Invalidate();
                break;
            }
        }

        public void ClearAndInvalidateDataGrid()
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

            ImportLabelsFromCsv(false);
        }

        private void btnImportReplace_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Info: All list items will be deleted and replaced with the CSV file.\n" +
                                "\n" +
                                "Continue?\n", "Warning", MessageBoxButtons.OKCancel) != DialogResult.OK)
                return;

            ImportLabelsFromCsv(true);
        }

        public void RebindProject()
        {
            RepopulateFromData();

            // todo: eventually use databinding/datasource, probably.
            // Todo: modify observabledictionary wrapper to avoid having to do the .Dict call here.
            Data.Labels.Dict.PropertyChanged += Labels_PropertyChanged;
            Data.Labels.Dict.CollectionChanged += Labels_CollectionChanged;
        }

        private void RepopulateFromData()
        {
            ClearAndInvalidateDataGrid();

            if (Data == null)
                return;

            // TODO: replace with winforms databinding eventually
            foreach (KeyValuePair<int, Label> item in Data.Labels.Dict)
            {
                RawAdd(item.Key, item.Value);
            }
            dataGridView1.Invalidate();
        }

        private void Labels_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (KeyValuePair<int, Label> item in e.NewItems)
                {
                    AddRow(item.Key, item.Value);
                }
            }

            if (e.OldItems != null)
            {
                foreach (KeyValuePair<int, Label> item in e.OldItems)
                {
                    RemoveRow(item.Key);
                }
            }
        }

        private void Labels_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // if needed, catch any changes to label content here
        }
    }
}

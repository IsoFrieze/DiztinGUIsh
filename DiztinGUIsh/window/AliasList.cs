using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;
using DiztinGUIsh.controller;
using DiztinGUIsh.window2;
using Label = Diz.Core.model.Label;

namespace DiztinGUIsh.window
{
    public partial class AliasList : Form
    {
        private readonly DataGridEditorForm parentWindow;
        private IMainFormController MainFormController => parentWindow?.MainFormController;
        private Data Data => MainFormController?.Project?.Data;

        private bool Locked;
        private int currentlyEditing = -1;
        
        public AliasList(DataGridEditorForm main)
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

            var offset = Data.ConvertSnesToPc(val);
            if (offset >= 0)
            {
                MainFormController.SelectedSnesOffset = offset;
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
                    SplitOnFirstComma(remainder, out var labelName, out var labelComment);
                    
                    label.Name = labelName.Trim();
                    label.Comment = labelComment;

                    if (!validLabelChars.Match(label.Name).Success)
                        throw new InvalidDataException("invalid label name: " + label.Name);

                    newValues.Add(int.Parse(labelAddress, NumberStyles.HexNumber, null), label);
                }

                // everything read OK, modify the existing list now. point of no return
                if (replaceAll)
                    Data.DeleteAllLabels();

                ClearAndInvalidateDataGrid();

                // this will call AddRow() to add items back to the UI datagrid.
                foreach (var (key, value) in newValues)
                {
                    Data.AddLabel(key, value, true);
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
                WriteLabelsToCsv(sw);
            } catch (Exception)
            {
                MessageBox.Show("An error occurred while saving the file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WriteLabelsToCsv(TextWriter sw)
        {
            foreach (var pair in Data.Labels)
            {
                int snesOffset = pair.Key;
                Label label = pair.Value;

                OutputCsvLine(sw, snesOffset, label);
            }
        }

        private static void OutputCsvLine(TextWriter sw, int labelSnesAddress, Label label)
        {
            var outputLine = $"{Util.ToHexString6(labelSnesAddress)},{label.Name},{label.Comment}";
            sw.WriteLine(outputLine);
        }

        private void dataGridView1_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            if (!int.TryParse((string) dataGridView1.Rows[e.Row.Index].Cells[0].Value, NumberStyles.HexNumber, null,
                out var val)) return;
            Locked = true;
            Data.RemoveLabel(val);
            Locked = false;
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
            if (dataGridView1.Rows[e.RowIndex].IsNewRow)
                return;
            
            var existingAddressStr = (string)dataGridView1.Rows[e.RowIndex].Cells[0].Value;
            var existingName = (string)dataGridView1.Rows[e.RowIndex].Cells[1].Value;
            var existingComment = (string)dataGridView1.Rows[e.RowIndex].Cells[2].Value;
            int.TryParse(existingAddressStr, NumberStyles.HexNumber, null, out var existingAddress);
            
            var newLabel = new Label
            {
                Name = existingName,
                Comment = existingComment
            };

            toolStripStatusLabel1.Text = "";
            var newAddress = -1;

            switch (e.ColumnIndex)
            {
                case 0: // label's address
                    {
                        if (!int.TryParse(e.FormattedValue.ToString(), NumberStyles.HexNumber, null, out newAddress))
                        {
                            e.Cancel = true;
                            toolStripStatusLabel1.Text = "Must enter a valid hex address.";
                            break;
                        }
                        
                        if (existingAddress == -1 && Data.GetLabel(newAddress) != null)
                        {
                            e.Cancel = true;
                            toolStripStatusLabel1.Text = "This address already has a label.";
                            break;
                        }

                        if (dataGridView1.EditingControl != null)
                        {
                            dataGridView1.EditingControl.Text = Util.ToHexString6(newAddress);
                        }
                        break;
                    }
                case 1: // label name
                    {
                        newAddress = existingAddress;
                        newLabel.Name = e.FormattedValue.ToString();
                        // todo (validate for valid label characters)
                        break;
                    }
                case 2: // label comment
                    {
                        newAddress = existingAddress;
                        newLabel.Comment = e.FormattedValue.ToString();
                        // todo (validate for valid comment characters, if any)
                        break;
                    }
            }

            Locked = true;
            if (currentlyEditing >= 0)
            {
                if (newAddress >= 0) 
                    Data.RemoveLabel(existingAddress);
                
                Data.AddLabel(newAddress, newLabel, true);
            }
            Locked = false;

            currentlyEditing = -1;
        }

        public void AddRow(int address, Label alias)
        {
            if (Locked) 
                return;
            RawAdd(address, alias);
            dataGridView1.Invalidate();
        }

        private void RawAdd(int address, Label alias)
        {
            dataGridView1.Rows.Add(Util.ToHexString6(address), alias.Name, alias.Comment);
        }

        public void RemoveRow(int address)
        {
            if (Locked) 
                return;

            for (var index = 0; index < dataGridView1.Rows.Count; index++)
            {
                if ((string) dataGridView1.Rows[index].Cells[0].Value !=
                    Util.ToHexString6(address)) continue;

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
            // tmp disabled // Data.Labels.PropertyChanged += Labels_PropertyChanged;
            // tmp disabled // Data.Labels.CollectionChanged += Labels_CollectionChanged;
        }

        private void RepopulateFromData()
        {
            ClearAndInvalidateDataGrid();

            if (Data == null)
                return;

            // TODO: replace with winforms databinding eventually
            foreach (var item in Data.Labels)
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

using System;
using System.ComponentModel;
using System.Windows.Forms;
using Diz.Core;
using Diz.Core.model;
using DiztinGUIsh.controller;
using DynamicData;
using Label = Diz.Core.model.Label;

namespace DiztinGUIsh.window
{
    public partial class AliasList : Form, IViewLabels
    {
        private readonly MainWindow parentWindow;
        private ProjectController ProjectController => parentWindow?.ProjectController;
        private Data Data => ProjectController?.Project?.Data;
        
        public Project Project { get; set; } // TODO call RebindProject() on set

        //public bool Locked;
        //private int currentlyEditing = -1;
        
        public AliasList(MainWindow main)
        {
            parentWindow = main;
            InitializeComponent();
            
            RebindProject();
        }

        private void AliasList_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.UserClosing) return;
            e.Cancel = true;
            Hide();
        }

        private void AliasList_Resize(object sender, EventArgs e)
        {
            // var h = Height - 68 - 22;
            // labelGrid.Height = h;
        }

        // private static void SplitOnFirstComma(string instr, out string firstPart, out string remainder)
        // {
        //     if (!instr.Contains(","))
        //     {
        //         firstPart = instr;
        //         remainder = "";
        //         return;
        //     }
        //
        //     firstPart = instr.Substring(0, instr.IndexOf(','));
        //     remainder = instr.Substring(instr.IndexOf(',') + 1);
        // }

        private void ImportLabelsFromCsv(bool replaceAll)
        {
            // var result = openFileDialog1.ShowDialog();
            // if (result != DialogResult.OK || openFileDialog1.FileName == "")
            //     return;
            //
            // var errLine = 0;
            // try
            // {
            //     var newValues = new Dictionary<int, Label>();
            //     var lines = Util.ReadLines(openFileDialog1.FileName).ToArray();
            //
            //     var validLabelChars = new Regex(@"^([a-zA-Z0-9_\-]*)$");
            //
            //     // NOTE: this is kind of a risky way to parse CSV files, won't deal with weirdness in the comments
            //     // section.
            //     for (var i = 0; i < lines.Length; i++)
            //     {
            //         var label = new Label();
            //
            //         errLine = i + 1;
            //
            //         SplitOnFirstComma(lines[i], out var labelAddress, out var remainder);
            //         SplitOnFirstComma(remainder, out label.Name, out label.Comment);
            //
            //         label.CleanUp();
            //
            //         label.Name = label.Name.Trim();
            //         if (!validLabelChars.Match(label.Name).Success)
            //             throw new InvalidDataException("invalid label name: " + label.Name);
            //
            //         newValues.Add(int.Parse(labelAddress, NumberStyles.HexNumber, null), label);
            //     }
            //
            //     // everything read OK, modify the existing list now. point of no return
            //     if (replaceAll)
            //         Data.DeleteAllLabels();
            //
            //     ClearAndInvalidateDataGrid();
            //
            //     // this will call AddRow() to add items back to the UI datagrid.
            //     foreach (var pair in newValues)
            //     {
            //         Data.AddLabel(pair.Key, pair.Value, true);
            //     }
            // }
            // catch (Exception ex)
            // {
            //     MessageBox.Show(
            //         "An error occurred while parsing the file.\n" + ex.Message +
            //         (errLine > 0 ? $" (Check line {errLine}.)" : ""),
            //         "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            // }
        }

        private void dataGridView1_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            // if (!int.TryParse((string) labelGrid.Rows[e.Row.Index].Cells[0].Value, NumberStyles.HexNumber, null,
            //     out var val)) return;
            // Locked = true;
            // Data.AddLabel(val, null, true);
            // Locked = false;
            // parentWindow.InvalidateTable(); // TODO: move to mainwindow, use notifychanged in mainwindow for this
        }

        private void dataGridView1_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            // currentlyEditing = e.RowIndex;
            //
            // // start by entering an address first, not the label
            // if (labelGrid.Rows[e.RowIndex].IsNewRow && e.ColumnIndex == 1)
            // {
            //     labelGrid.CurrentCell = labelGrid.Rows[e.RowIndex].Cells[0];
            // }
        }

        private void dataGridView1_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            // if (labelGrid.Rows[e.RowIndex].IsNewRow) return;
            // var val = -1;
            // int.TryParse((string)labelGrid.Rows[e.RowIndex].Cells[0].Value, NumberStyles.HexNumber, null, out var oldAddress);
            //
            // var labelLabel = new Label
            // {
            //     Name = (string) labelGrid.Rows[e.RowIndex].Cells[1].Value,
            //     Comment = (string)labelGrid.Rows[e.RowIndex].Cells[2].Value,
            // };
            //
            // toolStripStatusLabel1.Text = "";
            //
            // switch (e.ColumnIndex)
            // {
            //     case 0:
            //         {
            //             if (!int.TryParse(e.FormattedValue.ToString(), NumberStyles.HexNumber, null, out val))
            //             {
            //                 e.Cancel = true;
            //                 toolStripStatusLabel1.Text = "Must enter a valid hex address.";
            //             } else if (oldAddress == -1 && Data.Labels.ContainsKey(val))
            //             {
            //                 e.Cancel = true;
            //                 toolStripStatusLabel1.Text = "This address already has a label.";
            //
            //                 Console.WriteLine(Util.NumberToBaseString(val, Util.NumberBase.Hexadecimal));
            //             } else if (labelGrid.EditingControl != null)
            //             {
            //                 labelGrid.EditingControl.Text = Util.NumberToBaseString(val, Util.NumberBase.Hexadecimal, 6);
            //             }
            //             break;
            //         }
            //     case 1:
            //         {
            //             val = oldAddress;
            //             labelLabel.Name = e.FormattedValue.ToString();
            //             // todo (validate for valid label characters)
            //             break;
            //         }
            //     case 2:
            //         {
            //             val = oldAddress;
            //             labelLabel.Comment = e.FormattedValue.ToString();
            //             // todo (validate for valid comment characters, if any)
            //             break;
            //         }
            // }
            //
            // Locked = true;
            // if (currentlyEditing >= 0)
            // {
            //     if (val >= 0) Data.AddLabel(oldAddress, null, true);
            //     Data.AddLabel(val, labelLabel, true);
            // }
            // Locked = false;
            //
            // currentlyEditing = -1;
            // parentWindow.InvalidateTable();  // TODO: move to mainwindow, use notifychanged in mainwindow for this
        }

        // public void AddRow(int address, Label alias)
        // {
        //     if (Locked) 
        //         return;
        //     RawAdd(address, alias);
        //     labelGrid.Invalidate();
        // }

        // private void RawAdd(int address, Label alias)
        // {
        //     labelGrid.Rows.Add(Util.NumberToBaseString(address, Util.NumberBase.Hexadecimal, 6), alias.Name, alias.Comment);
        // }

        // public void RemoveRow(int address)
        // {
        //     if (Locked) 
        //         return;
        //
        //     for (var index = 0; index < labelGrid.Rows.Count; index++)
        //     {
        //         if ((string) labelGrid.Rows[index].Cells[0].Value !=
        //             Util.NumberToBaseString(address, Util.NumberBase.Hexadecimal, 6)) continue;
        //
        //         labelGrid.Rows.RemoveAt(index);
        //         labelGrid.Invalidate();
        //         break;
        //     }
        // }

        // public void ClearAndInvalidateDataGrid()
        // {
        //     labelGrid.Rows.Clear();
        //     labelGrid.Invalidate();
        // }
        
        // // keep here
        // BindingList<Label> dataBindingList;
        //
        // // put elsewhere
        // SourceCache<Label, int> sourceCache;
        
        public void RebindProject()
        {
//             sourceCache = new SourceCache<Label, int>(label=>label.Offset);
//
//             // var sourceLabels = 
//             //     sourceCache
//             //     // .Filter(t => t.Status == "Something")
//             //     .to();
//             
//             dataBindingList = new BindingList<Label>();
//
//             var observable = sourceCache.Connect();
//             
//             var disposable = observable
//                 // .Filter(Filter)
//                 .Bind(dataBindingList)
//                 .DisposeMany()
//                 .Subscribe();
//
//             labelGrid.Columns.Clear();
//             labelGrid.Rows.Clear();
//             labelGrid.AutoGenerateColumns = true;
//
//             var bs = new BindingSource(dataBindingList, null);
//
//             labelGrid.DataSource = bs;
//
//             sourceCache.AddOrUpdate(new Label
//             {
//                 Comment = "test2",
//                 Name = "name2",
//             });
//             
//             sourceCache.AddOrUpdate(new Label
//             {
//                 Comment = "test1",
//                 Name = "name1"
//             });
//
//             /*RepopulateFromData();
//
//             // todo: eventually use databinding/datasource, probably.
//             // Todo: modify observabledictionary wrapper to avoid having to do the .Dict call here.
//             Data.Labels.PropertyChanged += Labels_PropertyChanged;
//             Data.Labels.CollectionChanged += Labels_CollectionChanged;*/
        }

        private bool Filter(Label label)
        {
            var filtertext = txtAddress.Text;
            if (filtertext == "")
                return true;

            return label.Name.Contains(filtertext);
        }

        // private void RepopulateFromData()
        // {
        //     ClearAndInvalidateDataGrid();
        //
        //     if (Data == null)
        //         return;
        //
        //     // TODO: replace with winforms databinding eventually
        //     foreach (var item in Data.Labels)
        //     {
        //         RawAdd(item.Key, item.Value);
        //     }
        //     labelGrid.Invalidate();
        // }

        /*
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
        }*/

        private void jumpToLabelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // if (!int.TryParse((string) labelGrid.SelectedRows[0].Cells[0].Value, NumberStyles.HexNumber, null,
            //     out var val)) return;
            //
            // var offset = Data.ConvertSnesToPc(val);
            // if (offset >= 0)
            // {
            //     ProjectController.SelectOffset(
            //         offset, 
            //         new ISnesNavigation.HistoryArgs {Description = "Jump To Label"}
            //     );
            // }
        }

        private void importAppendToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // if (MessageBox.Show("Info: Items in CSV will:\n" +
            //                     "1) CSV items will be added if their address doesn't already exist in this list\n" +
            //                     "2) CSV items will replace anything with the same address as items in the list\n" +
            //                     "3) any unmatched addresses in the list will be left alone\n" +
            //                     "\n" +
            //                     "Continue?\n", "Warning", MessageBoxButtons.OKCancel) != DialogResult.OK)
            //     return;
            //
            // ImportLabelsFromCsv(false);
        }

        private void importReplaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // if (MessageBox.Show("Info: All list items will be deleted and replaced with the CSV file.\n" +
            //                     "\n" +
            //                     "Continue?\n", "Warning", MessageBoxButtons.OKCancel) != DialogResult.OK)
            //     return;
            //
            // ImportLabelsFromCsv(true);
        }

        private void exportCSVToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // var result = saveFileDialog1.ShowDialog();
            // if (result != DialogResult.OK || saveFileDialog1.FileName == "") return;
            //
            // try
            // {
            //     using var sw = new StreamWriter(saveFileDialog1.FileName);
            //     foreach (var pair in Data.Labels)
            //     {
            //         sw.WriteLine(
            //             $"{Util.NumberToBaseString(pair.Key, Util.NumberBase.Hexadecimal, 6)},{pair.Value.Name},{pair.Value.Comment}");
            //     }
            // } catch (Exception)
            // {
            //     MessageBox.Show("An error occurred while saving the file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            // }
        }

        private void exportBSNESSymbolsToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
    }
}

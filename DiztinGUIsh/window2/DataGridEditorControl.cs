using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;
using DiztinGUIsh.util;
using Equin.ApplicationFramework;
using UserControl = System.Windows.Forms.UserControl;

// eventually, see if we can get this class to not directly contain references to "RomByteDataGridRow"
// so that it can be generically used to format whatever data we want to throw at it

namespace DiztinGUIsh.window2
{
    public partial class DataGridEditorControl : UserControl, IBytesGridViewer<RomByteDataGridRow>
    {
        #region Init
        
        public Data Data => DataController?.Data;
        public Util.NumberBase NumberBaseToShow { get; set; } = Util.NumberBase.Hexadecimal;

        
        private IDataController dataController;

        public IDataController DataController
        {
            get => dataController;
            set
            {
                dataController = value;
                DataBind();
            }
        }

        private void DataBind()
        {
            SuspendLayout();

            var dataGridView1BindingSource = new BindingSource
            {
                DataSource = DataSource
            };
            dataGridView1.DataSource = dataGridView1BindingSource;
            
            OnDataBindingChanged();
            ResumeLayout();
        }
        
        private BindingListView<RomByteDataGridRow> dataSource;

        public BindingListView<RomByteDataGridRow> DataSource
        {
            get => dataSource;
            set
            {
                dataSource = value;
                DataBind();
            }
        }

        public DataGridEditorControl()
        {
            InitializeComponent();
            ExtraDesignInit();
            GuiUtil.EnableDoubleBuffering(typeof(DataGridView), Table);
        }

        private void ExtraDesignInit()
        {
            // stuff that should probably be in the designer, but we're migrating some old code

            Table.AutoGenerateColumns = true;
            Table.AllowUserToAddRows = false;
            Table.AllowUserToDeleteRows = false;
            Table.AllowUserToResizeRows = false;

            Table.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
            Table.EditMode = DataGridViewEditMode.EditOnEnter;

            Table.BorderStyle = BorderStyle.None;
            Table.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            Table.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            Table.Margin = new Padding(0);
            Table.MultiSelect = false;
            Table.RowHeadersVisible = false;
            Table.RowTemplate.Height = 15;
            Table.ShowCellToolTips = false;
            Table.ShowEditingIcon = false;
            Table.TabStop = false;

            // questionable stuff, evaluate if we need it anymore
            // Table.Location = new System.Drawing.Point(0, 24);
            // Table.Size = new System.Drawing.Size(913, 492);
            // Table.TabIndex = 1;
            // Table.ScrollBars = ScrollBars.None; // we will likely keep this ON now.
            Table.ShowCellErrors = false; // we want this later, I think.
            Table.ShowRowErrors = false; // we want this later, I think.

            var defaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                BackColor = SystemColors.Window,
                Font = RomByteDataGridRowFormatting.FontData,
                ForeColor = SystemColors.ControlText,
                SelectionBackColor = Color.CornflowerBlue,
                SelectionForeColor = SystemColors.HighlightText,
                WrapMode = DataGridViewTriState.False
            };
            Table.DefaultCellStyle = defaultCellStyle;

            // this is fine but doesn't do anything if we don't override the two events below?
            Table.VirtualMode = true;
            // TODO Table.CellValueNeeded += table_CellValueNeeded; // may not need anymore?
            // TODO Table.CellValuePushed += table_CellValuePushed; // may not need anymore?

            //this.Table.KeyDown += new System.Windows.Forms.KeyEventHandler(this.table_KeyDown);
            //this.Table.MouseDown += new System.Windows.Forms.MouseEventHandler(this.table_MouseDown);
            //this.Table.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.table_MouseWheel);

            Table.CellPainting += table_CellPainting;
            
            Table.CurrentCellChanged += TableOnCurrentCellChanged;
        }

        // remove this eventually, shortcut for now.
        private DataGridView Table => dataGridView1;

        public Util.NumberBase DisplayBase { get; set; } = Util.NumberBase.Hexadecimal;

        public void InvalidateTable() => Table.Invalidate();

        private int SelectedColumnIndex => Table.CurrentCell.ColumnIndex;

        private void SelectColumnClamped(int offset)
        {
            // SelectColumn(Util.ClampIndex(SelectedColumnIndex + offset, Table.ColumnCount));
        }

        #endregion

        #region KeyboardHandler

        private bool IsDataValid() => Data != null && Data.GetRomSize() > 0;

        /*public void table_KeyDown(object sender, KeyEventArgs e)
        {
            if (IsDataValid())
                return;

            var offset = GetSelectedOffset();

            switch (e.KeyCode)
            {
                // nav
                case Keys.Home:
                case Keys.End:
                case Keys.PageUp:
                case Keys.PageDown:
                case Keys.Up:
                case Keys.Down:
                    AdjustSelectedOffsetByKeyCode(e.KeyCode, offset);
                    break;
                case Keys.Left:
                case Keys.Right:
                    AdjustSelectedColumnByKeyCode(e.KeyCode);
                    break;

                // keyboard shortcuts to edit certain fields
                case Keys.L:
                    Table.CurrentCell = Table.Rows[Table.CurrentCell.RowIndex].Cells[0];
                    Table.BeginEdit(true);
                    break;
                case Keys.B:
                    Table.CurrentCell = Table.Rows[Table.CurrentCell.RowIndex].Cells[8];
                    Table.BeginEdit(true);
                    break;
                case Keys.D:
                    Table.CurrentCell = Table.Rows[Table.CurrentCell.RowIndex].Cells[9];
                    Table.BeginEdit(true);
                    break;
                case Keys.C:
                    Table.CurrentCell = Table.Rows[Table.CurrentCell.RowIndex].Cells[12];
                    Table.BeginEdit(true);
                    break;

                default:
                    Controller.KeyDown(sender, e);
                    break;
            }

            e.Handled = true;
            InvalidateTable();
        }*/
        
        private static int GetOffsetDeltaFromKeycode(Keys keyCode)
        {
            const int ONE = 1;
            const int SMALL = 16;
            const int LARGE = 256;
            
            var sign = keyCode is not Keys.Home and not Keys.PageUp and not Keys.Up ? 1 : -1;
            var magnitude = 0;
            switch (keyCode)
            {
                case Keys.Up: case Keys.Down: magnitude = ONE; break;
                case Keys.PageUp: case Keys.PageDown: magnitude = SMALL; break;
                case Keys.Home: case Keys.End: magnitude = LARGE; break;
            };

            return sign * magnitude;
        }

        #endregion

        #region Painting

        private RomByteDataGridRow GetRomByteAtRow(int row) =>
            (Table.Rows[row].DataBoundItem as ObjectView<RomByteDataGridRow>)?.Object;

        public RomByteDataGridRow SelectedRomByteRow =>
            Table.CurrentRow == null
                ? null
                : GetRomByteAtRow(Table.CurrentRow.Index);

        public int ViewOffset => SelectedRomByteRow?.RomByte?.Offset ?? -1;

        private int SelectedRow => Table.CurrentCell.RowIndex;
        private int SelectedCol => Table.CurrentCell.ColumnIndex;

        // Corresponds to the name of properties in RomByteData,
        // NOT what you see on the screen as the column heading text
        private string GetColumnHeaderDataProperty(DataGridViewCellPaintingEventArgs e) =>
            GetColumnHeaderDataProperty(e?.ColumnIndex ?? -1);

        private string GetColumnHeaderDataProperty(int colIndex) =>
            GetColumn(colIndex)?.DataPropertyName;

        private DataGridViewColumn GetColumn(int colIndex) =>
            colIndex >= 0 && colIndex < Table.Columns.Count ? Table.Columns[colIndex] : null;

        private void table_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            var valid = IsDataValid() && e.RowIndex != -1 && e.ColumnIndex != -1;
            if (!valid)
                return;

            var romByteAtRow = GetRomByteAtRow(e.RowIndex);
            var colHeaderDataProperty = GetColumnHeaderDataProperty(e);

            if (romByteAtRow?.RomByte == null || string.IsNullOrEmpty(colHeaderDataProperty))
                return;

            romByteAtRow.SetStyleForCell(colHeaderDataProperty, e.CellStyle);
        }

        #endregion

        private void OnDataBindingChanged() => ApplyColumnFormatting();

        private void ApplyColumnFormatting()
        {
            foreach (DataGridViewTextBoxColumn col in Table.Columns)
            {
                RomByteDataGridRowFormatting.ApplyFormatting(col);
            }
        }
        
        private void SelectColumn(int columnIndex) => 
            Table.CurrentCell = Table.Rows[Table.CurrentCell.RowIndex].Cells[columnIndex];
        private void SelectColumn(string columnName) => 
            Table.CurrentCell = Table.Rows[Table.CurrentCell.RowIndex].Cells[columnName];

        public void BeginEditingSelectionComment()
        {
            SelectColumn(nameof(RomByteDataGridRow.Comment));
            Table.BeginEdit(true);
        }
        
        public void BeginEditingSelectionLabel()
        {
            SelectColumn(nameof(RomByteDataGridRow.Label));
            Table.BeginEdit(true);
        }

        public event IBytesGridViewer<RomByteDataGridRow>.SelectedOffsetChange SelectedOffsetChanged;

        private void AdjustSelectedColumnByKeyCode(Keys keyCode)
        {
            var adjustBy = keyCode switch { Keys.Left => -1, Keys.Right => 1, _ => 0 };
            if (adjustBy == 0)
                return;
            
            SelectColumnClamped(adjustBy);
        }

        private void TableOnCurrentCellChanged(object sender, EventArgs e)
        {
            var selectedRomByteRow = SelectedRomByteRow;
            if (selectedRomByteRow == null)
                return;
            
            SelectedOffsetChanged?.Invoke(this, 
                new IBytesGridViewer<RomByteDataGridRow>.SelectedOffsetChangedEventArgs {Row=selectedRomByteRow});
        }
        
        /*
        private void AdjustSelectedOffsetByKeyCode(Keys keyCode, int offset)
        {
            var newOffset = CalcNewOffsetFromKeyCode(keyCode, offset);
            SelectRowOffset(newOffset);
        }

        public void SelectRowOffset(int offset)
        {
            SelectRowOffset(offset, SelectedColumnIndex);
        }
        
        public void table_MouseWheel(object sender, MouseEventArgs e)
        {
            ScrollTableBy(e.Delta);
        }

        public void ScrollTableBy(int delta)
        
        // we don't need this anymore? {
        
            if (IsDataValid())
                return;
            
            // TODO: refactor
            
            int selRow = Table.CurrentCell.RowIndex + ViewOffset, selCol = Table.CurrentCell.ColumnIndex;
            var amount = delta / 0x18;
            ViewOffset -= amount;
            
            UpdateDataGridView();
            
            if (selRow < ViewOffset) 
                selRow = ViewOffset;
            else if (selRow >= ViewOffset + RowsToShow) 
                selRow = ViewOffset + RowsToShow - 1;
            
            Table.CurrentCell = Table.Rows[selRow - ViewOffset].Cells[selCol];
            
            InvalidateTable();
        }

        
        // TODO: this should be part of some kind of DataView class that handles
        // dealing with the underlying transform of the full dataset => small window of data we're looking at. 
        //
        // dataOffset is a ROM offset. it doesn't know about our window or view or anything like that.
        // this function needs to get our view/table/window to jump to show that address.
        // the table itself doesn't scroll. instead, we delete all the contents and re-create it.
        /*public void SelectRowOffset(int dataOffset, int col)
        {
            var outOfBoundsBefore = dataOffset < ViewOffset;
            var viewOffset = ViewOffset + RowsToShow;
            var outOfBoundsAfter = dataOffset >= viewOffset;

            // set the DataGrid's real row# (offset in our current VIEW, not the underlying data)
            var viewRow = 0;
            if (outOfBoundsAfter)
                viewRow = RowsToShow - 1;
            else if (!outOfBoundsBefore) 
                viewRow = dataOffset - ViewOffset;

            //----
            
            /* order of operations:
            1) set ViewOffset
            2) call UpdateDataGridView() if needed
            3) set table.CurrentCell #3#
            
            // TODO: this could be combined with ScrollTo() which is doing something really similar.
            if (outOfBoundsBefore)
                ViewOffset = dataOffset;
            else if (outOfBoundsAfter) 
                ViewOffset = dataOffset - RowsToShow + 1;

            if (outOfBoundsBefore || outOfBoundsAfter)
                UpdateDataGridView();

            // TODO: basically doing what SetCurrentCellTo() is doing, refactor.
            Table.CurrentCell = Table.Rows[viewRow].Cells[col];
        }
        
        public void UpdateDataGridView()
        {
            if (IsDataValid())
                return;

            RowsToShow = (Table.Height - Table.ColumnHeadersHeight) / Table.RowTemplate.Height;

            if (ViewOffset + RowsToShow > Data.GetRomSize())
                ViewOffset = Data.GetRomSize() - RowsToShow;
            if (ViewOffset < 0)
                ViewOffset = 0;

            vScrollBar1.Enabled = true;
            vScrollBar1.Maximum = Data.GetRomSize() - RowsToShow;
            vScrollBar1.Value = ViewOffset;
            
            Table.RowCount = RowsToShow;

            OnGridViewChanged();
        }

        public void ScrollTo(int selOffset)
        {
            if (Table.CurrentCell == null)
                return;
            
            // TODO: something might be screwed up in here in the refactor
            
            ViewOffset = selOffset;
            
            // pre condition: ViewOffset was previously set to the updated value.
            
            UpdateDataGridView();

            var newRow = 0;
            if (selOffset < ViewOffset)
                newRow = 0;
            else if (selOffset >= ViewOffset + RowsToShow)
                newRow = RowsToShow - 1;
            else
                newRow = selOffset - ViewOffset;
            
            SetCurrentCellTo(newRow);

            InvalidateTable();
        }#1#
        //
        // private int ClampOffsetToDataBounds(int offset) => Util.ClampIndex(offset, Data.GetRomSize());
        //
        // private int CalcNewOffsetFromKeyCode(Keys keyCode, int offset)
        // {
        //     return ClampOffsetToDataBounds(GetOffsetDeltaFromKeycode(keyCode) + offset);
        // }

        /#1#/ should be no longer needed since replaced by RowByteDataGridRow
        private void table_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            var row = e.RowIndex + ViewOffset;
            if (row >= Data.GetRomSize()) return;
            switch (e.ColumnIndex)
            {
                case 0:
                    e.Value = Data.GetLabelName(Data.ConvertPCtoSnes(row));
                    break;
                case 1:
                    e.Value = Util.NumberToBaseString(Data.ConvertPCtoSnes(row), Util.NumberBase.Hexadecimal,
                        6);
                    break;
                case 2:
                    e.Value = (char) Data.GetRomByte(row);
                    break;
                case 3:
                    e.Value = Util.NumberToBaseString(Data.GetRomByte(row), DisplayBase);
                    break;
                case 4:
                    e.Value = RomUtil.PointToString(Data.GetInOutPoint(row));
                    break;
                case 5:
                    var len = Data.GetInstructionLength(row);
                    e.Value = row + len <= Data.GetRomSize() ? Data.GetInstruction(row) : "";
                    break;
                case 6:
                    var ia = Data.GetIntermediateAddressOrPointer(row);
                    e.Value = ia >= 0 ? Util.NumberToBaseString(ia, Util.NumberBase.Hexadecimal, 6) : "";
                    break;
                case 7:
                    e.Value = Util.GetEnumDescription(Data.GetFlag(row));
                    break;
                case 8:
                    e.Value = Util.NumberToBaseString(Data.GetDataBank(row), Util.NumberBase.Hexadecimal, 2);
                    break;
                case 9:
                    e.Value = Util.NumberToBaseString(Data.GetDirectPage(row), Util.NumberBase.Hexadecimal, 4);
                    break;
                case 10:
                    e.Value = RomUtil.BoolToSize(Data.GetMFlag(row));
                    break;
                case 11:
                    e.Value = RomUtil.BoolToSize(Data.GetXFlag(row));
                    break;
                case 12:
                    e.Value = Data.GetComment(Data.ConvertPCtoSnes(row));
                    break;
            }
        }

        private void table_CellValuePushed(object sender, DataGridViewCellValueEventArgs e)
        {
            string strValue = e.Value as string;
            // int value;
            int romOffset = e.RowIndex + ViewOffset;
            if (romOffset >= Data.GetRomSize())
                return;
            
            switch (e.ColumnIndex)
            {
                case 0:
                    this.
                    Controller.AddLabel(Data.ConvertPCtoSnes(romOffset), 
                        new Diz.Core.model.Label
                        {
                            Name = strValue
                        },
                        true);
                    break; // todo (validate for valid label characters)
                case 8: {
                    if (int.TryParse(strValue, NumberStyles.HexNumber, null, out var value))
                        Controller.SetDataBank(romOffset, value);
                    break;
                }
                case 9: {
                    if (int.TryParse(strValue, NumberStyles.HexNumber, null, out var value))
                        Controller.SetDirectPage(romOffset, value);
                    break;
                }
                case 10:
                    Controller.SetMFlag(romOffset, (strValue == "8" || strValue == "M"));
                    break;
                case 11:
                    Controller.SetXFlag(romOffset, (strValue == "8" || strValue == "X"));
                    break;
                case 12:
                    Controller.AddComment(Data.ConvertPCtoSnes(romOffset), strValue, true);
                    break;
            }

            Table.InvalidateRow(e.RowIndex);
        }#1#/*
#1#
    }*/
    }
}
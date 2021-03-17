using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;
using DiztinGUIsh.controller;

namespace DiztinGUIsh.window
{
    /*public class MemoryTableUserControl
    {
        // -----------------------------------------------------------------
        // these eventually should go into the designer. for now we fake this.
        // public DataGridView Table { get; init; }
        // public VScrollBar vScrollBar1; // TEMP
        // -----------------------------------------------------------------
        public Util.NumberBase DisplayBase { get; set; } = Util.NumberBase.Hexadecimal;

        // ROM offset that corresponds to the top row of our table.
        // (i.e. if we're scrolled 1000 bytes into the ROM, then row 0 of our table is address 0x1000 in the ROM) 
        // if rowsToShow is Ten, then row[0] ROM address is 1000, and row[max-1] ROM address is 1010
        public int ViewOffset
        {
            get => Controller.StartingOffset;
            set => Controller.StartingOffset = value;
        }
        
        public int RowsToShow { get; set; }

        // public int SelectedOffset => Table.CurrentCell.RowIndex + ViewOffset;

        // public IMemoryTableController Controller { get; set; }

        

        public void Init()
        {
            // dont need? RowsToShow = (Table.Height - Table.ColumnHeadersHeight) / Table.RowTemplate.Height;
        }
        
        /*
        public void table_MouseWheel(object sender, MouseEventArgs e)
        {
            ScrollTableBy(e.Delta);
        }

        public void ScrollTableBy(int delta)
        {
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
        }#1#
        
        /*
        public void SetCurrentCellTo(int i)
        {
            Table.CurrentCell = Table.Rows[i].Cells[Table.CurrentCell.ColumnIndex];
        }
        
        public int GetSelectedOffset()
        {
            return Table.CurrentCell.RowIndex + ViewOffset;
        }

        private ISnesInstructionReader Data { get; set; } // todo hook up

        }

        private void AdjustSelectedColumnByKeyCode(Keys keyCode)
        {
            var adjustBy = keyCode switch { Keys.Left => -1, Keys.Right => 1, _ => 0 };
            if (adjustBy == 0)
                return;
            
            SelectColumnClamped(adjustBy);
        }
        

        enum ColumnType
        {
            Label = 0,
            Comment = 12,
        }
        
        private void SelectColumn(ColumnType column) => SelectColumn((int)column);
        private void SelectColumn(int columnIndex)
        {
            Table.CurrentCell = Table.Rows[Table.CurrentCell.RowIndex].Cells[columnIndex];
        }
        
        private void AdjustSelectedOffsetByKeyCode(Keys keyCode, int offset)
        {
            var newOffset = CalcNewOffsetFromKeyCode(keyCode, offset);
            SelectRowOffset(newOffset);
        }

        public void SelectRowOffset(int offset)
        {
            SelectRowOffset(offset, SelectedColumnIndex);
        }#1#
        
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
        }#1#


        /*public void UpdateDataGridView()
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
        }#1#

        /*
        private void OnGridViewChanged()
        {
            // TODO: call this stuff back in the main form via event:
            // importerMenuItemsEnabled = true;
            // UpdateImporterEnabledStatus();
        }
        #1#

        

        /*
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

        public void BeginEditingComment()
        {
            SelectColumn(ColumnType.Comment);
            Table.BeginEdit(true);
        }
        
        public void BeginEditingLabel()
        {
            SelectColumn(ColumnType.Label);
            Table.BeginEdit(true);
        }#1#
    }*/
}
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;
using DiztinGUIsh.controller;

namespace DiztinGUIsh.window
{
    public class MemoryTableUserControl
    {
        // -----------------------------------------------------------------
        // these eventually should go into the designer. for now we fake this.
        public DataGridView Table { get; init; }
        public VScrollBar vScrollBar1; // TEMP
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

        public int SelectedOffset => Table.CurrentCell.RowIndex + ViewOffset;

        public IMemoryTableController Controller { get; set; }

        public void InvalidateTable() => Table.Invalidate();

        public void Init()
        {
            Table.CellValueNeeded += table_CellValueNeeded;
            Table.CellValuePushed += table_CellValuePushed;
            Table.CellPainting += table_CellPainting;

            RowsToShow = (Table.Height - Table.ColumnHeadersHeight) / Table.RowTemplate.Height;

            // https://stackoverflow.com/a/1506066
            typeof(DataGridView).InvokeMember(
                "DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.SetProperty,
                null,
                Table,
                new object[] {true});
        }
        
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
        }
        
        public void SetCurrentCellTo(int i)
        {
            Table.CurrentCell = Table.Rows[i].Cells[Table.CurrentCell.ColumnIndex];
        }
        
        public int GetSelectedOffset()
        {
            return Table.CurrentCell.RowIndex + ViewOffset;
        }

        private ISnesInstructionReader Data { get; set; } // todo hook up

        // TODO: hook up to real handler, right now this is called manually.
        public void table_KeyDown(object sender, KeyEventArgs e)
        {
            if (IsDataValid()) 
                return;

            var offset = GetSelectedOffset();

            switch (e.KeyCode)
            {
                // nav
                case Keys.Home: case Keys.End:
                case Keys.PageUp: case Keys.PageDown:
                case Keys.Up: case Keys.Down:
                    AdjustSelectedOffsetByKeyCode(e.KeyCode, offset);
                    break;
                case Keys.Left: case Keys.Right:
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
        }

        private void AdjustSelectedColumnByKeyCode(Keys keyCode)
        {
            var adjustBy = keyCode switch { Keys.Left => -1, Keys.Right => 1, _ => 0 };
            if (adjustBy == 0)
                return;
            
            SelectColumnClamped(adjustBy);
        }
        
        private int SelectedColumnIndex => Table.CurrentCell.ColumnIndex;

        private void SelectColumnClamped(int offset)
        {
            SelectColumn(Util.ClampIndex(SelectedColumnIndex + offset, Table.ColumnCount));
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
        }
        
        // TODO: this should be part of some kind of DataView class that handles
        // dealing with the underlying transform of the full dataset => small window of data we're looking at. 
        //
        // dataOffset is a ROM offset. it doesn't know about our window or view or anything like that.
        // this function needs to get our view/table/window to jump to show that address.
        // the table itself doesn't scroll. instead, we delete all the contents and re-create it.
        public void SelectRowOffset(int dataOffset, int col)
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
            3) set table.CurrentCell */
            
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

        private void OnGridViewChanged()
        {
            // TODO: call this stuff back in the main form via event:
            // importerMenuItemsEnabled = true;
            // UpdateImporterEnabledStatus();
        }

        private bool IsDataValid() => Data == null || Data.GetRomSize() <= 0;

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
        }

        private int ClampOffsetToDataBounds(int offset) => Util.ClampIndex(offset, Data.GetRomSize());

        private int CalcNewOffsetFromKeyCode(Keys keyCode, int offset)
        {
            return ClampOffsetToDataBounds(GetOffsetDeltaFromKeycode(keyCode) + offset);
        }

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
        }

        public void BeginEditingComment()
        {
            SelectColumn(ColumnType.Comment);
            Table.BeginEdit(true);
        }
        
        public void BeginEditingLabel()
        {
            SelectColumn(ColumnType.Label);
            Table.BeginEdit(true);
        }

        private void table_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            int row = e.RowIndex + ViewOffset;
            if (row < 0 || row >= Data.GetRomSize()) return;
            PaintCell(row, e.CellStyle, e.ColumnIndex, Table.CurrentCell.RowIndex + ViewOffset);
        }
        
        public void PaintCell(int offset, DataGridViewCellStyle style, int column, int selOffset)
        {
            // editable cells show up green
            if (column == 0 || column == 8 || column == 9 || column == 12) style.SelectionBackColor = Color.Chartreuse;

            switch (Data.GetFlag(offset))
            {
                case Diz.Core.model.FlagType.Unreached:
                    style.BackColor = Color.LightGray;
                    style.ForeColor = Color.DarkSlateGray;
                    break;
                case FlagType.Opcode:
                    int opcode = Data.GetRomByte(offset);
                    switch (column)
                    {
                        case 4: // <*>
                            InOutPoint point = Data.GetInOutPoint(offset);
                            int r = 255, g = 255, b = 255;
                            if ((point & (InOutPoint.EndPoint | InOutPoint.OutPoint)) != 0) g -= 50;
                            if ((point & (InOutPoint.InPoint)) != 0) r -= 50;
                            if ((point & (InOutPoint.ReadPoint)) != 0) b -= 50;
                            style.BackColor = Color.FromArgb(r, g, b);
                            break;
                        case 5: // Instruction
                            if (opcode == 0x40 || opcode == 0xCB || opcode == 0xDB || opcode == 0xF8 // RTI WAI STP SED
                                || opcode == 0xFB || opcode == 0x00 || opcode == 0x02 ||
                                opcode == 0x42 // XCE BRK COP WDM
                            ) style.BackColor = Color.Yellow;
                            break;
                        case 8: // Data Bank
                            if (opcode == 0xAB || opcode == 0x44 || opcode == 0x54) // PLB MVP MVN
                                style.BackColor = Color.OrangeRed;
                            else if (opcode == 0x8B) // PHB
                                style.BackColor = Color.Yellow;
                            break;
                        case 9: // Direct Page
                            if (opcode == 0x2B || opcode == 0x5B) // PLD TCD
                                style.BackColor = Color.OrangeRed;
                            if (opcode == 0x0B || opcode == 0x7B) // PHD TDC
                                style.BackColor = Color.Yellow;
                            break;
                        case 10: // M Flag
                        case 11: // X Flag
                            int mask = column == 10 ? 0x20 : 0x10;
                            if (opcode == 0x28 || ((opcode == 0xC2 || opcode == 0xE2) // PLP SEP REP
                                                   && (Data.GetRomByte(offset + 1) & mask) != 0)
                            ) // relevant bit set
                                style.BackColor = Color.OrangeRed;
                            if (opcode == 0x08) // PHP
                                style.BackColor = Color.Yellow;
                            break;
                    }

                    break;
                case FlagType.Operand:
                    style.ForeColor = Color.LightGray;
                    break;
                case FlagType.Graphics:
                    style.BackColor = Color.LightPink;
                    break;
                case FlagType.Music:
                    style.BackColor = Color.PowderBlue;
                    break;
                case FlagType.Data8Bit:
                case FlagType.Data16Bit:
                case FlagType.Data24Bit:
                case FlagType.Data32Bit:
                    style.BackColor = Color.NavajoWhite;
                    break;
                case FlagType.Pointer16Bit:
                case FlagType.Pointer24Bit:
                case FlagType.Pointer32Bit:
                    style.BackColor = Color.Orchid;
                    break;
                case FlagType.Text:
                    style.BackColor = Color.Aquamarine;
                    break;
                case FlagType.Empty:
                    style.BackColor = Color.DarkSlateGray;
                    style.ForeColor = Color.LightGray;
                    break;
            }

            if (selOffset >= 0 && selOffset < Data.GetRomSize())
            {
                if (column == 1
                    //&& (Data.GetFlag(selOffset) == FlagType.Opcode || Data.GetFlag(selOffset) == FlagType.Unreached)
                    && Data.ConvertSnesToPc(Data.GetIntermediateAddressOrPointer(selOffset)) == offset
                ) style.BackColor = Color.DeepPink;

                if (column == 6
                    //&& (Data.GetFlag(offset) == FlagType.Opcode || Data.GetFlag(offset) == FlagType.Unreached)
                    && Data.ConvertSnesToPc(Data.GetIntermediateAddressOrPointer(offset)) == selOffset
                ) style.BackColor = Color.DeepPink;
            }
        }
    }
}
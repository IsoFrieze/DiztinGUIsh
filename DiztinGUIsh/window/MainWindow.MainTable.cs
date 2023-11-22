using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Diz.Controllers.controllers;
using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.Core.util;
using Diz.Cpu._65816;

namespace DiztinGUIsh.window
{
    // Everything in here should probably go in its own usercontrol for JUST the table.
    // It's a complicated little beast.
    public partial class MainWindow
    {
        // Data offset of the selected row
        public int SelectedOffset => table.CurrentCell.RowIndex + ViewOffset;

        private int rowsToShow;
        private bool moveWithStep = true;

        public void InvalidateTable() => table.Invalidate();

        private void ScrollTableBy(int delta)
        {
            if (Project?.Data == null || Project.Data.GetRomSize() <= 0)
                return;
            int selRow = table.CurrentCell.RowIndex + ViewOffset, selCol = table.CurrentCell.ColumnIndex;
            var amount = delta / 0x18;
            ViewOffset -= amount;
            UpdateDataGridView();
            if (selRow < ViewOffset) selRow = ViewOffset;
            else if (selRow >= ViewOffset + rowsToShow) selRow = ViewOffset + rowsToShow - 1;
            table.CurrentCell = table.Rows[selRow - ViewOffset].Cells[selCol];
            InvalidateTable();
        }

        private void vScrollBar1_ValueChanged(object sender, EventArgs e)
        {
            if (table.CurrentCell == null)
                return;

            int selOffset = table.CurrentCell.RowIndex + ViewOffset;
            ViewOffset = vScrollBar1.Value;
            UpdateDataGridView();

            if (selOffset < ViewOffset) table.CurrentCell = table.Rows[0].Cells[table.CurrentCell.ColumnIndex];
            else if (selOffset >= ViewOffset + rowsToShow)
                table.CurrentCell = table.Rows[rowsToShow - 1].Cells[table.CurrentCell.ColumnIndex];
            else table.CurrentCell = table.Rows[selOffset - ViewOffset].Cells[table.CurrentCell.ColumnIndex];

            InvalidateTable();
        }

        private void table_MouseDown(object sender, MouseEventArgs e)
        {
            InvalidateTable();
        }

        private void table_SelectionChanged(object sender, EventArgs e)
        {
            SelectOffset(SelectedOffset, -1);
        }

        private void table_CellClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void table_KeyDown(object sender, KeyEventArgs e)
        {
            if (Project?.Data == null || Project.Data.GetRomSize() <= 0) return;

            var offset = table.CurrentCell.RowIndex + ViewOffset;
            int newOffset;
            var amount = 0x01;

            Console.WriteLine(e.KeyCode);

            var snesData = Project.Data.GetSnesApi();
            switch (e.KeyCode)
            {
                case Keys.F3:
                    GoToNextUnreachedInPoint(offset);
                    break;
                
                case Keys.Home:
                case Keys.PageUp:
                case Keys.Up:
                    amount = e.KeyCode == Keys.Up ? 0x01 : e.KeyCode == Keys.PageUp ? 0x10 : 0x100;
                    newOffset = offset - amount;
                    if (newOffset < 0) newOffset = 0;
                    SelectOffset(newOffset, -1);
                    break;
                case Keys.End:
                case Keys.PageDown:
                case Keys.Down:
                    amount = e.KeyCode == Keys.Down ? 0x01 : e.KeyCode == Keys.PageDown ? 0x10 : 0x100;
                    newOffset = offset + amount;
                    if (newOffset >= Project.Data.GetRomSize()) newOffset = Project.Data.GetRomSize() - 1;
                    SelectOffset(newOffset, -1);
                    break;
                case Keys.Left:
                    amount = table.CurrentCell.ColumnIndex;
                    amount = amount - 1 < 0 ? 0 : amount - 1;
                    table.CurrentCell = table.Rows[table.CurrentCell.RowIndex].Cells[amount];
                    break;
                case Keys.Right:
                    amount = table.CurrentCell.ColumnIndex;
                    amount = amount + 1 >= table.ColumnCount ? table.ColumnCount - 1 : amount + 1;
                    table.CurrentCell = table.Rows[table.CurrentCell.RowIndex].Cells[amount];
                    break;
                case Keys.S:
                    Step(offset);
                    break;
                case Keys.I:
                    StepIn(offset);
                    break;
                case Keys.A:
                    AutoStepSafe(offset);
                    break;
                case Keys.T:
                    GoToIntermediateAddress(offset);
                    break;
                case Keys.U:
                    GoToUnreached(true, true);
                    break;
                case Keys.H:
                    GoToUnreached(false, false);
                    break;
                case Keys.N:
                    GoToUnreached(false, true);
                    break;
                case Keys.K:
                    Mark(offset);
                    break;
                case Keys.L:
                    table.CurrentCell = table.Rows[table.CurrentCell.RowIndex].Cells[0];
                    table.BeginEdit(true);
                    break;
                case Keys.B:
                    table.CurrentCell = table.Rows[table.CurrentCell.RowIndex].Cells[8];
                    table.BeginEdit(true);
                    break;
                case Keys.D:
                    table.CurrentCell = table.Rows[table.CurrentCell.RowIndex].Cells[9];
                    table.BeginEdit(true);
                    break;
                case Keys.M:
                    snesData.SetMFlag(offset, !snesData.GetMFlag(offset));
                    break;
                case Keys.X:
                    snesData.SetXFlag(offset, !snesData.GetXFlag(offset));
                    break;
                case Keys.C:
                    table.CurrentCell = table.Rows[table.CurrentCell.RowIndex].Cells[12];
                    table.BeginEdit(true);
                    break;
                case Keys.Enter:
                    table.BeginEdit(true);
                    break;
                case Keys.Delete:
                    table.CurrentCell.Value = null;
                    break;
            }

            e.Handled = true;
            InvalidateTable();
        }

        private void table_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            var row = e.RowIndex + ViewOffset;
            if (row >= Project.Data.GetRomSize()) return;
            switch (e.ColumnIndex)
            {
                case 0:
                    e.Value = Project.Data.Labels.GetLabelName(Project.Data.ConvertPCtoSnes(row));
                    break;
                case 1:
                    e.Value = Util.NumberToBaseString(Project.Data.ConvertPCtoSnes(row), Util.NumberBase.Hexadecimal, 6);
                    break;
                case 2:
                    e.Value = (char)Project.Data.GetRomByte(row);
                    break;
                case 3:
                    e.Value = Util.NumberToBaseString(Project.Data.GetRomByte(row) ?? 0x0, displayBase);
                    break;
                case 4:
                    e.Value = RomUtil.PointToString(Project.Data.GetSnesApi().GetInOutPoint(row));
                    break;
                case 5:
                    var len = Project.Data.GetSnesApi().GetInstructionLength(row);
                    e.Value = row + len <= Project.Data.GetRomSize() ? Project.Data.GetSnesApi().GetInstruction(row) : "";
                    break;
                case 6:
                    var ia = Project.Data.GetSnesApi().GetIntermediateAddressOrPointer(row);
                    e.Value = ia >= 0 ? Util.NumberToBaseString(ia, Util.NumberBase.Hexadecimal, 6) : "";
                    break;
                case 7:
                    e.Value = Util.GetEnumDescription(Project.Data.GetSnesApi().GetFlag(row));
                    break;
                case 8:
                    e.Value = Util.NumberToBaseString(Project.Data.GetSnesApi().GetDataBank(row), Util.NumberBase.Hexadecimal, 2);
                    break;
                case 9:
                    e.Value = Util.NumberToBaseString(Project.Data.GetSnesApi().GetDirectPage(row), Util.NumberBase.Hexadecimal, 4);
                    break;
                case 10:
                    e.Value = RomUtil.BoolToSize(Project.Data.GetSnesApi().GetMFlag(row));
                    break;
                case 11:
                    e.Value = RomUtil.BoolToSize(Project.Data.GetSnesApi().GetXFlag(row));
                    break;
                case 12:
                    e.Value = Project.Data.GetCommentText(Project.Data.ConvertPCtoSnes(row));
                    break;
            }
        }

        private void table_CellValuePushed(object sender, DataGridViewCellValueEventArgs e)
        {
            string value = e.Value as string;
            int result;
            int row = e.RowIndex + ViewOffset;
            if (row >= Project.Data.GetRomSize()) return;
            switch (e.ColumnIndex)
            {
                case 0:
                    Project.Data.Labels.AddLabel(Project.Data.ConvertPCtoSnes(row), new Diz.Core.model.Label() { Name = value }, true);
                    break; // todo (validate for valid label characters)
                case 8:
                    if (int.TryParse(value, NumberStyles.HexNumber, null, out result)) Project.Data.GetSnesApi().SetDataBank(row, result);
                    break;
                case 9:
                    if (int.TryParse(value, NumberStyles.HexNumber, null, out result)) Project.Data.GetSnesApi().SetDirectPage(row, result);
                    break;
                case 10:
                    Project.Data.GetSnesApi().SetMFlag(row, (value == "8" || value == "M"));
                    break;
                case 11:
                    Project.Data.GetSnesApi().SetXFlag(row, (value == "8" || value == "X"));
                    break;
                case 12:
                    Project.Data.AddComment(Project.Data.ConvertPCtoSnes(row), value, true);
                    break;
            }

            table.InvalidateRow(e.RowIndex);
        }

        public void PaintCell(int offset, DataGridViewCellStyle style, int column, int selOffset)
        {
            // editable cells show up green
            if (column == 0 || column == 8 || column == 9 || column == 12) style.SelectionBackColor = Color.Chartreuse;

            switch (Project.Data.GetSnesApi().GetFlag(offset))
            {
                case FlagType.Unreached:
                    style.BackColor = Color.LightGray;
                    style.ForeColor = Color.DarkSlateGray;
                    break;
                case FlagType.Opcode:
                    int opcode = Project.Data.GetRomByte(offset) ?? 0x0;
                    switch (column)
                    {
                        case 4: // <*>
                            InOutPoint point = Project.Data.GetSnesApi().GetInOutPoint(offset);
                            int r = 255, g = 255, b = 255;
                            if ((point & (InOutPoint.EndPoint | InOutPoint.OutPoint)) != 0) g -= 50;
                            if ((point & (InOutPoint.InPoint)) != 0) r -= 50;
                            if ((point & (InOutPoint.ReadPoint)) != 0) b -= 50;
                            style.BackColor = Color.FromArgb(r, g, b);
                            break;
                        case 5: // Instruction
                            if (opcode == 0x40 || opcode == 0xCB || opcode == 0xDB || opcode == 0xF8 // RTI WAI STP SED
                                || opcode == 0xFB || opcode == 0x00 || opcode == 0x02 || opcode == 0x42 // XCE BRK COP WDM
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
                                && (Project.Data.GetRomByte(offset + 1) & mask) != 0)) // relevant bit set
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

            if (selOffset >= 0 && selOffset < Project.Data.GetRomSize())
            {
                if (column == 1
                    //&& (Project.Data.GetFlag(selOffset) == Data.FlagType.Opcode || Project.Data.GetFlag(selOffset) == Data.FlagType.Unreached)
                    && Project.Data.ConvertSnesToPc(Project.Data.GetSnesApi().GetIntermediateAddressOrPointer(selOffset)) == offset
                ) style.BackColor = Color.DeepPink;

                if (column == 6
                    //&& (Project.Data.GetFlag(offset) == Data.FlagType.Opcode || Project.Data.GetFlag(offset) == Data.FlagType.Unreached)
                    && Project.Data.ConvertSnesToPc(Project.Data.GetSnesApi().GetIntermediateAddressOrPointer(offset)) == selOffset
                ) style.BackColor = Color.DeepPink;
            }
        }

        private void table_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            int row = e.RowIndex + ViewOffset;
            if (row < 0 || row >= Project.Data.GetRomSize()) return;
            PaintCell(row, e.CellStyle, e.ColumnIndex, table.CurrentCell.RowIndex + ViewOffset);
        }

        public void MarkHistoryPoint(int pcOffset, ISnesNavigation.HistoryArgs historyArgs, string position)
        {
            if (historyArgs == null) 
                return;

            historyArgs.Position = position;
            
            RememberNavigationPoint(SelectedOffset, historyArgs); // save old position
        }

        public void SelectOffset(int pcOffset, ISnesNavigation.HistoryArgs historyArgs = null)
            => SelectOffset(pcOffset, -1, historyArgs);

        public void SelectOffset(int pcOffset, int column = -1, ISnesNavigation.HistoryArgs historyArgs = null, int overshootAmount=0)
        {
            if (pcOffset == -1)
                return;
            
            MarkHistoryPoint(SelectedOffset, historyArgs, "start");

            // purely visual. allows this offset to appear more in the middle of the screen, instead of at the very bottom
            // you typically want to be presented with a view that shows stuff of interest you jumped to 
            // visible and not having to scroll down a bit then back up.
            //
            // THIS IS 100% OPTIONAL.
            if (overshootAmount > 0)
            {
                // ideally, we'd calculate this number to be at the center or top.
                var overshotOffset = Math.Min(Project.Data.GetRomSize()-1, pcOffset + overshootAmount);
                InternalSelectOffset(overshotOffset, column);
            }
            
            // do the real thing
            InternalSelectOffset(pcOffset, column);
            
            MarkHistoryPoint(pcOffset, historyArgs, "end");

            InvalidateTable();
        }

        private void InternalSelectOffset(int pcOffset, int column)
        {
            var col = column == -1 ? table.CurrentCell.ColumnIndex : column;
            if (pcOffset < ViewOffset)
            {
                ViewOffset = pcOffset;
                UpdateDataGridView();
                table.CurrentCell = table.Rows[0].Cells[col];
            }
            else if (pcOffset >= ViewOffset + rowsToShow)
            {
                ViewOffset = pcOffset - rowsToShow + 1;
                UpdateDataGridView();
                table.CurrentCell = table.Rows[rowsToShow - 1].Cells[col];
            }
            else
            {
                table.CurrentCell = table.Rows[pcOffset - ViewOffset].Cells[col];
            }
        }

        private void InitMainTable()
        {
            table.CellValueNeeded += table_CellValueNeeded;
            table.CellValuePushed += table_CellValuePushed;
            table.CellPainting += table_CellPainting;

            rowsToShow = ((table.Height - table.ColumnHeadersHeight) / table.RowTemplate.Height);

            // https://stackoverflow.com/a/1506066
            typeof(DataGridView).InvokeMember(
                "DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.SetProperty,
                null,
                table,
                new object[] {true});
        }

        private void BeginEditingComment()
        {
            table.CurrentCell = table.Rows[table.CurrentCell.RowIndex].Cells[12];
            table.BeginEdit(true);
        }

        private void BeginAddingLabel()
        {
            table.CurrentCell = table.Rows[table.CurrentCell.RowIndex].Cells[0];
            table.BeginEdit(true);
        }
    }
}
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;
using Equin.ApplicationFramework;

namespace DiztinGUIsh.window2
{ 
    public class RomByteDatagridRow
    {
        private readonly int ia;

        public RomByteDatagridRow(RomByte rb, Data d, IBytesViewer parentView)
        {
            RomByte = rb;
            Data = d;
            ParentView = parentView;
        }
        
        [Browsable(false)] public RomByteData RomByte { get; }
        [Browsable(false)] public Data Data { get; }
        [Browsable(false)] public IBytesViewer ParentView { get; }
        

        public Util.NumberBase NumberBase => ParentView.DataGridNumberBase;

        public string Label => Data.GetLabelName(Data.ConvertPCtoSnes(RomByte.Offset));
        
        public string Offset => Util.NumberToBaseString(Data.ConvertPCtoSnes(RomByte.Offset), Util.NumberBase.Hexadecimal,6);

        // show the byte two different ways: ascii and numeric
        public char AsciiCharRep => (char) RomByte.Rom;
        public string NumericRep => Util.NumberToBaseString(RomByte.Rom, this.NumberBase);
        
        public string InOut => RomUtil.PointToString(RomByte.Point);

        public string Instruction
        {
            get
            {
                var len = Data.GetInstructionLength(RomByte.Offset);
                return len <= Data.GetRomSize() ? Data.GetInstruction(RomByte.Offset) : "";
            }
        }

        public string IA
        {
            get
            {
                var ia = Data.GetIntermediateAddressOrPointer(RomByte.Offset);
                return ia >= 0 ? Util.NumberToBaseString(ia, Util.NumberBase.Hexadecimal, 6) : "";
            }
        }

        public string Flag => Util.GetEnumDescription(Data.GetFlag(RomByte.Offset));
        public string B => Util.NumberToBaseString(Data.GetDataBank(RomByte.Offset), Util.NumberBase.Hexadecimal, 2);
        public string D => Util.NumberToBaseString(Data.GetDirectPage(RomByte.Offset), Util.NumberBase.Hexadecimal, 4);
        public string M => RomUtil.BoolToSize(Data.GetMFlag(RomByte.Offset));
        public string X => RomUtil.BoolToSize(Data.GetXFlag(RomByte.Offset));
        public string Comment => Data.GetComment(Data.ConvertPCtoSnes(RomByte.Offset));
    }
    
    public partial class DataGridEditorControl : UserControl, IBytesViewer
    {
        #region Init

        private IBytesViewerController controller;
        public Util.NumberBase DataGridNumberBase { get; } // TODO

        public IBytesViewerController Controller
        {
            get => controller;
            set
            {
                controller = value;
                dataGridView1.DataSource = controller.BindingList;
            }
        }

        public DataGridEditorControl()
        {
            InitializeComponent();
            
            dataGridView1.AutoGenerateColumns = true;
            dataGridView1.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
            dataGridView1.BorderStyle = BorderStyle.Fixed3D;
            dataGridView1.EditMode = DataGridViewEditMode.EditOnEnter;
            
            // probably?
            dataGridView1.VirtualMode = true;

            dataGridView1.CellPainting += table_CellPainting;
            // TODO Table.CellValueNeeded += table_CellValueNeeded;
            // TODO Table.CellValuePushed += table_CellValuePushed;

            HackDoubleBufferedEnable();
            
            CellConditionalFormatterCollection.RegisterAllRomByteFormattersHelper();
        }

        public CellConditionalFormatterCollection CellConditionalFormatterCollection = new(); 
        
        // remove this eventually, shortcut for now.
        public DataGridView Table => dataGridView1;
        
        public Util.NumberBase DisplayBase { get; set; } = Util.NumberBase.Hexadecimal;

        // public int SelectedOffset => Table.CurrentCell.RowIndex + ViewOffset;
        
        public void InvalidateTable() => Table.Invalidate();

        private void HackDoubleBufferedEnable()
        {
            // https://stackoverflow.com/a/1506066
            typeof(DataGridView).InvokeMember(
                "DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.SetProperty,
                null,
                dataGridView1,
                new object[] {true});
        }

        
        private int SelectedColumnIndex => Table.CurrentCell.ColumnIndex;

        private void SelectColumnClamped(int offset)
        {
            // SelectColumn(Util.ClampIndex(SelectedColumnIndex + offset, Table.ColumnCount));
        }
        #endregion
        
        #region KeyboardHandler

        public Data Data => Controller.Data;
        
        private bool IsDataValid() => Data == null || Data.GetRomSize() <= 0;
        
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

        #endregion

        #region Painting

        public RomByteData GetRomByteAtRow(int row) => 
            (Table.Rows[row].DataBoundItem as ObjectView<RomByteData>)?.Object;
        public RomByteData SelectedRomByte => 
            Table.CurrentRow == null 
                ? null : GetRomByteAtRow(Table.CurrentRow.Index);

        public int ViewOffset => SelectedRomByte?.Offset ?? -1;

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
            if (!IsDataValid() || e.RowIndex == -1 || e.ColumnIndex == -1)
                return;
            
            // int row = e.RowIndex + ViewOffset;
            // int selOffset = Table.CurrentCell.RowIndex + ViewOffset;

            var romByteAtRow = GetRomByteAtRow(e.RowIndex);
            var colHeaderDataProperty = GetColumnHeaderDataProperty(e);
            
            if (romByteAtRow == null || string.IsNullOrEmpty(colHeaderDataProperty))
                return;
            
            PaintCell(e.RowIndex, e.ColumnIndex, romByteAtRow, colHeaderDataProperty, e.CellStyle);
        }

        /// <summary>
        /// Format an arbitrary cell in the grid. it may or may not be the currently selected cell 
        /// </summary>
        /// <param name="row">row of cell to format</param>
        /// <param name="col">column of cell to format</param>
        /// <param name="rowRomByte">the RomByte associated with this row</param>
        /// <param name="colPropName">the name of the data property associated with this column (not the column header, this is the internal name)</param>
        /// <param name="style">Out param, modify this to set the style</param>
        private void PaintCell(int row, int col, RomByteData rowRomByte, string colPropName, DataGridViewCellStyle style)
        {
            var formatter = CellConditionalFormatterCollection.Get(colPropName);

            // editable cells that are selected show up in what I call "fancy green"
            if (formatter.IsEditable)
                style.SelectionBackColor = Color.Chartreuse;

            // all cells in a row get this treatment
            switch (rowRomByte.TypeFlag)
            {
                case FlagType.Unreached:
                    style.BackColor = Color.LightGray;
                    style.ForeColor = Color.DarkSlateGray;
                    break;
                case FlagType.Opcode:
                    var color = GetColorWhenRowMarkedAsOpcode(rowRomByte, colPropName);
                    if (color != null)
                        style.BackColor = color.Value;
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
            
            if (SelectedRomByte != null)
            {
                var matchingIa = colPropName switch
                {
                    "PC" => IsMatchingIntermediateAddress(SelectedRomByte.Offset, rowRomByte.Offset),
                    "IA" => IsMatchingIntermediateAddress(rowRomByte.Offset, SelectedRomByte.Offset),
                    _ => false
                };

                if (matchingIa)
                    style.BackColor = Color.DeepPink;
            }
        }

        private Color? GetColorWhenRowMarkedAsOpcode(RomByteData rowRomByte, string colPropName)
        {
            // weird edge case for M/X flags off end of ROM, just make it zero as as default if we hit this.
            // it's just coloring.
            var nextByte = Data.GetNextRomByte(rowRomByte.Offset) ?? 0;
            
            return colPropName switch
            {
                "<*>" => 
                    CellFormatterUtils.GetBackColorInOut(rowRomByte),
                "Instruction" =>
                    CellFormatterUtils.GetInstructionBackgroundColor(rowRomByte),
                "B" =>
                    CellFormatterUtils.GetDataBankColor(rowRomByte),
                "D" =>
                    CellFormatterUtils.GetDirectPageColor(rowRomByte),
                "M" =>
                    CellFormatterUtils.GetMFlagColor(rowRomByte, nextByte),
                "X" =>
                    CellFormatterUtils.GetXFlagColor(rowRomByte, nextByte),
                _ => null
            };
        }

        private bool IsMatchingIntermediateAddress(int intermediateAddress, int addressToMatch)
        {
            var intermediateAddressOrPointer = Data.GetIntermediateAddressOrPointer(intermediateAddress);
            var destinationOfIa = Data.ConvertSnesToPc(intermediateAddressOrPointer);

            return destinationOfIa == addressToMatch;
        }

        #endregion
    }

}
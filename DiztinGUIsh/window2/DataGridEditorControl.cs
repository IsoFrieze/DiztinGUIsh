using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;
using DiztinGUIsh.util;
using Equin.ApplicationFramework;
using UserControl = System.Windows.Forms.UserControl;

namespace DiztinGUIsh.window2
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class RomByteDataGridRow
    {
        [DisplayName("Label")]
        [Editable(true)]
        public string Label
        {
            get => Data.GetLabelName(Data.ConvertPCtoSnes(RomByte.Offset));
            
            // todo (validate for valid label characters)
            // (note: validation implemented in Furious's branch, integrate here)
            set => Data.AddLabel(
                Data.ConvertPCtoSnes(RomByte.Offset), 
                new Diz.Core.model.Label{Name = value}, 
                true);
        }

        [DisplayName("PC")]
        [ReadOnlyAttribute(true)]
        public string Offset => Util.NumberToBaseString(Data.ConvertPCtoSnes(RomByte.Offset), Util.NumberBase.Hexadecimal,6);

        // show the byte two different ways: ascii and numeric
        [DisplayName("@")]
        [ReadOnlyAttribute(true)]
        public char AsciiCharRep => (char) RomByte.Rom;
        
        [DisplayName("#")]
        [ReadOnlyAttribute(true)]
        public string NumericRep => Util.NumberToBaseString(RomByte.Rom, this.NumberBase);
        
        [DisplayName("<*>")]
        [ReadOnlyAttribute(true)]
        public string InOut => RomUtil.PointToString(RomByte.Point);

        [DisplayName("Instruction")]
        [ReadOnlyAttribute(true)]
        public string Instruction
        {
            get
            {
                // NOTE: this does not handle instructions whose opcodes cross banks correctly.
                // if we hit this situation, just return empty for the grid, it's likely real instruction won't do this?
                var len = Data.GetInstructionLength(RomByte.Offset);
                return RomByte.Offset + len <= Data.GetRomSize() ? Data.GetInstruction(RomByte.Offset) : "";
            }
        }

        [DisplayName("IA")]
        [ReadOnlyAttribute(true)]
        public string IA
        {
            get
            {
                var ia = Data.GetIntermediateAddressOrPointer(RomByte.Offset);
                return ia >= 0 ? Util.NumberToBaseString(ia, Util.NumberBase.Hexadecimal, 6) : "";
            }
        }

        [DisplayName("Flag")]
        [ReadOnlyAttribute(true)]
        public string Flag => Util.GetEnumDescription(Data.GetFlag(RomByte.Offset));
        
        [DisplayName("B")]
        [Editable(true)]
        public string DataBank
        {
            get => Util.NumberToBaseString(Data.GetDataBank(RomByte.Offset), Util.NumberBase.Hexadecimal, 2);
            set
            {
                if (int.TryParse(value, NumberStyles.HexNumber, null, out var parsed))
                    Data.SetDataBank(RomByte.Offset, parsed);
            }
        }

        [DisplayName("D")]
        [Editable(true)]
        public string DirectPage
        {
            get => Util.NumberToBaseString(Data.GetDirectPage(RomByte.Offset), Util.NumberBase.Hexadecimal, 4);
            set
            {
                if (int.TryParse(value, NumberStyles.HexNumber, null, out var parsed))
                    Data.SetDirectPage(RomByte.Offset, parsed);
            }
        }

        [DisplayName("M")]
        [Editable(true)]
        public string MFlag
        {
            get => RomUtil.BoolToSize(Data.GetMFlag(RomByte.Offset));
            set => Data.SetMFlag(RomByte.Offset, value == "8" || value == "M");
        }

        [DisplayName("X")]
        [Editable(true)]
        public string XFlag
        {
            get => RomUtil.BoolToSize(Data.GetXFlag(RomByte.Offset));
            set => Data.SetXFlag(RomByte.Offset, value == "8" || value == "X");
        }

        [DisplayName("Comment")]
        [Editable(true)]
        public string Comment
        {
            get => Data.GetComment(Data.ConvertPCtoSnes(RomByte.Offset));
            set => Data.AddComment(Data.ConvertPCtoSnes(RomByte.Offset), value, true);
        }

        public RomByteDataGridRow(RomByteData rb, Data d, IBytesGridViewer parentView)
        {
            RomByte = rb;
            Data = d;
            ParentView = parentView;
        }
        [Browsable(false)] public RomByteData RomByte { get; }
        [Browsable(false)] public Data Data { get; }
        [Browsable(false)] public IBytesGridViewer ParentView { get; }
        [Browsable(false)] private Util.NumberBase NumberBase => ParentView.DataGridNumberBase;
    }
    
    public partial class DataGridEditorControl : UserControl, IBytesGridViewer
    {
        #region Init

        private IBytesViewerController controller;
        public Data Data => Controller.Data;
        public Util.NumberBase DataGridNumberBase { get; set; } = Util.NumberBase.Hexadecimal;

        public IBytesViewerController Controller
        {
            get => controller;
            set
            {
                controller = value;
                DataBind();
            }
        }

        private void DataBind()
        {
            var bindingList = new BindingListView<RomByteDataGridRow>(
                Data.RomBytes.Select(romByte => 
                    new RomByteDataGridRow(romByte, Data, this)
                ).ToList());
            
            dataGridView1.DataSource = bindingList;
        }

        public DataGridEditorControl()
        {
            InitializeComponent();
            
            dataGridView1.AutoGenerateColumns = true;
            dataGridView1.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
            dataGridView1.BorderStyle = BorderStyle.Fixed3D;
            dataGridView1.EditMode = DataGridViewEditMode.EditOnEnter;
            
            // probably? probably need to do more if we enable this
            dataGridView1.VirtualMode = true;

            dataGridView1.CellPainting += table_CellPainting;
            
            // TODO Table.CellValueNeeded += table_CellValueNeeded; // may not need anymore?
            // TODO Table.CellValuePushed += table_CellValuePushed; // may not need anymore?

            // CellConditionalFormatterCollection.RegisterAllRomByteFormattersHelper();
            
            GuiUtil.EnableDoubleBuffering(typeof(DataGridView), dataGridView1);
        }

        // private readonly CellConditionalFormatterCollection CellConditionalFormatterCollection = new(); 
        
        // remove this eventually, shortcut for now.
        private DataGridView Table => dataGridView1;
        
        public Util.NumberBase DisplayBase { get; set; } = Util.NumberBase.Hexadecimal;

        // public int SelectedOffset => Table.CurrentCell.RowIndex + ViewOffset;
        
        public void InvalidateTable() => Table.Invalidate();

        private void HackDoubleBufferedEnable()
        {
            GuiUtil.EnableDoubleBuffering(typeof(DataGridView), dataGridView1);
        }

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

        #endregion

        #region Painting

        private RomByteDataGridRow GetRomByteAtRow(int row) => 
            (Table.Rows[row].DataBoundItem as ObjectView<RomByteDataGridRow>)?.Object;

        public RomByteDataGridRow SelectedRomByteRow => 
            Table.CurrentRow == null 
                ? null : GetRomByteAtRow(Table.CurrentRow.Index);

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
            
            // int row = e.RowIndex + ViewOffset;
            // int selOffset = Table.CurrentCell.RowIndex + ViewOffset;

            var romByteAtRow = GetRomByteAtRow(e.RowIndex);
            var colHeaderDataProperty = GetColumnHeaderDataProperty(e);
            
            if (romByteAtRow?.RomByte == null || string.IsNullOrEmpty(colHeaderDataProperty))
                return;
            
            SetStyleForCell(romByteAtRow, colHeaderDataProperty, e.CellStyle);
        }

        /// <summary>
        /// Format an arbitrary cell in the grid. it may or may not be the currently selected cell.
        /// </summary>
        /// <param name="rowRomByte">the RomByte associated with this row</param>
        /// <param name="colPropName">the name of the data property associated with this column (not the column header, this is the internal name)</param>
        /// <param name="style">Out param, modify this to set the style</param>
        private static void SetStyleForCell(RomByteDataGridRow rowRomByte, string colPropName, DataGridViewCellStyle style)
        {
            // var formatter = CellConditionalFormatterCollection.Get(colPropName);//old?

            // editable cells that are selected show up in what I call "fancy green"
            if (colPropName == "Comment" || colPropName == "Label" || colPropName == "B" || colPropName == "D") 
                style.SelectionBackColor = Color.Chartreuse;

            // all cells in a row get this treatment
            switch (rowRomByte.RomByte.TypeFlag)
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
            
            SetStyleForIndirectAddress(rowRomByte, colPropName, ref style);
        }

        private static void SetStyleForIndirectAddress(RomByteDataGridRow rowRomByte, string colPropName, ref DataGridViewCellStyle style)
        {
            var selectedRomByteRow = rowRomByte.ParentView.SelectedRomByteRow;
            if (selectedRomByteRow == null) 
                return;
            
            var matchingIa = colPropName switch
            {
                "PC" => rowRomByte.Data.IsMatchingIntermediateAddress(selectedRomByteRow.RomByte.Offset, rowRomByte.RomByte.Offset),
                "IA" => rowRomByte.Data.IsMatchingIntermediateAddress(rowRomByte.RomByte.Offset, selectedRomByteRow.RomByte.Offset),
                _ => false
            };

            if (matchingIa)
                style.BackColor = Color.DeepPink;
        }

        private static Color? GetColorWhenRowMarkedAsOpcode(RomByteDataGridRow romByteRow, string colPropName)
        {
            var romByte = romByteRow.RomByte;
            
            // weird edge case for M/X flags off end of ROM, just make it zero as as default if we hit this.
            // it's just coloring.
            var nextByte = romByteRow.Data.GetNextRomByte(romByte.Offset) ?? 0;
            
            // TODO: eventually, don't match strings here.
            // instead, look for the appropriate attribute attached to romByteRow and let that 
            // attribute hook in here.
            return colPropName switch
            {
                "<*>" => CellFormatterUtils.GetBackColorInOut(romByte),
                "Instruction" => CellFormatterUtils.GetInstructionBackgroundColor(romByte),
                "B" => CellFormatterUtils.GetDataBankColor(romByte),
                "D" => CellFormatterUtils.GetDirectPageColor(romByte),
                "M" => CellFormatterUtils.GetMFlagColor(romByte, nextByte),
                "X" => CellFormatterUtils.GetXFlagColor(romByte, nextByte),
                _ => null
            };
        }

        #endregion
    }

}
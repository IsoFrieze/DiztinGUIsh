using System;
using System.Drawing;
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
        #region Properties
        
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

        #endregion
        
        #region Init

        public DataGridEditorControl()
        {
            InitializeComponent();
            ExtraDesignInit();
        }

        private void ExtraDesignInit()
        {
            // stuff that should probably be in the designer, but we're migrating some old code
            Table.AutoGenerateColumns = true;
            
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
            Table.VirtualMode = false;
            // TODO Table.CellValueNeeded += table_CellValueNeeded; // may not need anymore?
            // TODO Table.CellValuePushed += table_CellValuePushed; // may not need anymore?
            
            // Table.MouseDown += table_MouseDown;
            Table.MouseWheel += table_MouseWheel;

            Table.CellPainting += table_CellPainting;
            Table.CurrentCellChanged += TableOnCurrentCellChanged;
        }

        private void table_MouseWheel(object? sender, MouseEventArgs e) => 
            AdjustSelectedOffsetByDelta(e.Delta / 0x18);

        #endregion

        #region DataBinding

        private bool IsDataValid() => Data != null && Data.GetRomSize() > 0;
        
        private void DataBind()
        {
            SuspendLayout();

            var dataGridView1BindingSource = new BindingSource
            {
                DataSource = DataSource
            };
            Table.DataSource = dataGridView1BindingSource;
            
            OnDataBindingChanged();
            ResumeLayout();
        }
        
        private void OnDataBindingChanged() => ApplyColumnFormatting();

        public void InvalidateTable() => Table.Invalidate();

        #endregion
        
        #region RowColumnAccess
        
        private RomByteDataGridRow GetRomByteAtRow(int row) =>
            (Table.Rows[row].DataBoundItem as ObjectView<RomByteDataGridRow>)?.Object;

        public RomByteDataGridRow SelectedRomByteRow =>
            Table.CurrentRow == null
                ? null
                : GetRomByteAtRow(Table.CurrentRow.Index);

        public int SelectedRowRomOffset => SelectedRomByteRow?.RomByte?.Offset ?? -1;
        
        private void SelectRowBySnesOffset(int newSnesOffsetToSelect)
        {
            // right now, rows in table are 1:1 with RomBytes.
            // in the future, we might cache a window, and this function will need to be modified to deal with that.
            
            var romOffset = Data.ConvertSnesToPc(newSnesOffsetToSelect);
            SelectRowByRomOffset(romOffset);
        }
        
        private void SelectRowByRomOffset(int romOffset)
        {
            if (romOffset < 0 || romOffset >= Data.GetRomSize()) 
                return;
            
            SelectRow(romOffset);
        }

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

        private void SelectColumn(int columnIndex) =>
            SelectCell(SelectedRow, columnIndex);
            
        private void SelectColumn(string columnName) => 
            SelectCell(SelectedRow, columnName);
        
        private void SelectColumnClamped(int adjustBy) => 
            SelectColumn(Util.ClampIndex(SelectedCol + adjustBy, Table.ColumnCount));

        private void SelectRow(int rowIndex) =>
            SelectCell(rowIndex, SelectedCol);

        private void SelectCell(int row, int col) => SelectCell(Table.Rows[row].Cells[col]);
        private void SelectCell(int row, string columnName) => SelectCell(Table.Rows[row].Cells[columnName]);
        
        private void SelectCell(DataGridViewCell cellToSelect)
        {
            Table.CurrentCell = cellToSelect;
            InvalidateTable();
        }

        #endregion

        #region KeyboardHandler

        private static int GetOffsetDeltaFromKeycode(Keys keyCode)
        {
            const int ONE = 0x01;
            const int SMALL = 0x10;
            const int LARGE = 0x80;
            
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

        #region Formatting

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

        private void ApplyColumnFormatting()
        {
            foreach (DataGridViewTextBoxColumn col in Table.Columns)
            {
                RomByteDataGridRowFormatting.ApplyFormatting(col);
            }
        }
        
        #endregion

        #region Editing
        
        public void BeginEditingSelectionComment() => BeginEditingSelectedRowProperty(nameof(RomByteDataGridRow.Comment));
        public void BeginEditingSelectionLabel() =>   BeginEditingSelectedRowProperty(nameof(RomByteDataGridRow.Label));

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

        private void BeginEditingSelectedRowProperty(string propertyName)
        {
            SelectColumn(propertyName);
            Table.BeginEdit(true);
        }

        public void AdjustSelectedOffsetByDelta(int delta)
        {
            var newRomOffset = CalcNewRomOffsetAdjustByDelta(delta);
            SelectRowByRomOffset(newRomOffset);
        }
        
        private void AdjustSelectedOffsetByKeyCode(Keys keyCode)
        {
            var newRomOffset = CalcNewRomOffsetFromKeyCode(keyCode);
            SelectRowByRomOffset(newRomOffset);
        }

        private int CalcNewRomOffsetFromKeyCode(Keys keyCode)
        {
            var delta = GetOffsetDeltaFromKeycode(keyCode);
            return CalcNewRomOffsetAdjustByDelta(delta);
        }

        private int CalcNewRomOffsetAdjustByDelta(int delta) => 
            ClampRomOffsetToDataBounds(SelectedRowRomOffset + delta);

        private int ClampRomOffsetToDataBounds(int offset) => 
            Util.ClampIndex(offset, Data.GetRomSize());

        #endregion

        private void Table_KeyDown(object sender, KeyEventArgs e)
        {
            if (!IsDataValid())
                return;

            switch (e.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.PageUp:
                case Keys.PageDown:
                case Keys.Home:
                case Keys.End:
                    AdjustSelectedOffsetByKeyCode(e.KeyCode);
                    e.Handled = true;
                    break;

                case Keys.Left:
                case Keys.Right:
                    AdjustSelectedColumnByKeyCode(e.KeyCode);
                    e.Handled = true;
                    break;

                case Keys.L:
                    BeginEditingSelectionLabel();
                    e.Handled = true;
                    break;
                case Keys.B:
                    BeginEditingSelectedRowProperty(nameof(RomByteDataGridRow.DataBank));
                    e.Handled = true;
                    break;
                case Keys.D:
                    BeginEditingSelectedRowProperty(nameof(RomByteDataGridRow.DirectPage));
                    e.Handled = true;
                    break;
                case Keys.C:
                    BeginEditingSelectionLabel();
                    e.Handled = true;
                    break;
            }

            // InvalidateTable(); // may not need it anymore.
        }

        private void DataGridEditorControl_Load(object? sender, EventArgs e)
        {
            GuiUtil.EnableDoubleBuffering(typeof(DataGridView), Table);
        }
    }
}
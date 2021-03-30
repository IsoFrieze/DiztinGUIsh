#define PROFILING

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;
using DiztinGUIsh.util;
using UserControl = System.Windows.Forms.UserControl;

// eventually, see if we can get this class to not directly contain references to "RomByteDataGridRow"
// so that it can be generically used to format whatever data we want to throw at it

namespace DiztinGUIsh.window2
{
    public partial class DataGridEditorControl : UserControl, IBytesGridViewer<RomByteData>
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
                RecreateTableAndData();
            }
        }

        private List<RomByteData> dataSource;

        public List<RomByteData> DataSource
        {
            get => dataSource;
            set
            {
                dataSource = value;
                RecreateTableAndData();
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

            // note: enabling is REALLY EXPENSIVE
            Table.AutoGenerateColumns = false;

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

            Table.CellValueNeeded += table_CellValueNeeded;
            Table.CellValuePushed += table_CellValuePushed;

            // Table.MouseDown += table_MouseDown;
            // Table.MouseWheel += table_MouseWheel; // don't really need.

            Table.CellPainting += table_CellPainting;
            Table.CurrentCellChanged += TableOnCurrentCellChanged;
        }

        // private void table_MouseWheel(object? sender, MouseEventArgs e) => AdjustSelectedOffsetByDelta(e.Delta / 0x18);

        #endregion

        #region DataBinding

        private bool IsDataValid() => Data?.GetRomSize() > 0 && dataSource?.Count > 0;

        private void RecreateTableAndData()
        {
#if PROFILING
            using var profilerSnapshot = new ProfilerDotTrace.CaptureSnapshot(
                shouldSkip: DataController == null || dataSource == null
            );
#endif

            // note: DataGridView performance is .... rough. Follow some careful guidelines when
            // making any major changes.
            // https://10tec.com/articles/why-datagridview-slow.aspx

            void Performance_DisableDataGridUpdating()
            {
                // perf: don't let any kind of resizing happen during loading, slow.
                /*Table.AutoGenerateColumns = false;
                Table.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
                Table.RowHeadersVisible = false;
                Table.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
                Table.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
                Table.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
                Table.AllowUserToResizeColumns = false;
                Table.AllowUserToResizeRows = false;
                Table.RowTemplate.Resizable = DataGridViewTriState.False;
                Table.Visible = false;
                Table.ColumnHeadersVisible = false;

                Table.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
                Table.RowHeadersVisible = false;*/

                Table.Visible = false;
                GuiUtil.SendMessage(Table.Handle, GuiUtil.WM_SETREDRAW, false, 0);
                SuspendLayout();
                Table.SuspendLayout();
            }

            void Performance_EnableDataGridUpdating()
            {
                //Table.AllowUserToResizeColumns = true;
                //Table.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.ColumnHeader;
                Table.Visible = true;
                // Table.RowHeadersVisible = true;
                
                GuiUtil.SendMessage(Table.Handle, GuiUtil.WM_SETREDRAW, true, 0);
                Table.ResumeLayout();
                ResumeLayout();
                
                Table.Refresh();
            }

            Performance_DisableDataGridUpdating();

            ClearTableData();
            RecreateFromNewData();

            Performance_EnableDataGridUpdating();
        }
        
        private void ClearTableData()
        {
            cachedRows = null;
            Table.RowCount = 0;
        }

        private void RecreateFromNewData()
        {
            if (DataController == null || dataSource == null) 
                return;

            // databinding approach. awesome, but it's too slow for us.
            /*var dataGridView1BindingSource = new BindingSource
            {
                DataSource = DataSource
            };
            Table.DataSource = dataGridView1BindingSource;*/

            RecreateColumns();

            cachedRows = DataSubsetWithSelection.Create(Data, dataSource, this);
            cachedRows.Data = Data;
            cachedRows.RomBytes = dataSource;
            cachedRows.SetViewTo(0, NumRowsToDisplay);
            cachedRows.SelectedLargeIndex = 0;
            
            Table.RowCount = cachedRows.RowCount;
        }
        
        // TODO: don't hardcode table size. calculate from width/height.
        public const int NumRowsToDisplay = 20;

        public void ForceTableRedraw() => Table.Invalidate();

        #endregion

        #region RowColumnAccess

        public RomByteData SelectedRomByteRow =>
            Table.CurrentRow == null
                ? null
                : GetRowValue(Table.CurrentRow.Index)?.RomByte;

        private RomByteDataGridRow GetRowValue(int row) => 
            cachedRows?.TryGetRow(row);

        public int SelectedRowRomOffset => SelectedRomByteRow?.Offset ?? -1;

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

            SelectRow(GetRowOffsetFromLargeOffset(romOffset));
        }

        private int GetRowOffsetFromLargeOffset(int romOffset) => 
            cachedRows?.GetRowOffsetFromLargeOffset(romOffset) ?? -1;

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

        private void SelectCell(int row, int col) => 
            SelectCell(Table.Rows[row].Cells[col]);
        private void SelectCell(int row, string columnName) => 
            SelectCell(Table.Rows[row].Cells[columnName]);

        private void SelectCell(DataGridViewCell cellToSelect)
        {
            DizUIGridTrace.Log.SelectCell_Start();
            try
            {
                cachedRows.SelectRow(cellToSelect.RowIndex);
                Table.CurrentCell = cellToSelect;
                
                ForceTableRedraw();
            }
            finally
            {
                DizUIGridTrace.Log.SelectCell_Stop();
            }
        }

        #endregion

        #region KeyboardHandler

        private static int GetOffsetDeltaFromKeycode(Keys keyCode)
        {
            const int one = 0x01;
            const int small = 0x10;
            const int large = 0x80;

            var sign = keyCode is not Keys.Home and not Keys.PageUp and not Keys.Up ? 1 : -1;
            var magnitude = 0;
            switch (keyCode)
            {
                case Keys.Up:
                case Keys.Down:
                    magnitude = one;
                    break;
                case Keys.PageUp:
                case Keys.PageDown:
                    magnitude = small;
                    break;
                case Keys.Home:
                case Keys.End:
                    magnitude = large;
                    break;
            }

            return sign * magnitude;
        }

        #endregion

        #region Formatting

        private void table_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            DizUIGridTrace.Log.CellPainting_Start();
            try
            {
                var valid = IsDataValid() && e.RowIndex != -1 && e.ColumnIndex != -1;
                if (!valid)
                    return;

                var romByteAtRow = GetRowValue(e.RowIndex);
                var colHeaderDataProperty = GetColumnHeaderDataProperty(e);

                if (romByteAtRow?.RomByte == null || string.IsNullOrEmpty(colHeaderDataProperty))
                    return;

                romByteAtRow.SetStyleForCell(colHeaderDataProperty, e.CellStyle);
            }
            finally
            {
                DizUIGridTrace.Log.CellPainting_Stop();
            }
        }

        private void RecreateColumns()
        {
            Table.Columns.Clear();

            foreach (var property in typeof(RomByteDataGridRow).GetProperties())
            {
                if (!RomByteDataGridRow.IsPropertyBrowsable(property.Name))
                    continue;

                var newCol = new DataGridViewTextBoxColumn()
                {
                    DataPropertyName = property.Name,
                    Resizable = DataGridViewTriState.False,
                    HeaderText = RomByteDataGridRow.GetColumnDisplayName(property.Name),
                    ReadOnly = RomByteDataGridRow.GetColumnIsReadOnly(property.Name),

                    // PERF: if enabled, during load, resizing will be super-slow
                    // this can be re-enabled after load.
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                };

                Table.Columns.Add(newCol);
            }

            foreach (DataGridViewTextBoxColumn col in Table.Columns)
            {
                RomByteDataGridRowFormatting.ApplyFormatting(col);
            }
        }

        #endregion

        #region Editing

        public void BeginEditingSelectionComment() =>
            BeginEditingSelectedRowProperty(nameof(RomByteDataGridRow.Comment));

        public void BeginEditingSelectionLabel() =>
            BeginEditingSelectedRowProperty(nameof(RomByteDataGridRow.Label));

        public event IBytesGridViewer<RomByteData>.SelectedOffsetChange SelectedOffsetChanged;

        private void AdjustSelectedColumnByKeyCode(Keys keyCode)
        {
            var adjustBy = keyCode switch {Keys.Left => -1, Keys.Right => 1, _ => 0};
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
                new IBytesGridViewer<RomByteData>.SelectedOffsetChangedEventArgs {Row = selectedRomByteRow});
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

            ForceTableRedraw();
        }

        // stores just the current Rom bytes in view (subset of larger data source)
        private DataSubsetWithSelection cachedRows;

        private int GetRomAddressAtRow(int largeIndex) => 
            cachedRows?.GetRowOffsetFromLargeOffset(largeIndex) ?? -1;

        private void table_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            DizUIGridTrace.Log.CellValueNeeded_Start();
            try
            {
                var romOffset = GetRomAddressAtRow(e.RowIndex);
                if (Data == null || romOffset >= Data.GetRomSize())
                    return;

                e.Value = CalculateCellValueFor(romOffset, e.ColumnIndex);
            }
            finally
            {
                DizUIGridTrace.Log.CellValueNeeded_Stop();
            }
        }

        private object CalculateCellValueFor(int romOffset, int colIndex)
        {
            var rowOffset = GetRowOffsetFromLargeOffset(romOffset);
            var romByteDataGridRow = GetRowValue(rowOffset);
            return GetPropertyAtColumn(romByteDataGridRow, colIndex);
        }

        private object GetPropertyAtColumn(RomByteDataGridRow romByteGridRow, int colIndex)
        {
            var headerName = GetColumnHeaderDataProperty(colIndex);
            var propertyValue = typeof(RomByteDataGridRow).GetProperty(headerName)?.GetValue(romByteGridRow);
            return propertyValue;
        }

        private void SetPropertyAtColumn(RomByteDataGridRow romByteGridRow, int colIndex, object value)
        {
            var headerName = GetColumnHeaderDataProperty(colIndex);
            typeof(RomByteDataGridRow).GetProperty(headerName)?.SetValue(romByteGridRow, value);
        }

        private void table_CellValuePushed(object sender, DataGridViewCellValueEventArgs e)
        {
            var romByteDataGridRow = GetRowValue(e.RowIndex);
            if (romByteDataGridRow == null)
                return;

            SetPropertyAtColumn(romByteDataGridRow, e.ColumnIndex, e.Value as string);

            Table.InvalidateRow(e.RowIndex);
        }

        private void DataGridEditorControl_Load(object? sender, EventArgs e) =>
            GuiUtil.EnableDoubleBuffering(typeof(DataGridView), Table);
    }
}
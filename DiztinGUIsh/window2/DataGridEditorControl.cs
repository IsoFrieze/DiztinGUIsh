#define PROFILING

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;
using DiztinGUIsh.util;
using UserControl = System.Windows.Forms.UserControl;

// eventually, see if we can get this class to not directly contain references to "RomByteDataGridRow"
// so that it can be generically used to format whatever data we want to throw at it
//
// Keep ".Data" out of here if we can.  this class shouldn't know anything about Roms or data or whatever.

namespace DiztinGUIsh.window2
{
    public partial class DataGridEditorControl : UserControl, IBytesGridViewer<RomByteData>
    {
        #region Properties

        public Util.NumberBase NumberBaseToShow { get; set; } = Util.NumberBase.Hexadecimal;

        private IBytesGridViewerDataController<RomByteData> dataController;

        public IBytesGridViewerDataController<RomByteData> DataController
        {
            get => dataController;
            set
            {
                if (DataController != null)
                    DataController.PropertyChanged -= ControllerPropertyChanged;

                dataController = value;
                
                if (DataController != null)
                    DataController.PropertyChanged += ControllerPropertyChanged;

                RecreateTableAndData();
            }
        }

        private void ControllerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            var rowsChanged = false;

            switch (e.PropertyName)
            {
                case nameof(DataSubsetWithSelection.SelectedLargeIndex):
                    if (DataController.Rows.SelectedRowIndex != -1)
                        SelectRow(DataController.Rows.SelectedRowIndex);
                    break;

                case nameof(DataSubsetWithSelection.StartingRowLargeIndex):
                case nameof(DataSubsetWithSelection.RowCount):
                case nameof(ByteViewerDataBindingGridController<RomByteData>.Rows):
                    rowsChanged = true;
                    break;
            }

            if (rowsChanged)
                ForceTableRedraw();
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

            // note: enabling is REALLY EXPENSIVE if we have lots of rows.
            Table.AutoGenerateColumns = false;

            Table.ScrollBars = ScrollBars.Horizontal;

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
            Table.MouseWheel += table_MouseWheel;

            Table.CellPainting += table_CellPainting;
            Table.CurrentCellChanged += TableOnCurrentCellChanged;
        }

        private void table_MouseWheel(object? sender, MouseEventArgs e) =>
            AdjustSelectedLargeIndexByDelta(e.Delta / -120);

        #endregion

        #region DataBinding

        private bool IsDataValid() => dataSource?.Count > 0;

        private void RecreateTableAndData()
        {
#if PROFILING
            using var profilerSnapshot = new ProfilerDotTrace.CaptureSnapshot(
                shouldSkip: DataController == null || dataSource == null
            );
#endif
            Table.RowCount = 0;

            if (DataController == null || dataSource == null)
                return;

            RecreateColumns();
            RecreateRows();

            ForceTableRedraw();
        }

        private void RecreateRows()
        {
            // causes more rows to be asked for in cellValueNeeded fn
            Table.RowCount = DataController?.Rows?.RowCount ?? 0;
        }
        
        public int TargetNumberOfRowsToShow => 
            (Table.Height - Table.ColumnHeadersHeight) / Table.RowTemplate.Height;

        public void ForceTableRedraw() => Table.Invalidate();

        #endregion

        #region RowColumnAccess

        public RomByteData SelectedRomByte => DataController?.Rows?.SelectedRow?.RomByte;

        private RomByteDataGridRow GetValueAtRowIndex(int row)
        {
            var outputRows = DataController?.Rows?.OutputRows;
            if (outputRows == null || outputRows.Count == 0 || row < 0 || row >= outputRows.Count)
                throw new IndexOutOfRangeException("GetRowValue() row out of range, or no cached outputrows ready");

            return outputRows[row];
        }

        // this should go somewhere else, outside this grid class.
        //
        // THIS IS DIFFERENT THAN A "LARGE INDEX"
        // "rom offset" is an index into .Data
        // "large offset" is an index into .dataSource
        // public int SelectedRowRomOffset => SelectedRomByteRow?.Offset ?? -1;
        /*private void SelectRowBySnesOffset(int newSnesOffsetToSelect)
        {
            // right now, rows in table are 1:1 with RomBytes.
            // in the future, we might cache a window, and this function will need to be modified to deal with that.

            var romOffset = Data.ConvertSnesToPc(newSnesOffsetToSelect);
            SelectRowByRomOffset(romOffset);
        }*/

        private void SelectRowByLargeIndex(int largeIndex)
        {
            if (!IsLargeIndexValid(largeIndex))
                throw new Exception("LargeIndex out of range");

            DataController.Rows.SelectedLargeIndex = largeIndex;
        }

        private int GetRowIndexFromLargeIndex(int largeIndex) =>
            DataController?.Rows?.GetRowIndexFromLargeOffset(largeIndex) ?? -1;

        private int SelectedTableRow => Table.CurrentCell?.RowIndex ?? -1;
        private int SelectedTableCol => Table.CurrentCell?.ColumnIndex ?? -1;

        // Corresponds to the name of properties in RomByteData,
        // NOT what you see on the screen as the column heading text
        private string GetColumnHeaderDataProperty(DataGridViewCellPaintingEventArgs e) =>
            GetColumnHeaderDataProperty(e?.ColumnIndex ?? -1);

        private string GetColumnHeaderDataProperty(int colIndex) =>
            GetColumn(colIndex)?.DataPropertyName;

        private DataGridViewColumn GetColumn(int colIndex) =>
            colIndex >= 0 && colIndex < Table.Columns.Count ? Table.Columns[colIndex] : null;

        private void SelectColumn(int columnIndex) =>
            SelectCell(SelectedTableRow, columnIndex);

        private void SelectColumn(string columnName) =>
            SelectCell(SelectedTableRow, columnName);

        private void SelectColumnClamped(int adjustBy) =>
            SelectColumn(Util.ClampIndex(SelectedTableCol + adjustBy, Table.ColumnCount));

        public void SelectRow(int rowIndex)
        {
            if (!IsValidRowIndex(rowIndex))
                throw new ArgumentOutOfRangeException(nameof(rowIndex));
            
            SelectCell(rowIndex, SelectedTableCol);
        }

        private void SelectCell(int row, int col) =>
            SelectCell(Table.Rows[row].Cells[col]);

        private void SelectCell(int row, string columnName) =>
            SelectCell(Table.Rows[row].Cells[columnName]);

        private void SelectCell(DataGridViewCell cellToSelect)
        {
            DizUIGridTrace.Log.SelectCell_Start();
            try
            {
                // important so we don't accidentally recurse during updates
                if (cellToSelect.RowIndex == SelectedTableRow)
                    return;

                // note: complex. dispatches lots of other events on set
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
                var valid = IsDataValid() && IsValidRowIndex(e.RowIndex) && e.ColumnIndex != -1;
                if (!valid)
                    return;

                var romByteAtRow = GetValueAtRowIndex(e.RowIndex);
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
            var selectedRomByteRow = SelectedRomByte;
            if (selectedRomByteRow == null)
                return;

            SelectedOffsetChanged?.Invoke(this,
                new IBytesGridViewer<RomByteData>.SelectedOffsetChangedEventArgs
                {
                    Row = selectedRomByteRow,
                    RowIndex = SelectedTableRow,
                });
        }

        private void BeginEditingSelectedRowProperty(string propertyName)
        {
            SelectColumn(propertyName);
            Table.BeginEdit(true);
        }

        public void AdjustSelectedLargeIndexByDelta(int delta)
        {
            var newLargeIndex = CalcNewLargeIndexAdjustByDelta(delta);
            SelectRowByLargeIndex(newLargeIndex);
        }

        private void AdjustSelectedOffsetByKeyCode(Keys keyCode)
        {
            var newLargeIndex = CalcNewLargeIndexFromKeyCode(keyCode);
            SelectRowByLargeIndex(newLargeIndex);
        }

        private int CalcNewLargeIndexFromKeyCode(Keys keyCode)
        {
            var delta = GetOffsetDeltaFromKeycode(keyCode);
            return CalcNewLargeIndexAdjustByDelta(delta);
        }

        public int SelectedLargeIndex => DataController.Rows.SelectedLargeIndex;

        private int CalcNewLargeIndexAdjustByDelta(int delta) =>
            ClampLargeIndexToDataBounds(SelectedLargeIndex + delta);

        private int ClampLargeIndexToDataBounds(int largeIndex) =>
            Util.ClampIndex(largeIndex, dataSource.Count);

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

        private bool IsLargeIndexValid(int largeIndex) =>
            dataSource != null && largeIndex >= 0 && largeIndex < dataSource.Count;

        private void table_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            DizUIGridTrace.Log.CellValueNeeded_Start();
            try
            {
                if (!IsValidRowIndex(e.RowIndex))
                    return;
                
                var obj = CalculateCellValueForRowIndex(e.RowIndex, e.ColumnIndex);
                if (obj == null)
                    return;

                e.Value = obj;
            }
            finally
            {
                DizUIGridTrace.Log.CellValueNeeded_Stop();
            }
        }

        private object CalculateCellValueForLargeIndex(int largeIndex, int colIndex)
        {
            var rowIndex = GetRowIndexFromLargeIndex(largeIndex);
            return rowIndex == -1
                ? null
                : CalculateCellValueForRowIndex(rowIndex, colIndex);
        }

        private object CalculateCellValueForRowIndex(int rowIndex, int colIndex)
        {
            if (!IsValidRowIndex(rowIndex))
                throw new ArgumentOutOfRangeException(nameof(rowIndex));

            var romByteDataGridRow = GetValueAtRowIndex(rowIndex);
            return GetPropertyAtColumn(romByteDataGridRow, colIndex);
        }

        private bool IsValidRowIndex(int rowIndex) =>
            rowIndex >= 0 && rowIndex < DataController?.Rows?.RowCount;

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
            var romByteDataGridRow = GetValueAtRowIndex(e.RowIndex);
            if (romByteDataGridRow == null)
                return;

            SetPropertyAtColumn(romByteDataGridRow, e.ColumnIndex, e.Value as string);

            Table.InvalidateRow(e.RowIndex);
        }

        private void DataGridEditorControl_Load(object? sender, EventArgs e) =>
            GuiUtil.EnableDoubleBuffering(typeof(DataGridView), Table);

        private void DataGridEditorControl_SizeChanged(object sender, EventArgs e)
        {
            Table.RowCount = TargetNumberOfRowsToShow;
            DataController?.MatchCachedRowsToView();
            ForceTableRedraw();
        }
    }
}
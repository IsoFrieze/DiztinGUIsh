using System.Linq;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;
using DiztinGUIsh.util;
using Equin.ApplicationFramework;
using UserControl = System.Windows.Forms.UserControl;

namespace DiztinGUIsh.window2
{
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

            GuiUtil.EnableDoubleBuffering(typeof(DataGridView), dataGridView1);
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
    }
}
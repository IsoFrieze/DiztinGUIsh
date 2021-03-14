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
        
        public Data Data => Controller?.Data;
        public Util.NumberBase DataGridNumberBase { get; set; } = Util.NumberBase.Hexadecimal;

        
        private IController controller;

        public IController Controller
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
    }
}
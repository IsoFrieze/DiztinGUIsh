using System.Windows.Forms;

namespace DiztinGUIsh.window2
{
    public partial class DataGridEditorControl : UserControl, IBytesViewer
    {
        private IBytesViewerController controller;

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
        }
    }

}
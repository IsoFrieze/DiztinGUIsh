using System.Diagnostics;
using System.Windows.Forms;

namespace DiztinGUIsh.window2
{
    public partial class DataGridEditorForm : Form, IBytesViewer
    {
        private IBytesViewerController FormController;
        private BytesViewerController DataGridController = new();
        
        public DataGridEditorForm(IBytesViewerController formController)
        {
            InitializeComponent();
            Debug.Assert(formController?.BindingList != null);
            FormController = formController;
            
            DataGridController.BindingList = FormController.BindingList;
            dataGridEditorControl1.Controller = DataGridController;
        }

        private void DG_Load(object sender, System.EventArgs e)
        {
            
        }
    }
}
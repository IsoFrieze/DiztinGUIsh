using System.Diagnostics;
using System.Windows.Forms;
using Diz.Core.util;

namespace DiztinGUIsh.window2
{
    public partial class DataGridEditorForm : Form, IBytesViewer
    {
        private IBytesViewerController FormController;
        private readonly BytesViewerController DataGridController;

        public DataGridEditorForm(IBytesViewerController formController)
        {
            InitializeComponent();
            FormController = formController;
            DataGridController = new BytesViewerController()
            {
                Data = formController.Data
            };
            dataGridEditorControl1.Controller = DataGridController;
        }

        private void DG_Load(object sender, System.EventArgs e)
        {
            
        }
    }
}
using System.Windows.Forms;

namespace DiztinGUIsh.window2
{
    public partial class DataGridEditorForm : Form, IBytesViewer
    {
        private IBytesViewerController FormController;
        private readonly BytesViewerController DataGridController;

        public DataGridEditorForm(IBytesViewerController formController)
        {
            InitializeComponent();
            
            // 
            // MainWindow itself, old designer stuff migrated. keep or kill
            // 
            // this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            // this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            // this.ClientSize = new System.Drawing.Size(930, 538);
            // this.MinimumSize = new System.Drawing.Size(780, 196);
            
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
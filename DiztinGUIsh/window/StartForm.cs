using System;
using System.Diagnostics;
using System.Windows.Forms;
using Diz.Controllers.interfaces;

namespace DiztinGUIsh.window
{
    public partial class StartForm : Form, IStartFormViewer
    {
        public StartForm()
        {
            InitializeComponent();

            // TODO
            /*var projectsBs = new BindingSource(ProjectsController.)
            GuiUtil.BindListControl(comboBox1, DizApplication.App.ProjectsController, nameof(ProjectsController.Projects), bs));
            GuiUtil.BindListControlToEnum<RomMapMode>(comboBox1, );*/
        }

        public string PromptForOpenFile()
        {
            var openProjectFile = new OpenFileDialog
            {
                Filter = "DiztinGUIsh Project Files|*.diz;*.dizraw|All Files|*.*",
            };
            return openProjectFile.ShowDialog() != DialogResult.OK ? "" : openProjectFile.FileName;
        }

        public event EventHandler<ProjectOpenEventArgs> ProjectOpenRequested;

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var filename = PromptForOpenFile();
            if (string.IsNullOrEmpty(filename))
                return;

            ProjectOpenRequested?.Invoke(this, new ProjectOpenEventArgs {
                Filename = filename,
            });
        }

        private void newViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ProjectOpenRequested?.Invoke(this, new ProjectOpenEventArgs {
                OpenLast = true
            });
        }

        private void button1_Click(object sender, EventArgs e)
        {
            
        }

        private void btnCloseSelectedProject_Click(object sender, EventArgs e)
        {

        }

        private void StartForm_Load(object sender, EventArgs e)
        {
        }
    }
}
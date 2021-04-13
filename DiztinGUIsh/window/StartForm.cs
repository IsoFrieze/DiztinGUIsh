using System;
using System.Windows.Forms;
using Diz.Core.util;
using DiztinGUIsh.util;

namespace DiztinGUIsh.window2
{
    public partial class StartForm : Form, IFormViewer
    {
        public StartFormController Controller { get; set; }
        
        public StartForm()
        {
            InitializeComponent();

            // HACK. open last file.
            //if (!string.IsNullOrEmpty(Settings.Default.LastOpenedFile))
            //    Controller.OpenFileWithNewView(Settings.Default.LastOpenedFile);

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

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var filename = PromptForOpenFile();
            if (string.IsNullOrEmpty(filename))
                return;

            Controller.OpenFileWithNewView(filename);
        }

        private void newViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Controller.OpenNewViewOfLastLoadedProject();
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
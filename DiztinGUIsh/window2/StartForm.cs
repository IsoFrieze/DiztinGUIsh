using System;
using System.Windows.Forms;
using DiztinGUIsh.Properties;

namespace DiztinGUIsh.window2
{
    public class StartFormDataBindingController : DataBindingController
    {
        public DizApplication DizApplication { get; init; }

        public void OpenFileWithNewView(string filename)
        {
            DizApplication.OpenProjectFileWithNewView(filename);
        }

        protected override void DataBind()
        {
            
        }

        public void OpenNewViewOfLastLoadedProject()
        {
            DizApplication.OpenNewViewOfLastLoadedProject();
        }
    }
    
    public partial class StartForm : Form, IViewer
    {
        public StartFormDataBindingController DataBindingController { get; set; }
        
        public StartForm()
        {
            InitializeComponent();
            
            // HACK. open last file.
            //if (!string.IsNullOrEmpty(Settings.Default.LastOpenedFile))
            //    DataBindingController.OpenFileWithNewView(Settings.Default.LastOpenedFile);
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

            DataBindingController.OpenFileWithNewView(filename);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            
        }

        private void newViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DataBindingController.OpenNewViewOfLastLoadedProject();
        }

        private void newViewBankC0ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            
        }

        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }
    }
}
using System;
using System.Windows.Forms;

namespace DiztinGUIsh.window2
{
    public class StartFormController : IFormController
    {
        private IFormViewer view;

        public IFormViewer FormView
        {
            get => view;
            set
            {
                view = value;
                view.Closed += ViewOnClosed;
            }
        }
        
        IViewer IController.View => FormView;

        private void ViewOnClosed(object? sender, EventArgs e) => Closed?.Invoke(sender, e);

        public event EventHandler Closed;
        
        public void OpenFileWithNewView(string filename)
        {
            DizApplication.App.OpenProjectFileWithNewView(filename);
        }

        public void OpenNewViewOfLastLoadedProject()
        {
            DizApplication.App.OpenNewViewOfLastLoadedProject();
        }
    }
    
    public partial class StartForm : Form, IFormViewer
    {
        public StartFormController Controller { get; set; }
        
        public StartForm()
        {
            InitializeComponent();
            
            // HACK. open last file.
            //if (!string.IsNullOrEmpty(Settings.Default.LastOpenedFile))
            //    Controller.OpenFileWithNewView(Settings.Default.LastOpenedFile);
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
    }
}
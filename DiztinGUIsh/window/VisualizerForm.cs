using System.Windows.Forms;
using Diz.Core.model;
using DiztinGUIsh.window.usercontrols;

namespace DiztinGUIsh.window
{
    public partial class VisualizerForm : Form
    {
        private readonly MainWindow mainWindow;
        private Project project;

        public Project Project
        {
            get => project;
            set
            {
                project = value;
                romFullVisualizer1.Project = project;
            }
        }

        public VisualizerForm(MainWindow window)
        {
            mainWindow = window;
            InitializeComponent();
        }

        private void VisualizerForm_Load(object sender, System.EventArgs e)
        {
            mainWindow.ProjectController.ProjectChanged += ProjectController_ProjectChanged;

            // hack to make room for the scrollbar
            // I wish docking dealt with this, or maybe I set it up wrong...
            Width = romFullVisualizer1.Width + 40;

            romFullVisualizer1.Project = mainWindow.Project;
        }

        private void ProjectController_ProjectChanged(object sender, controller.ProjectController.ProjectChangedEventArgs e)
        {
            this.Project = e.Project;
        }

        private void VisualizerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.UserClosing) return;
            e.Cancel = true;
            Hide();
        }
    }
}
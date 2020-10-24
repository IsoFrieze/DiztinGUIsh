using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;

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
                romImage1.Project = project;
            }
        }

        public VisualizerForm(MainWindow window)
        {
            mainWindow = window;
            InitializeComponent();
        }

        private void VisualizerForm_Load(object sender, System.EventArgs e)
        {
            // hack to make room for the scrollbar
            // I wish docking dealt with this, or maybe I set it up wrong...
            Width = romImage1.Width + 40;
        }

        private void VisualizerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.UserClosing) return;
            e.Cancel = true;
            Hide();
        }
    }
}
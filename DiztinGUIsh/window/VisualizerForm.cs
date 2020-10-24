using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;
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
                romImage1.Project = project;
            }
        }

        public VisualizerForm(MainWindow window)
        {
            mainWindow = window;
            InitializeComponent();

            romImage1.RedrawOccurred += RomImage1_RedrawOccurred;
        }

        private void RomImage1_RedrawOccurred(object sender, System.EventArgs e)
        {
            if (!(sender is RomImage romImage))
                return;

            romImage1.Width = romImage.ROMVisual.Bitmap.Width;
            romImage1.Height = romImage.ROMVisual.Bitmap.Height;
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
using System.Collections.Generic;
using System.Windows.Forms;
using Diz.Core.util;
using DiztinGUIsh.util;

namespace DiztinGUIsh.window
{
    public partial class VisualizerForm : Form
    {
        private readonly MainWindow mainWindow;
        private readonly RomVisual romVisual = new RomVisual();

        public VisualizerForm(MainWindow window)
        {
            mainWindow = window;
            InitializeComponent();
        }

        private void VisualizerForm_Load(object sender, System.EventArgs e)
        {
            romVisual.Project = mainWindow.Project;
            pictureBox1.Image = romVisual.Bitmap;

            romVisual.ImageDataUpdated += RomVisual_ImageDataUpdated;
            romVisual.MarkedDirty += RomVisual_MarkedDirty;

            Width = pictureBox1.Width + 40;
        }

        private void RomVisual_MarkedDirty(object sender, System.EventArgs e)
        {
            pictureBox1.Invalidate();
        }

        private void RomVisual_ImageDataUpdated(object sender, System.EventArgs e)
        {
            pictureBox1.Refresh();
            Application.DoEvents();

            // ugly hack city.
            pictureBox1.Image = null;
            pictureBox1.Image = romVisual.Bitmap;
        }

        private void VisualizerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.UserClosing) return;
            e.Cancel = true;
            Hide();
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            romVisual?.Refresh();
        }
    }
}
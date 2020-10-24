using System.Drawing;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;

namespace DiztinGUIsh.window.usercontrols
{
    public partial class RomImage : UserControl
    {
        private readonly RomVisual romVisual = new RomVisual();
        private Project project;

        public Project Project
        {
            get => project;
            set
            {
                project = value;
                romVisual.Project = project;
            }
        }

        public RomImage()
        {
            InitializeComponent();
        }
        private void RomImage_Load(object sender, System.EventArgs e)
        {
            // if there's a reason to track ROM byte changes, hook in here
            // romVisual.MarkedDirty += RomVisual_MarkedDirty;
        }

        private void RomImage_Paint(object sender, PaintEventArgs e)
        {
            Redraw(e.Graphics);
        }

        private void Redraw(Graphics graphics = null)
        {
            if (romVisual?.Bitmap == null)
                return;

            graphics ??= CreateGraphics();

            var width = romVisual.Bitmap.Width;
            var height = romVisual.Bitmap.Height;
            graphics.DrawImage(romVisual.Bitmap, 0, 0, width, height);
        }

        private void RedrawIfNeeded()
        {
            if (!Visible || !romVisual.IsDirty)
                return;

            romVisual.Refresh();
            Redraw();
        }

        private void timer1_Tick(object sender, System.EventArgs e)
        {
            RedrawIfNeeded();
        }
    }
}

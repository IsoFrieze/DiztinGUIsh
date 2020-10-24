using System;
using System.Drawing;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;

namespace DiztinGUIsh.window.usercontrols
{
    public partial class RomImage : UserControl
    {
        public event EventHandler RedrawOccurred;

        public RomVisual ROMVisual { get; } = new RomVisual();
        private Project project;

        public Project Project
        {
            get => project;
            set
            {
                project = value;
                ROMVisual.Project = project;
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
            if (ROMVisual?.Bitmap == null)
                return;

            graphics ??= CreateGraphics();

            var width = ROMVisual.Bitmap.Width;
            var height = ROMVisual.Bitmap.Height;
            graphics.DrawImage(ROMVisual.Bitmap, 0, 0, width, height);

            OnRedrawOccurred();
        }

        private void RedrawIfNeeded()
        {
            if (!Visible || !ROMVisual.IsDirty)
                return;

            ROMVisual.Refresh();
            Redraw();
        }

        private void timer1_Tick(object sender, System.EventArgs e)
        {
            RedrawIfNeeded();
        }

        protected virtual void OnRedrawOccurred()
        {
            RedrawOccurred?.Invoke(this, EventArgs.Empty);
        }
    }
}

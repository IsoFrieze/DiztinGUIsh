using System;
using System.Drawing;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;

// this usercontrol is JUST to show the raw image data (no markup, formatting, etc)

namespace DiztinGUIsh.window.usercontrols
{
    public partial class RomImage : UserControl
    {
        public RomVisual RomVisual { get; } = new RomVisual();

        public Project Project
        {
            get => RomVisual.Project;
            set => RomVisual.Project = value;
        }

        public RomImage()
        {
            InitializeComponent();
        }
        private void RomImage_Load(object sender, System.EventArgs e)
        {
            // if there's a reason to track ROM byte changes, hook in here
            // romVisual.MarkedDirty += RomVisual_MarkedDirty;

            UpdateDimensions();
        }

        private void RomImage_Paint(object sender, PaintEventArgs e)
        {
            Redraw(e.Graphics);
        }

        private void Redraw(Graphics graphics = null)
        {
            if (RomVisual?.Bitmap == null || updatingDimensions)
                return;

            graphics ??= CreateGraphics();

            var width = RomVisual.Bitmap.Width;
            var height = RomVisual.Bitmap.Height;
            graphics.DrawImage(RomVisual.Bitmap, 0, 0, width, height);
        }

        private void RedrawIfNeeded()
        {
            if (!Visible || !RomVisual.IsDirty)
                return;

            RomVisual.Refresh();
            Redraw();
        }

        private void timer1_Tick(object sender, System.EventArgs e)
        {
            RedrawIfNeeded();
        }

        private bool updatingDimensions = false;

        private void UpdateDimensions()
        {
            Width = RomVisual.Width;
            Height = RomVisual.Height;
        }
    }
}

using System;
using System.Windows.Forms;
using Diz.Core.util;

namespace DiztinGUIsh.window2
{
    public partial class DataGridEditorForm : Form, IBytesFormViewer
    {
        // the class controlling US
        public IController Controller { get; set; }


        // a class we create that controls just the data grid usercontrol we host
        private IBytesGridViewerController<RomByteDataGridRow> DataGridController;
        public DataGridEditorForm(IController formController)
        {
            Controller = formController;
            InitializeComponent();
            Init();
        }

        private void Init()
        {
            // 
            // MainWindow itself, old designer stuff migrated. keep or kill
            // 
            // this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            // this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            // this.ClientSize = new System.Drawing.Size(930, 538);
            // this.MinimumSize = new System.Drawing.Size(780, 196);

            DataGridController = new RomByteGridController
            {
                ViewGrid = dataGridEditorControl1,
                Data = Controller.Data,
            };

            dataGridEditorControl1.Controller = DataGridController;
            
            
        }

        private void DG_Load(object sender, System.EventArgs e)
        {
            // test junk
            if (g_timerGoing)
            {
                timer1.Enabled = false;
            }

            g_timerGoing = true;
        }

        private static bool g_timerGoing;

        private void timer1_Tick(object sender, EventArgs e)
        {
            // test junk
            if (Controller?.Data != null)
                Controller.Data.RomBytes[0].DirectPage = Util.ClampIndex(
                    Controller.Data.RomBytes[0].DirectPage + 1, 0xFFFF);
        }
    }
}
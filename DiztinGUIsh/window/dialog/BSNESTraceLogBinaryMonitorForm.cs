using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Diz.Cpu._65816;
using Diz.Import.bsnes.tracelog;
using Diz.Ui.Winforms.util;
using DiztinGUIsh.util;

namespace DiztinGUIsh.window.dialog
{
    // TODO: add controller/view for this.
    //
    // TODO: BSNESTraceLogCapture does a lot of threading. It's decently protected but,
    // while that stuff is running, try and avoid using 'Data' anywhere outside BSNESTraceLogCapture.
    // eventually, if we want to do that we need to retrofit the rest of the app to take advantage of that.
    public partial class BsnesTraceLogBinaryMonitorForm : Form
    {
        private readonly MainWindow mainWindow;
        private BsnesTraceLogCapture capturing;
        private string lastError;

        public BsnesTraceLogBinaryMonitorForm(MainWindow window)
        {
            mainWindow = window;
            InitializeComponent();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            timer1.Enabled = true;
            btnFinish.Enabled = true;
            btnStart.Enabled = false;

            capturing = new BsnesTraceLogCapture();

            Start();
        }

        private async void Start()
        {
            // TODO: error handling is busted here.
            await Task.Run(() => {
                capturing.Run(mainWindow.Project.Data.GetSnesApi());
            }).ContinueWith(task => {
                this.InvokeIfRequired(() => CapturingFinished(task.Exception));
            });
            UpdateUi();
        }

        private void btnFinish_Click(object sender, EventArgs e)
        {
            capturing?.SignalToStop();
            UpdateUi();
        }

        private void CapturingFinished(AggregateException ex)
        {
            if (ex != null) {
                OnError(ex);
            }

            timer1.Enabled = false;
            capturing = null;
            UpdateUi();
        }

        private void timer1_Tick(object sender, EventArgs e) => UpdateUi();

        private void OnError(AggregateException e)
        {
            Console.WriteLine(e.ToString());
            lastError = e.InnerExceptions.Select(ex => ex.Message).Aggregate((line, val) => line += val + "\n");
        }   

        private void BSNESTraceLogBinaryMonitorForm_Load(object sender, EventArgs e) => UpdateUi();
        private void BSNESTraceLogBinaryMonitorForm_Shown(object sender, EventArgs e) => UpdateUi();
    }
}
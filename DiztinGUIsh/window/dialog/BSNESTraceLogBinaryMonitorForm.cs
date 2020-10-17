using System;
using System.Windows.Forms;

namespace DiztinGUIsh.window.dialog
{
    // TODO: add controller/view for this.
    //
    // TODO: BSNESTraceLogCapture does a lot of threading. It's decently protected but,
    // while that stuff is running, try and avoid using 'Data' anywhere outside BSNESTraceLogCapture.
    // eventually, if we want to do that we need to retrofit the rest of the app to take advantage of that.
    public partial class BSNESTraceLogBinaryMonitorForm : Form
    {
        private MainWindow MainWindow;
        private BSNESTraceLogCapture capturing;

        public BSNESTraceLogBinaryMonitorForm(MainWindow window)
        {
            MainWindow = window;
            InitializeComponent();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            timer1.Enabled = true;
            btnFinish.Enabled = true;
            btnStart.Enabled = false;
            
            capturing = new BSNESTraceLogCapture();
            capturing.Finished += CapturingFinished;
            capturing.Error += Capturing_Error;

            capturing.Start(MainWindow.Project.Data);
        }

        private void Capturing_Error(Exception e)
        {
            MessageBox.Show(e.Message, "Worker Error");
        }

        private void btnFinish_Click(object sender, EventArgs e)
        {
            timer1.Enabled = false;

            capturing?.SignalToStop();

            UpdateUI();
        }

        private void CapturingFinished(object sender, EventArgs e)
        {
            UpdateUI();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            var running = capturing?.Running ?? false;
            var finishing = capturing?.Finishing ?? false;

            lblStatus.Text = !running ? "Not running" : finishing ? "Stopping..." : "Running";

            btnFinish.Enabled = !finishing && running;
            btnStart.Enabled = !running;

            if (capturing == null) 
                return;

            lblQueueSize.Text = capturing.QueueLength.ToString();

            // TODO: use databinding
            var stats = capturing.GetStats(); // copy
            lblTotalProcessed.Text = stats.numRomBytesAnalyzed.ToString();
            lblNumberModified.Text = stats.numRomBytesModified.ToString();
            lblModifiedDBs.Text = stats.numDBModified.ToString();
            lblModifiedDPs.Text = stats.numDpModified.ToString();
            lblModifiedFlags.Text = stats.numMarksModified.ToString();
            lblModifiedXFlags.Text = stats.numXFlagsModified.ToString();
            lblModifiedMFlags.Text = stats.numMFlagsModified.ToString();
        }

        private void BSNESTraceLogBinaryMonitorForm_Load(object sender, EventArgs e)
        {

        }
    }
}
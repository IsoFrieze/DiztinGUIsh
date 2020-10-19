using System;
using System.Windows.Forms;
using ByteSizeLib;
using Diz.Core.import;

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

            var (stats, totalQueueBytes) = capturing.GetStats();

            var qItemCount = capturing.QueueLength.ToString();
            var qByteCount = ByteSize.FromBytes(totalQueueBytes).ToString("0.00");

            lblQueueSize.Text = $"{qByteCount} (num groups: {qItemCount})";

            // TODO: use databinding

            const string format = "0.00";

            lblTotalProcessed.Text = ByteSize.FromBytes(stats.numRomBytesAnalyzed).ToString(format);
            lblNumberModified.Text = ByteSize.FromBytes(stats.numRomBytesModified).ToString(format);
            lblModifiedDBs.Text = ByteSize.FromBytes(stats.numDBModified).ToString(format);
            lblModifiedDPs.Text = ByteSize.FromBytes(stats.numDpModified).ToString(format);
            lblModifiedFlags.Text = ByteSize.FromBytes(stats.numMarksModified).ToString(format);
            lblModifiedXFlags.Text = ByteSize.FromBytes(stats.numXFlagsModified).ToString(format);
            lblModifiedMFlags.Text = ByteSize.FromBytes(stats.numMFlagsModified).ToString(format);
        }
    }
}
using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ByteSizeLib;
using Diz.Core.import;
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
                capturing.Run(mainWindow.Project.Data);
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

        private void UpdateUi()
        {
            var running = capturing?.Running ?? false;
            var finishing = capturing?.Finishing ?? false;

            lblStatus.Text = !running ? "Not running" : finishing ? "Stopping..." : "Running";

            btnFinish.Enabled = !finishing && running;
            btnStart.Enabled = !running;

            pictureGreenSpinner.Visible = pictureGreenSpinner.Enabled = running;

            if (running)
            {
                lblResultStatus.Text = "";
            }
            else if (lastError != "")
            {
                lblResultStatus.Text = lastError;
                lblResultStatus.ForeColor = Color.Red;
            }
            else
            {
                lblResultStatus.Text = "Success!";
                lblResultStatus.ForeColor = Color.ForestGreen;
            }

            if (capturing == null)
                return;

            var currentStats = capturing.GetStats();
            var (stats, totalQueueBytes) = currentStats;

            var qItemCount = capturing.BlocksToProcess.ToString();
            var qByteCount = ByteSize.FromBytes(totalQueueBytes).ToString("0.0");

            lblQueueSize.Text = $"{qByteCount} (num groups: {qItemCount})";

            // TODO: use databinding

            lblTotalProcessed.Text = ByteSize.FromBytes(stats.NumRomBytesAnalyzed).ToString("0.00");
            lblNumberModified.Text = ByteSize.FromBytes(stats.NumRomBytesModified).ToString("0.00");
            lblModifiedDBs.Text = ByteSize.FromBytes(stats.NumDbModified).ToString("0.00");
            lblModifiedDPs.Text = ByteSize.FromBytes(stats.NumDpModified).ToString("0.00");
            lblModifiedFlags.Text = ByteSize.FromBytes(stats.NumMarksModified).ToString("0.00");
            lblModifiedXFlags.Text = ByteSize.FromBytes(stats.NumXFlagsModified).ToString("0.00");
            lblModifiedMFlags.Text = ByteSize.FromBytes(stats.NumMFlagsModified).ToString("0.00");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            MessageBox.Show("What is this? \r\n" +
                            "Connect via socket to a special build of BSNES-plus and capture live tracelog as you play the game " +
                            "in realtime or play back a movie/recording/TAS.\r\n\r\n" +
                            "As each instruction is visited by the CPU, info like X,M,DB,D and flags are capture and " +
                            "logged in Diz.  This will greatly aid in dissasembly.\r\n\r\n" +
                            "If you're just starting a ROM hacking project from scratch, you want to see this" +
                            " capture a lot of modified data for X,M,DP,DB and marking bytes as Opcode/Operands.\r\n\r\n" +
                            "If you're far into a ROM hacking project, you will start seeing fewer NEWLY DISCOVERED " +
                            "modifications here. Try playing through different parts of the game, menus, every " +
                            "combination of searching you can do to allow this tool to discover as much as it can.\r\n\r\n" +
                            "When you close this window, try exporting your disassembly and see how much you uncovered!\r\n");
        }
    }
}
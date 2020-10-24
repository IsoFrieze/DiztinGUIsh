using System;
using System.Windows.Forms;
using System.Windows.Media;
using ByteSizeLib;
using Diz.Core.import;
using LiveCharts;
using LiveCharts.Wpf;
using Color = System.Drawing.Color;

namespace DiztinGUIsh.window.dialog
{
    public partial class BSNESTraceLogBinaryMonitorForm
    {
        private bool initializedChart;
        private readonly ChartValues<long> chartValuesBytesModified = new ChartValues<long>();
        private long chartValueBytesModified_previous = 0;

        private const int refreshGraphEveryNDataPoints = 100;
        private int dataPointsIn = -1;

        private void AppendToChart((BSNESTraceLogImporter.Stats stats, int bytesInQueue) currentStats)
        {
            InitChart();

            // TODO: re-enable, it's maybe a little slow.
            /*if (dataPointsIn == -1 || ++dataPointsIn >= refreshGraphEveryNDataPoints)
                dataPointsIn = 0;

            if (dataPointsIn != 0)
                return;

            var diffBytes = currentStats.stats.numRomBytesModified - chartValueBytesModified_previous;
            chartValueBytesModified_previous = currentStats.stats.numRomBytesModified;
            chartValuesBytesModified.Add(diffBytes);*/
        }

        private void InitChart()
        {
            if (initializedChart)
                return;

            cartesianChart1.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Instructions Modified",
                    Values = chartValuesBytesModified,
                    PointGeometry = Geometry.Empty,
                },
            };

            cartesianChart1.DisableAnimations = true;

            initializedChart = true;
        }

        private void UpdateUI()
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

            AppendToChart(currentStats);

            var qItemCount = capturing.BlocksToProcess.ToString();
            var qByteCount = ByteSize.FromBytes(totalQueueBytes).ToString("0.0");

            lblQueueSize.Text = $"{qByteCount} (num groups: {qItemCount})";

            // TODO: use databinding

            lblTotalProcessed.Text = ByteSize.FromBytes(stats.numRomBytesAnalyzed).ToString("0.00");
            lblNumberModified.Text = ByteSize.FromBytes(stats.numRomBytesModified).ToString("0.00");
            lblModifiedDBs.Text = ByteSize.FromBytes(stats.numDBModified).ToString("0.00");
            lblModifiedDPs.Text = ByteSize.FromBytes(stats.numDpModified).ToString("0.00");
            lblModifiedFlags.Text = ByteSize.FromBytes(stats.numMarksModified).ToString("0.00");
            lblModifiedXFlags.Text = ByteSize.FromBytes(stats.numXFlagsModified).ToString("0.00");
            lblModifiedMFlags.Text = ByteSize.FromBytes(stats.numMFlagsModified).ToString("0.00");
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
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using Diz.Core.import;

namespace DiztinGUIsh.window.dialog
{
    public partial class BSNESTraceLogBinaryMonitor : Form
    {
        private MainWindow MainWindow;
        private CancellationTokenSource cancelToken;
        private BSNESTraceLogImporter importer;

        // caution: thread safety for next items:
        private BlockingCollection<byte[]> queue;
        private int totalModified;   
        private int totalAnalyzed;
        // end thread safety

        public BSNESTraceLogBinaryMonitor(MainWindow window)
        {
            MainWindow = window;
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            importer = new BSNESTraceLogImporter(MainWindow.Project.Data);
            cancelToken = new CancellationTokenSource();
            queue = new BlockingCollection<byte[]>();

            totalModified = 0;
            totalAnalyzed = 0;

            backgroundWorker1_pipeReader.RunWorkerAsync();
            backgroundWorker2_processQueue.RunWorkerAsync();

            timer1.Enabled = true;
            btnCancel.Enabled = true;
            button1.Enabled = false;
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            var bytes = new byte[22];

            var tcpClient = new TcpClient();
            var ipEndPoint = new IPEndPoint(IPAddress.Loopback, 27015);

            tcpClient.Connect(ipEndPoint);

            var networkStream = tcpClient.GetStream();

            while (!backgroundWorker1_pipeReader.CancellationPending)
            {
                var bytesRead = networkStream.Read(bytes, 0, bytes.Length);
                if (bytesRead <= 0)
                    return;
                queue.Add(bytes);
            }
        }

        private void backgroundWorker2_processQueue_DoWork(object sender, DoWorkEventArgs e)
        {
            while (!backgroundWorker2_processQueue.CancellationPending)
            {
                try
                {
                    var bytes = queue.Take(cancelToken.Token);
                    Debug.Assert(bytes.Length == 22);
                    var (numChanged, numLinesAnalyzed) = importer.ImportTraceLogLineBinary(bytes);
                    totalModified += numChanged;
                    totalAnalyzed += numLinesAnalyzed;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            UpdateUI();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            btnCancel.Enabled = false;
            button1.Enabled = true;

            cancelToken.Cancel();
            backgroundWorker1_pipeReader.CancelAsync();
            backgroundWorker2_processQueue.CancelAsync();

            timer1.Enabled = false;
        }

        private void backgroundWorker2_processQueue_RunWorkerCompleted_1(object sender, RunWorkerCompletedEventArgs e)
        {
            UpdateUI();
        }

        private void backgroundWorker1_pipeReader_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            lblQueuSize.Text = queue.Count.ToString();
            var running = backgroundWorker2_processQueue != null && backgroundWorker1_pipeReader != null;
            var cancelling = cancelToken?.IsCancellationRequested ?? false;

            lblTotalProcessed.Text = totalAnalyzed.ToString();
            lblNumberModified.Text = totalModified.ToString();

            lblStatus.Text = !running ? "Not running" : !cancelling ? "Stopping..." : "Running";

            btnCancel.Enabled = running && !cancelling;
            button1.Enabled = !running;
        }
    }
}
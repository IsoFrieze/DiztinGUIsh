using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Diz.Import.bsnes.tracelog;
using Diz.Ui.Winforms.util;

namespace DiztinGUIsh.window.dialog;

// TODO: add better controller/view for this, it's a bit mixed up.
//
// NOTE: BSNESTraceLogCapture does a lot of threading. It's decently protected but,
// while that stuff is running, try and avoid using 'Data' anywhere outside BSNESTraceLogCapture.
// eventually, if we want to do that we need to retrofit the rest of the app to take advantage of that.
public partial class BsnesTraceLogBinaryMonitorForm : Form
{
    private readonly BsnesTraceLogCaptureController captureController;
    private string lastError;

    public BsnesTraceLogBinaryMonitorForm(BsnesTraceLogCaptureController captureController)
    {
        this.captureController = captureController;
        InitializeComponent();
    }

    private void btnStart_Click(object sender, EventArgs e)
    {
        timer1.Enabled = true;
        btnFinish.Enabled = true;
        btnStart.Enabled = false;

        Start();
    }
    
    private void btnFinish_Click(object sender, EventArgs e)
    {
        captureController.SignalToStop();
        UpdateUi();
    }

    private async void Start()
    {
        // TODO: this thread stuff should really go into the Controller and it should call US for notifications
        // TODO: error handling is busted here.
        await Task.Run(() => captureController.Run()).ContinueWith(OnCapturingFinishedException);
        UpdateUi();
    }

    private void OnCapturingFinishedException(Task task) => this.InvokeIfRequired(() => CapturingFinished(task.Exception));

    private void CapturingFinished(AggregateException ex)
    {
        if (ex != null) {
            OnError(ex);
        }

        timer1.Enabled = false;
        UpdateUi();
    }

    private void OnError(AggregateException e)
    {
        Console.WriteLine(e.ToString());
        lastError = e.InnerExceptions.Select(ex => ex.Message).Aggregate((line, val) => line += val + "\n");
    }
}
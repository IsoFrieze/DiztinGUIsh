using System;
using System.Runtime.InteropServices;
using System.Threading;
using DiztinGUIsh.window.dialog;

namespace DiztinGUIsh
{
    // TODO: replace this with Task and async/await. don't use threads directly.
    public abstract class ProgressBarWorker
    {
        private ProgressDialog dialog;
        private bool isRunning;
        private Thread backgroundThread;
        public bool IsMarquee { get; set; }
        public string TextOverride { get; set; }

        protected void UpdateProgress(int i)
        {
            // i must be in range of 0 to 100
            dialog.UpdateProgress(i);
        }

        protected abstract void Thread_DoWork();

        // call from main thread to start a long-running job
        // 
        // shows a progress bar dialog box while the work is being performed
        // note: we're not being super-careful about thread safety here.
        // if main thread is blocked it should be fine, but, if other things can
        // still happen in the background, be really careful.
        public void Run()
        {
            Setup();
            backgroundThread.Start();
            WaitForJobToFinish();
        }

        protected virtual void Setup()
        {
            if (isRunning)
                throw new InvalidOperationException(
                    "Progress bar already running, existing job must finish first");

            isRunning = true;

            dialog = new ProgressDialog(IsMarquee, TextOverride);

            // setup, but don't start, the new thread
            backgroundThread = new Thread(Thread_Main);

            // honestly, not sure about this. works around some Invoke() stuff
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                backgroundThread.SetApartmentState(ApartmentState.STA);
            }
        }

        // blocking function
        private void WaitForJobToFinish()
        {
            // blocks til worker thread closes this dialog box
            dialog.ShowDialog();
        }

        // called from a new worker thread
        private void Thread_Main()
        {
            try
            {
                // BAD APPROACH. we should instead get an event
                // I'm too lazy right now. TODO FIXME
                while (!dialog.Visible)
                    Thread.Sleep(50);

                Thread_DoWork();
            }
            finally
            {
                isRunning = false;
                SignalJobIsDone();
            }
        }

        private void SignalJobIsDone()
        {
            // unblock the main thread from ShowDialog()
            dialog?.BeginInvoke(new Action(() => dialog.Close()));
        }
    }
}



namespace DiztinGUIsh
{
    public class ProgressBarJob : ProgressBarWorker
    {
        // a version that keeps calling 'callback' until it returns -1
        public static void Loop(long maxProgress, NextAction callback, string overrideTxt = null)
        {
            var j = new ProgressBarJob
            {
                MaxProgress = maxProgress,
                Callback = callback,
                IsMarquee = maxProgress == -1,
                TextOverride = overrideTxt,
            };
            j.Run();
        }

        // a version that calls action once and exits
        // shows a "marquee" i.e. spinner
        public static void RunAndWaitForCompletion(Action action, string overrideTxt = null)
        {
            Loop(-1, () => {
                action();
                return -1;
            }, overrideTxt);
        }

        public NextAction Callback { get; set; }
        public long MaxProgress { get; set; }

        protected override void Thread_DoWork()
        {
            UpdateProgress(0);
            var progress = -1L;
            do {
                progress = Callback();
                UpdateProgress(progress);
            } while (progress > 0);
        }

        private int previousProgress;

        protected void UpdateProgress(long currentProgress)
        {
            if (MaxProgress <= 0)
                return;

            var percent = currentProgress / (float)MaxProgress;
            var progressValue = (int)(percent * 100);

            if (progressValue <= previousProgress)
                return;

            // don't do this too often, kinda slow due to thread synchronization.
            base.UpdateProgress(progressValue);

            previousProgress = progressValue;
        }

        // return > 0 to continue. return value will be used to indicate progress in range of [0 -> MaxProgress]
        public delegate long NextAction();
    }
}
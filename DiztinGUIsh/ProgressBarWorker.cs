using System;
using System.Threading;
using DiztinGUIsh.window.dialog;

namespace DiztinGUIsh
{ 
    public abstract class ProgressBarWorker
    {
        private ProgressDialog Dialog = null;
        private bool IsRunning = false;
        private Thread backgroundThread = null;

        protected void UpdateProgress(int i)
        {
            // i must be in range of 0 to 100
            Dialog.UpdateProgress(i);
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
            if (IsRunning)
                throw new InvalidOperationException(
                    "Progress bar already running, existing job must finish first");

            IsRunning = true;

            Dialog = new ProgressDialog();

            // setup, but don't start, the new thread
            backgroundThread = new Thread(new ThreadStart(Thread_Main));
        }

        // blocking function
        private void WaitForJobToFinish()
        {
            // blocks til worker thread closes this dialog box
            Dialog.ShowDialog();
        }

        // called from a new worker thread
        private void Thread_Main()
        {
            try
            {
                // BAD APPROACH. we should instead get an event
                // I'm too lazy right now. TODO FIXME
                while (!Dialog.Visible)
                    Thread.Sleep(50);

                Thread_DoWork();
            }
            finally
            {
                IsRunning = false;
                SignalJobIsDone();
            }
        }

        private void SignalJobIsDone()
        {
            // unblock the main thread from ShowDialog()
            Dialog?.BeginInvoke(new Action(() => Dialog.Close()));
        }
    }
}



namespace DiztinGUIsh
{
    public class LongJob : ProgressBarWorker
    {
        public static void Loop(long maxProgress, NextAction callback)
        {
            var j = new LongJob()
            {
                MaxProgress = maxProgress,
                Callback = callback,
            };
            j.Run();
        }

        public NextAction Callback { get; set; }
        public long MaxProgress { get; set; }

        protected override void Thread_DoWork()
        {
            var progress = -1L;
            do {
                progress = Callback();
                UpdateProgress(progress);
            } while (progress > 0);
        }

        private int previousProgress = 0;

        protected void UpdateProgress(long currentProgress)
        {
            if (MaxProgress <= 0)
                return;

            float percent = (float)(currentProgress) / (float)MaxProgress;
            int progressValue = (int)(percent * 100);

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
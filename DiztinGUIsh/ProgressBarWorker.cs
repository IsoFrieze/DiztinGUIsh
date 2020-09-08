using System;
using System.Threading;
using DiztinGUIsh.window.dialog;

namespace DiztinGUIsh
{ public abstract class ProgressBarWorker
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

using System;
using System.IO;

namespace DiztinGUIsh
{
    public class LargeFileReader : ProgressBarWorker
    {
        public string Filename { get; set; }
        public Action<string> LineReadCallback { get; set; }

        protected long FileLengthInBytes { get; set; }

        protected override void Thread_DoWork()
        {
            using (var fs = File.Open(Filename, FileMode.Open, FileAccess.Read))
            using (var bs = new BufferedStream(fs))
            using (var sr = new StreamReader(bs))
            {
                FileLengthInBytes = fs.Length;

                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    this.LineReadCallback(line);
                    this.UpdateProgress(fs.Position);
                }
            }
        }

        private int previousProgress = 0;

        protected void UpdateProgress(long currentPositionInBytes)
        {
            float percent = (float)currentPositionInBytes / (float)FileLengthInBytes;
            int progressValue = (int)(percent * 100);

            if (progressValue <= previousProgress)
                return;

            // don't do this too often, kinda slow due to thread synchronization.
            base.UpdateProgress(progressValue);

            previousProgress = progressValue;
        }
        public static void ReadFileLines(string filename, Action<string> lineReadCallback)
        {
            var lfr = new LargeFileReader()
            {
                Filename = filename,
                LineReadCallback = lineReadCallback,
            };
            lfr.Run();
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;

namespace DiztinGUIsh
{
    public class LargeFilesReader : ProgressBarWorker
    {
        public List<string> Filenames { get; set; }
        public Action<string> LineReadCallback { get; set; }

        protected long SumFileLengthsInBytes { get; set; }
        protected long BytesReadFromPreviousFiles { get; set; }

        protected override void Thread_DoWork()
        {
            SumFileLengthsInBytes = 0L;
            foreach (var filename in Filenames)
            {
                SumFileLengthsInBytes += Util.GetFileSizeInBytes(filename);
            }

            BytesReadFromPreviousFiles = 0L;
            foreach (var filename in Filenames) {
                using (var fs = File.Open(filename, FileMode.Open, FileAccess.Read))
                using (var bs = new BufferedStream(fs))
                using (var sr = new StreamReader(bs))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        this.LineReadCallback(line);
                        this.UpdateProgress(fs.Position);
                    }

                    BytesReadFromPreviousFiles += fs.Length;
                }
            }
        }

        private int previousProgress = 0;

        protected void UpdateProgress(long currentPositionInBytes)
        {
            float percent = (float)(BytesReadFromPreviousFiles + currentPositionInBytes) / (float)SumFileLengthsInBytes;
            int progressValue = (int)(percent * 100);

            if (progressValue <= previousProgress)
                return;

            // don't do this too often, kinda slow due to thread synchronization.
            base.UpdateProgress(progressValue);

            previousProgress = progressValue;
        }
        public static void ReadFilesLines(string[] filenames, Action<string> lineReadCallback)
        {
            var lfr = new LargeFilesReader()
            {
                Filenames = new List<string>(filenames),
                LineReadCallback = lineReadCallback,
            };
            lfr.Run();
        }
    }
}

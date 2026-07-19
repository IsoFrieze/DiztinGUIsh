using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Diz.Controllers.interfaces;
using Diz.Core.util;

namespace Diz.Controllers.util
{
    // new-ui plan step 6: was a ProgressBarWorker subclass (raw Thread + spin-wait). Now a
    // plain synchronous line reader that reports progress through IProgress<int> and honors a
    // CancellationToken. It no longer owns a progress view or a thread -- ProjectController runs
    // it via DoLongRunningTaskAsync, which puts it on a background Task and shows the progress UI.
    public class LargeFilesReader : ILargeFilesReaderController
    {
        public IReadOnlyCollection<string> Filenames { get; set; }
        public Action<string> LineReadCallback { get; set; }

        private long SumFileLengthsInBytes { get; set; }
        private long BytesReadFromPreviousFiles { get; set; }
        private int previousProgress;

        /// <summary>
        /// Read every line of every file, invoking LineReadCallback per line. Reports 0..100
        /// (bytes read / total bytes) via <paramref name="progress"/>. Runs synchronously on
        /// whatever thread calls it (a background Task, under the new task handler).
        /// </summary>
        public void Read(IProgress<int> progress, CancellationToken token)
        {
            previousProgress = 0;

            SumFileLengthsInBytes = 0L;
            foreach (var filename in Filenames)
                SumFileLengthsInBytes += Util.GetFileSizeInBytes(filename);

            BytesReadFromPreviousFiles = 0L;
            foreach (var filename in Filenames)
            {
                using var fs = File.Open(filename, FileMode.Open, FileAccess.Read);
                using var bs = new BufferedStream(fs);
                using var sr = new StreamReader(bs);
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    token.ThrowIfCancellationRequested();
                    LineReadCallback(line);
                    UpdateProgress(progress, fs.Position);
                }

                BytesReadFromPreviousFiles += fs.Length;
            }
        }

        private void UpdateProgress(IProgress<int> progress, long currentPositionInBytes)
        {
            if (progress == null || SumFileLengthsInBytes <= 0)
                return;

            var percent = (BytesReadFromPreviousFiles + currentPositionInBytes) / (float)SumFileLengthsInBytes;
            var progressValue = (int)(percent * 100);

            if (progressValue <= previousProgress)
                return;

            // don't report too often (progress marshalling across threads has a cost).
            progress.Report(progressValue);
            previousProgress = progressValue;
        }
    }

    public interface ILargeFilesReaderController
    {
        IReadOnlyCollection<string> Filenames { get; set; }
        Action<string> LineReadCallback { get; set; }

        // new-ui plan step 6: replaces the old parameterless Run() (which started a thread and
        // blocked). The caller supplies progress + cancellation and runs it on a background Task.
        void Read(IProgress<int> progress, CancellationToken token);
    }
}

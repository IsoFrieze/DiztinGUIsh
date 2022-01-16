using System;
using System.Collections.Generic;
using System.IO;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core.util;

namespace Diz.Controllers.util
{
    public class LargeFilesReader : ProgressBarWorker, ILargeFilesReaderController
    {
        public IReadOnlyCollection<string> Filenames { get; set; }
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
            foreach (var filename in Filenames)
            {
                using var fs = File.Open(filename, FileMode.Open, FileAccess.Read);
                using var bs = new BufferedStream(fs);
                using var sr = new StreamReader(bs);
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    LineReadCallback(line);
                    UpdateProgress(fs.Position);
                }

                BytesReadFromPreviousFiles += fs.Length;
            }
        }

        private int previousProgress;

        protected void UpdateProgress(long currentPositionInBytes)
        {
            var percent = (BytesReadFromPreviousFiles + currentPositionInBytes) / (float)SumFileLengthsInBytes;
            var progressValue = (int)(percent * 100);

            if (progressValue <= previousProgress)
                return;

            // don't do this too often, kinda slow due to thread synchronization.
            base.UpdateProgress(progressValue);

            previousProgress = progressValue;
        }

        public LargeFilesReader(IProgressView view) : base(view) { }
    }

    public interface ILargeFilesReaderController
    {
        IReadOnlyCollection<string> Filenames { get; set; }
        Action<string> LineReadCallback { get; set; }
        void Run();
    }
}

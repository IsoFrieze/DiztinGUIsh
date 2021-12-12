using System.Threading;

namespace Diz.Core.import
{
    public partial class BsnesTraceLogImporter
    {
        public struct Stats
        {
            public long NumRomBytesAnalyzed;
            public long NumRomBytesModified;

            public long NumXFlagsModified;
            public long NumMFlagsModified;
            public long NumDbModified;
            public long NumDpModified;
            public long NumMarksModified;
        }

        private readonly ReaderWriterLockSlim statsLock = new();
        private Stats currentStats;

        // return a copy of struct so caller doesn't have to deal with thread safety issues 
        public Stats CurrentStats {
            get {
                statsLock.EnterReadLock();
                try {
                    return currentStats;
                } finally {
                    statsLock.ExitReadLock();
                }
            }
        }

        private void InitStats()
        {
            statsLock.EnterWriteLock();
            try {
                currentStats.NumRomBytesAnalyzed = 0;
                currentStats.NumRomBytesModified = 0;
                currentStats.NumXFlagsModified = 0;
                currentStats.NumMFlagsModified = 0;
                currentStats.NumDbModified = 0;
                currentStats.NumDpModified = 0;
                currentStats.NumMarksModified = 0;   
            } finally {
                statsLock.ExitWriteLock();
            }
        }
    }
}
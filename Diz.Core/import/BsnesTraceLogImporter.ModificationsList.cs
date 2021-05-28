using Diz.Core.model;
using Diz.Core.model.byteSources;

namespace Diz.Core.import
{
    public partial class BsnesTraceLogImporter
    {
        // important note: Tracelog capture doesn't guarantee ordering for tracelog data received.
        //
        // i.e. tracelog worker threads divide and conquer and don't call us in any particular order, 
        // so we might overwrite newer data in an address with older data.
        //
        // for tracing applications, I don't think it matters, just keep it in mind. or,
        // add some support for sequencing of incoming data to keep it in order if it's important.

        public class ModificationData : PoolItem
        {
            // imported data from trace log
            public int SnesAddress;
            public int Pc;
            public FlagType FlagType;
            public int DataBank;
            public int DirectPage;
            public bool XFlagSet;
            public bool MFlagSet;

            // we will set these if any of the above field were modified
            public bool Changed;
            public bool MDb, MMarks, MDp, Mx, Mm;

            // precondition: rombyte (minimum of) read lock already acquired
            private void CompareToExisting(ByteEntry romByte)
            {
                MDb = romByte.DataBank != DataBank;
                MMarks = romByte.TypeFlag != FlagType;
                MDp = romByte.DirectPage != DirectPage;
                Mx = romByte.XFlag != XFlagSet;
                Mm = romByte.MFlag != MFlagSet;

                Changed = MMarks || MDb || MDp || Mx || Mm;
            }

            // precondition: ByteEntry (minimum of) read lock already acquired
            private void ApplyModification(ByteEntry byteOffset)
            {
                byteOffset.Lock.EnterWriteLock();
                try
                {
                    byteOffset.TypeFlag = FlagType;
                    byteOffset.DataBank = (byte) DataBank;
                    byteOffset.DirectPage = 0xFFFF & DirectPage;
                    byteOffset.XFlag = XFlagSet;
                    byteOffset.MFlag = MFlagSet;
                }
                finally
                {
                    byteOffset.Lock.ExitWriteLock();
                }
            }

            public void ApplyModificationIfNeeded(ByteEntry byteOffset)
            {
                byteOffset.Lock.EnterUpgradeableReadLock();
                try
                {
                    CompareToExisting(byteOffset);
                    if (Changed)
                        ApplyModification(byteOffset);
                }
                finally
                {
                    byteOffset.Lock.ExitUpgradeableReadLock();
                }
            }
        }

        // optimization: save allocations
        private ObjPool<ModificationData> modificationDataPool;

        private void InitObjectPool()
        {
            modificationDataPool = new ObjPool<ModificationData>();
        }

        private void ApplyModification(ModificationData modData)
        {
            ApplyModificationIfNeeded(modData);

            UpdateStats(modData);
        }

        private void ApplyModificationIfNeeded(ModificationData modData)
        {
            var romByte = data.RomByteSource?.Bytes[modData.Pc];
            modData.ApplyModificationIfNeeded(romByte);
        }

        private void UpdateStats(ModificationData modData)
        {
            if (!modData.Changed)
                return;

            statsLock.EnterWriteLock();
            try
            {
                currentStats.NumRomBytesModified++;

                currentStats.NumMarksModified += modData.MMarks ? 1 : 0;
                currentStats.NumDbModified += modData.MDb ? 1 : 0;
                currentStats.NumDpModified += modData.MDp ? 1 : 0;
                currentStats.NumXFlagsModified += modData.Mx ? 1 : 0;
                currentStats.NumMFlagsModified += modData.Mm ? 1 : 0;
            }
            finally
            {
                statsLock.ExitWriteLock();
            }
        }
    }
}
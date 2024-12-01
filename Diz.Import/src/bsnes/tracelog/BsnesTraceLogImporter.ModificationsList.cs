﻿using Diz.Core.Interfaces;
using Diz.Core.model;

namespace Diz.Import.bsnes.tracelog;

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
        public bool changed;
        public bool mDb, mMarks, mDp, mX, mM;

        public BsnesTraceLogCaptureController.TraceLogCaptureSettings CaptureSettings;

        // precondition: rombyte (minimum of) read lock already acquired
        private void CompareToExisting(ISnesRomByte romByte)
        {
            mDb = romByte.DataBank != DataBank;
            mMarks = romByte.TypeFlag != FlagType;
            mDp = romByte.DirectPage != DirectPage;
            mX = romByte.XFlag != XFlagSet;
            mM = romByte.MFlag != MFlagSet;

            changed = mMarks || mDb || mDp || mX || mM;
        }

        // precondition: rombyte (minimum of) read lock already acquired
        private void ApplyModification(IRomByte romByte)
        {
            romByte.Lock.EnterWriteLock();
            try
            {
                romByte.TypeFlag = FlagType;
                romByte.DataBank = (byte) DataBank;
                romByte.DirectPage = 0xFFFF & DirectPage;
                romByte.XFlag = XFlagSet;
                romByte.MFlag = MFlagSet;
            }
            finally
            {
                romByte.Lock.ExitWriteLock();
            }
        }

        public void ApplyModificationIfNeeded(IRomByte romByte)
        {
            romByte.Lock.EnterUpgradeableReadLock();
            try
            {
                CompareToExisting(romByte);
                if (changed)
                    ApplyModification(romByte);
            }
            finally
            {
                romByte.Lock.ExitUpgradeableReadLock();
            }
        }
    }

    // optimization: save allocations
    private readonly ObjPool<ModificationData> modificationDataPool;

    private void UpdateStats(ModificationData modData)
    {
        if (!modData.changed)
            return;

        statsLock.EnterWriteLock();
        try
        {
            currentStats.NumRomBytesModified++;

            currentStats.NumMarksModified += modData.mMarks ? 1 : 0;
            currentStats.NumDbModified += modData.mDb ? 1 : 0;
            currentStats.NumDpModified += modData.mDp ? 1 : 0;
            currentStats.NumXFlagsModified += modData.mX ? 1 : 0;
            currentStats.NumMFlagsModified += modData.mM ? 1 : 0;

            currentStats.NumCommentsMarked = tracelogCommentsGenerated.Count;
        }
        finally
        {
            statsLock.ExitWriteLock();
        }
    }
}
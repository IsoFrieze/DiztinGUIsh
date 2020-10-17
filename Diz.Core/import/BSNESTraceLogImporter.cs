using System;
using System.Diagnostics;
using System.IO;
using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Core.import
{
    public class BSNESTraceLogImporter
    {
        private Data Data;
        int romSize;
        public struct Stats
        {
            public long numRomBytesAnalyzed;
            public long numRomBytesModified;

            public long numXFlagsModified;
            public long numMFlagsModified;
            public long numDBModified;
            public long numDpModified;
            public long numMarksModified;
        }

        public Stats CurrentStats;

        public Data GetData()
        {
            return Data;
        }

        public BSNESTraceLogImporter(Data data)
        {
            Data = data;
            romSize = Data.GetROMSize();

            CurrentStats.numRomBytesAnalyzed = 0;
            CurrentStats.numRomBytesModified = 0;
            CurrentStats.numXFlagsModified = 0;
            CurrentStats.numMFlagsModified = 0;
            CurrentStats.numDBModified = 0;
            CurrentStats.numDpModified = 0;
            CurrentStats.numMarksModified = 0;
        }

        // this class exists for performance optimization ONLY.
        // class representing offsets into a trace log
        // we calculate it once from sample data and hang onto it
        private class CachedTraceLineIndex
        {
            // NOTE: newer versions of BSNES use different text for flags. check for completeness.
            private string sample =
                "028cde rep #$30               A:0004 X:0000 Y:0004 S:1fdd D:0000 DB:02 nvmxdiZC V:133 H: 654 F:36";

            // index of the start of the info
            public readonly int
                addr,
                D,
                DB,
                flags,
                f_N,
                f_V,
                f_M,
                f_X,
                f_D,
                f_I,
                f_Z,
                f_C;

            public CachedTraceLineIndex()
            {
                int SkipToken(string token)
                {
                    return sample.IndexOf(token) + token.Length;
                }

                addr = 0;
                D = SkipToken("D:");
                DB = SkipToken("DB:");
                flags = DB + 3;

                // flags: nvmxdizc
                f_N = flags + 0;
                f_V = flags + 1;
                f_M = flags + 2;
                f_X = flags + 3;
                f_D = flags + 4;
                f_I = flags + 5;
                f_Z = flags + 6;
                f_C = flags + 7;
            }
        }

        private static readonly CachedTraceLineIndex CachedIdx = new CachedTraceLineIndex();

        public void ImportTraceLogLine(string line)
        {
            // caution: very performance-sensitive function, please take care when making modifications
            // string.IndexOf() is super-slow too.
            // Input lines must follow this strict format and be this exact formatting and column indices.
            // 028cde rep #$30               A:0004 X:0000 Y:0004 S:1fdd D:0000 DB:02 nvmxdiZC V:133 H: 654 F:36

            int GetHexValueAt(int startIndex, int length)
            {
                return Convert.ToInt32(line.Substring(startIndex, length), 16);
            }

            if (line.Length < 80)
                return;

            var snesAddress = GetHexValueAt(0, 6);

            // TODO: error treatment / validation

            var directPage = GetHexValueAt(CachedIdx.D, 4);
            var dataBank = GetHexValueAt(CachedIdx.DB, 2);

            // 'X' = unchecked in bsnesplus debugger UI = (8bit), 'x' or '.' = checked (16bit)
            var xflag_set = line[CachedIdx.f_X] == 'X';

            // 'M' = unchecked in bsnesplus debugger UI = (8bit), 'm' or '.' = checked (16bit)
            var mflag_set = line[CachedIdx.f_M] == 'M';

            SetOpcodeAndOperandsFromTraceData(snesAddress, dataBank, directPage, xflag_set, mflag_set);
        }

        // this is same as above but, reads the same data from a binary format. this is for
        // performance reasons to try and stream the data live from BSNES
        public void ImportTraceLogLineBinary(byte[] bytes)
        {
            // extremely performance-intensive function. be really careful when adding stuff

            Debug.Assert(bytes.Length == 21);
            var pointer = 0;

            // -----------------------------
            var snesAddress = ByteUtil.ByteArrayToInt24(bytes, pointer);
            pointer += 3;

            var opcodeLen = bytes[pointer++];

            // skip opcodes. NOTE: must read all 5 bytes but only use up to 'opcode_len' bytes 
            //var op  = bytes[pointer++];
            //var op0 = bytes[pointer++];
            //var op1 = bytes[pointer++];
            //var op2 = bytes[pointer++];
            pointer += 4;

            // skip A register
            pointer += 2;

            // skip X register
            pointer += 2;

            // skip Y register
            pointer += 2;

            // skip S register
            pointer += 2;

            var directPage = ByteUtil.ByteArrayToInt16(bytes, pointer);
            pointer += 2;

            var dataBank = bytes[pointer++];

            // skip, flag 'e' for emulation mode or not
            // var emuFlag = bytes[pointer++] == 0x01;
            pointer++;

            // the real flags, we mainly care about X and M
            var flags = bytes[pointer++];
            // n = flags & 0x80;
            // v = flags & 0x40;
            // m = flags & 0x20;
            // d = flags & 0x08;
            // i = flags & 0x04;
            // z = flags & 0x02;
            // c = flags & 0x01;
            var xflagSet = (flags & 0x10) != 0;
            var mflagSet = (flags & 0x20) != 0;

            Debug.Assert(pointer == bytes.Length);

            SetOpcodeAndOperandsFromTraceData(snesAddress, dataBank, directPage, xflagSet, mflagSet, opcodeLen);
        }

        // Mark collected trace data for a RomByte (which should be an opcode) AND any of the operands that follow us.
        private void SetOpcodeAndOperandsFromTraceData(
            int snesAddress, int dataBank, int directPage, 
            bool xflagSet, bool mflagSet,
            int opcodeLen = -1)
        {
            // extremely performance-intensive function. be really careful when adding stuff
            var currentIndex = 0;

            while (true) 
            {
                if (!SetOneRomByteFromTraceData(snesAddress, dataBank, directPage, xflagSet, mflagSet, opcodeLen, currentIndex)) 
                    break;

                snesAddress = RomUtil.CalculateSNESOffsetWithWrap(snesAddress, 1);
                currentIndex++;
            }
        }

        private bool SetOneRomByteFromTraceData(int snesAddress, int dataBank, int directPage, bool xflagSet, bool mflagSet,
            int opcodeLen, int currentIndex)
        {
            CurrentStats.numRomBytesAnalyzed++;

            var pc = Data.ConvertSNEStoPC(snesAddress);
            if (!IsOKToSetThisRomByte(pc, currentIndex, opcodeLen)) 
                return false;

            var flagType = currentIndex == 0 ? Data.FlagType.Opcode : Data.FlagType.Operand;

            LogStats(pc, dataBank, directPage, xflagSet, mflagSet, flagType);

            // TODO: it doesn't hurt anything but, banks come back mirrored sometimes,
            // would be cool to find a way to do the right thing and deal with the mirroring so we're not
            // flipping and re-flipping bytes, but still maintain correctness with the game itself.

            // actually do the update
            Data.SetFlag(pc, flagType);
            Data.SetDataBank(pc, dataBank);
            Data.SetDirectPage(pc, directPage);
            Data.SetXFlag(pc, xflagSet);
            Data.SetMFlag(pc, mflagSet);

            return true;
        }

        private bool IsOKToSetThisRomByte(int pc, int opIndex, int instructionByteLen)
        {
            if (pc < 0 || pc >= romSize)
                return false;

            if (instructionByteLen != -1 && (instructionByteLen < 1 || instructionByteLen > 4))
            {
                throw new InvalidDataException($"Inavalid opcode+operand byte length {instructionByteLen}. Must be -1, or between 1 and 4");
            } 
            if (opIndex < 0 || opIndex > 4) // yes, 4, not 3. it'll bail below
            {
                throw new InvalidDataException($"Inavalid opcode index {opIndex}. Must be between 0 and 4");
            }

            if (instructionByteLen != -1)
            {
                // just make sure we're in range if we know the amount of bytes we need to process
                return opIndex < instructionByteLen;
            }
            else
            {
                // we don't know how many bytes to process, so have to do some fuzzy guessing now. play it safe.

                // easy: this is the first byte, this will be the Opcode, so clear that for takeoff.
                if (opIndex == 0)
                    return true;

                // otherwise, this is NOT the first byte (Opcode), and we don't have information about how many bytes
                // past us are Operands. Could be none, could be up to 3.
                //
                // We're trying to mark as many bytes as operands with the flags from the tracelog.
                // We can't safely know though, so, unless they've ALREADY been marked as Operands, let's
                // just play it safe and stop at the first thing that's NOT an Operand.
                //
                // Calling code should ideally not let us get to here, and instead supply us with a valid instructionByteLen
                return Data.GetFlag(pc) == Data.FlagType.Operand;
            }
        }

        private void LogStats(int pc, int dataBank, int directPage, bool xflagSet, bool mflagSet, Data.FlagType flagType)
        {
            var mMarks = Data.GetFlag(pc) != flagType;
            var mDb = Data.GetDataBank(pc) != dataBank;
            var mDp = Data.GetDirectPage(pc) != directPage;
            var mX = Data.GetXFlag(pc) != xflagSet;
            var mM = Data.GetMFlag(pc) != mflagSet;

            if (mMarks || mDb || mDp || mX || mM)
                CurrentStats.numRomBytesModified++;

            CurrentStats.numMarksModified += mMarks ? 1 : 0;
            CurrentStats.numDBModified += mDb ? 1 : 0;
            CurrentStats.numDpModified += mDp ? 1 : 0;
            CurrentStats.numXFlagsModified += mX ? 1 : 0;
            CurrentStats.numMFlagsModified += mM ? 1 : 0;
        }
    }
}
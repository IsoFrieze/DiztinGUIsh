using System;
using System.Diagnostics;
using System.IO;
using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Core.import
{
    public class BsnesTraceLogImporter
    {
        private Data data;
        int romSize;
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

        public Stats CurrentStats;

        public Data GetData()
        {
            return data;
        }

        public BsnesTraceLogImporter(Data data)
        {
            this.data = data;
            romSize = this.data.GetRomSize();

            CurrentStats.NumRomBytesAnalyzed = 0;
            CurrentStats.NumRomBytesModified = 0;
            CurrentStats.NumXFlagsModified = 0;
            CurrentStats.NumMFlagsModified = 0;
            CurrentStats.NumDbModified = 0;
            CurrentStats.NumDpModified = 0;
            CurrentStats.NumMarksModified = 0;
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
                Addr,
                D,
                Db,
                Flags,
                FN,
                FV,
                FM,
                FX,
                FD,
                FI,
                FZ,
                FC;

            public CachedTraceLineIndex()
            {
                int SkipToken(string token)
                {
                    return sample.IndexOf(token) + token.Length;
                }

                Addr = 0;
                D = SkipToken("D:");
                Db = SkipToken("DB:");
                Flags = Db + 3;

                // flags: nvmxdizc
                FN = Flags + 0;
                FV = Flags + 1;
                FM = Flags + 2;
                FX = Flags + 3;
                FD = Flags + 4;
                FI = Flags + 5;
                FZ = Flags + 6;
                FC = Flags + 7;
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
            var dataBank = GetHexValueAt(CachedIdx.Db, 2);

            // 'X' = unchecked in bsnesplus debugger UI = (8bit), 'x' or '.' = checked (16bit)
            var xflagSet = line[CachedIdx.FX] == 'X';

            // 'M' = unchecked in bsnesplus debugger UI = (8bit), 'm' or '.' = checked (16bit)
            var mflagSet = line[CachedIdx.FM] == 'M';

            SetOpcodeAndOperandsFromTraceData(snesAddress, dataBank, directPage, xflagSet, mflagSet);
        }

        // this is same as above but, reads the same data from a binary format. this is for
        // performance reasons to try and stream the data live from BSNES
        public void ImportTraceLogLineBinary(byte[] bytes, bool abridgedFormat=true)
        {
            // extremely performance-intensive function. be really careful when adding stuff

            if (abridgedFormat) {
                Debug.Assert(bytes.Length == 8);
            } else {
                Debug.Assert(bytes.Length == 21);
            }

            var pointer = 0;

            // -----------------------------
            var snesAddress = ByteUtil.ByteArrayToInt24(bytes, pointer);
            pointer += 3;

            var opcodeLen = bytes[pointer++];

            var directPage = ByteUtil.ByteArrayToInt16(bytes, pointer);
            pointer += 2;

            var dataBank = bytes[pointer++];

            // the flags register
            var flags = bytes[pointer++];
            // n = flags & 0x80;
            // v = flags & 0x40;
            // m = flags & 0x20;
            // d = flags & 0x08;
            // i = flags & 0x04;
            // z = flags & 0x02;
            // c = flags & 0x01;

            // we only care about X and M flags
            var xflagSet = (flags & 0x10) != 0;
            var mflagSet = (flags & 0x20) != 0;

            if (!abridgedFormat)
            {
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

                // skip, flag 'e' for emulation mode or not
                // skip E(emu) flag <-- NOTE: we might... want this someday.
                pointer += 1;
            }

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

                snesAddress = RomUtil.CalculateSnesOffsetWithWrap(snesAddress, 1);
                currentIndex++;
            }
        }

        private bool SetOneRomByteFromTraceData(int snesAddress, int dataBank, int directPage, bool xflagSet, bool mflagSet,
            int opcodeLen, int currentIndex)
        {
            CurrentStats.NumRomBytesAnalyzed++;

            var pc = data.ConvertSnesToPc(snesAddress);
            if (!IsOkToSetThisRomByte(pc, currentIndex, opcodeLen)) 
                return false;

            var flagType = currentIndex == 0 ? Data.FlagType.Opcode : Data.FlagType.Operand;

            LogStats(pc, dataBank, directPage, xflagSet, mflagSet, flagType);

            // TODO: it doesn't hurt anything but, banks come back mirrored sometimes,
            // would be cool to find a way to do the right thing and deal with the mirroring so we're not
            // flipping and re-flipping bytes, but still maintain correctness with the game itself.

            // actually do the update
            data.SetFlag(pc, flagType);
            data.SetDataBank(pc, dataBank);
            data.SetDirectPage(pc, directPage);
            data.SetXFlag(pc, xflagSet);
            data.SetMFlag(pc, mflagSet);

            return true;
        }

        private bool IsOkToSetThisRomByte(int pc, int opIndex, int instructionByteLen)
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
                return data.GetFlag(pc) == Data.FlagType.Operand;
            }
        }

        private void LogStats(int pc, int dataBank, int directPage, bool xflagSet, bool mflagSet, Data.FlagType flagType)
        {
            var mMarks = data.GetFlag(pc) != flagType;
            var mDb = data.GetDataBank(pc) != dataBank;
            var mDp = data.GetDirectPage(pc) != directPage;
            var mX = data.GetXFlag(pc) != xflagSet;
            var mM = data.GetMFlag(pc) != mflagSet;

            if (mMarks || mDb || mDp || mX || mM)
                CurrentStats.NumRomBytesModified++;

            CurrentStats.NumMarksModified += mMarks ? 1 : 0;
            CurrentStats.NumDbModified += mDb ? 1 : 0;
            CurrentStats.NumDpModified += mDp ? 1 : 0;
            CurrentStats.NumXFlagsModified += mX ? 1 : 0;
            CurrentStats.NumMFlagsModified += mM ? 1 : 0;
        }
    }
}
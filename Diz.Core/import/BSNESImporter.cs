using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Core.import
{
    public class BSNESUsageMapImporter
    {
        // TODO: move BsnesPlusUsage stuff to its own class outside of Data
        [Flags]
        public enum BsnesPlusUsage : byte
        {
            UsageRead = 0x80,
            UsageWrite = 0x40,
            UsageExec = 0x20,
            UsageOpcode = 0x10,
            UsageFlagM = 0x02,
            UsageFlagX = 0x01,
        };

        // move out of here to extension method or just external method
        public int ImportUsageMap(byte[] usageMap, Data data)
        {
            int size = data.GetROMSize();
            int modified = 0;
            int prevFlags = 0;

            for (int map = 0; map <= 0xFFFFFF; map++)
            {
                var i = data.ConvertSNEStoPC(map);

                if (i == -1 || i >= size)
                {
                    // branch predictor may optimize this
                    continue;
                }

                var flags = (BsnesPlusUsage) usageMap[map];

                if (flags == 0)
                {
                    // no information available
                    continue;
                }

                if (data.GetFlag(i) != Data.FlagType.Unreached)
                {
                    // skip if there is something already set..
                    continue;
                }

                // opcode: 0x30, operand: 0x20
                if (flags.HasFlag(BsnesPlusUsage.UsageExec))
                {
                    data.SetFlag(i, Data.FlagType.Operand);

                    if (flags.HasFlag(BsnesPlusUsage.UsageOpcode))
                    {
                        prevFlags = ((int) flags & 3) << 4;
                        data.SetFlag(i, Data.FlagType.Opcode);
                    }

                    data.SetMXFlags(i, prevFlags);
                    modified++;
                }
                else if (flags.HasFlag(BsnesPlusUsage.UsageRead))
                {
                    data.SetFlag(i, Data.FlagType.Data8Bit);
                    modified++;
                }
            }

            return modified;
        }
    }

    public class BSNESTraceLogImporter
    {
        private readonly Data Data;
        int romSize;

        public BSNESTraceLogImporter(Data data)
        {
            Data = data;
            romSize = Data.GetROMSize();
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

        public (int numChanged, int numLinesAnalyzed) ImportTraceLogLine(string line)
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
                return (0,0);

            int snesAddress = GetHexValueAt(0, 6);
            int pc = Data.ConvertSNEStoPC(snesAddress);
            if (pc == -1)
                return (0, 0);

            // TODO: error treatment / validation

            int directPage = GetHexValueAt(CachedIdx.D, 4);
            int dataBank = GetHexValueAt(CachedIdx.DB, 2);

            // 'X' = unchecked in bsnesplus debugger UI = (8bit), 'x' or '.' = checked (16bit)
            bool xflag_set = line[CachedIdx.f_X] == 'X';

            // 'M' = unchecked in bsnesplus debugger UI = (8bit), 'm' or '.' = checked (16bit)
            bool mflag_set = line[CachedIdx.f_M] == 'M';

            return SetFromTraceData(pc, dataBank, directPage, xflag_set, mflag_set);
        }

        // this is same as above but, reads the same data from a binary format. this is for
        // performance reasons to try and stream the data live from BSNES
        public (int numChanged, int numLinesAnalyzed) ImportTraceLogLineBinary(byte[] bytes)
        {
            // extremely performance-intensive function. be really careful when adding stuff

            Debug.Assert(bytes.Length == 22);
            var pointer = 0;

            // -----------------------------

            var watermark = bytes[pointer++];
            if (watermark != 0xEE)
                return (0, 0);

            var snesAddress = ByteUtil.ByteArrayToInt24(bytes, pointer);
            pointer += 3;

            var pc = Data.ConvertSNEStoPC(snesAddress);
            if (pc == -1)
                return (0, 0);

            var opcodeLen = bytes[pointer++];

            // skip opcodes. NOTE: must read all 5 butes but only use up to 'opcode_len' bytes 
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

            var directPage = ByteUtil.ByteArrayToInt24(bytes, pointer);
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

            return SetFromTraceData(pc, dataBank, directPage, xflagSet, mflagSet, opcodeLen);
        }

        // returns the number of bytes actually changed
        private (int numChanged, int numLinesAnalyzed) SetFromTraceData(int pc, int dataBank, int directPage, bool xflagSet, bool mflagSet,
            int opcodeLen = -1)
        {
            // extremely performance-intensive function. be really careful when adding stuff

            // set this data for us and any following operands
            var currentIndex = 0;
            var totalModified = 0;
            bool keepGoing;

            do {
                var flagType = currentIndex == 0 ? Data.FlagType.Opcode : Data.FlagType.Operand;

                var modified = false;

                modified |= Data.GetFlag(pc) != flagType;
                Data.SetFlag(pc, flagType);

                modified |= Data.GetDataBank(pc) != dataBank;
                Data.SetDataBank(pc, dataBank);

                modified |= Data.GetDirectPage(pc) != directPage;
                Data.SetDirectPage(pc, directPage);

                modified |= Data.GetXFlag(pc) != xflagSet;
                Data.SetXFlag(pc, xflagSet);

                modified |= Data.GetMFlag(pc) != mflagSet;
                Data.SetMFlag(pc, mflagSet);

                if (modified)
                    totalModified++;

                pc++; // note: should we check for crossing banks? probably should stop there.
                currentIndex++;

                if (opcodeLen != -1)
                {
                    // we know the # of bytes that should follow our opcode, so mark that many bytes
                    keepGoing = currentIndex < opcodeLen;
                }
                else
                {
                    // only continue if the next bytes were already marked as operands
                    keepGoing = Data.GetFlag(pc) == Data.FlagType.Operand;
                }
            } while (pc < romSize && keepGoing);

            return (totalModified, currentIndex);
        }
    }
}

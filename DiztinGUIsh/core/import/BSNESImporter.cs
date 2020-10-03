using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiztinGUIsh.core.import
{
    class BSNESUsageMapImporter
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

    class BSNESTraceLogImporter
    {
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
                D, DB,
                flags,
                f_N, f_V, f_M, f_X, f_D, f_I, f_Z, f_C;

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

        public int ImportTraceLogLine(string line, Data data)
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
                return 0;

            int snesAddress = GetHexValueAt(0, 6);
            int pc = data.ConvertSNEStoPC(snesAddress);
            if (pc == -1)
                return 0;

            // TODO: error treatment / validation

            int directPage = GetHexValueAt(CachedIdx.D, 4);
            int dataBank = GetHexValueAt(CachedIdx.DB, 2);

            // 'X' = unchecked in bsnesplus debugger UI = (8bit), 'x' or '.' = checked (16bit)
            bool xflag_set = line[CachedIdx.f_X] == 'X';

            // 'M' = unchecked in bsnesplus debugger UI = (8bit), 'm' or '.' = checked (16bit)
            bool mflag_set = line[CachedIdx.f_M] == 'M';

            data.SetFlag(pc, Data.FlagType.Opcode);

            int modified = 0;
            int size = data.GetROMSize();
            do
            {
                data.SetDataBank(pc, dataBank);
                data.SetDirectPage(pc, directPage);
                data.SetXFlag(pc, xflag_set);
                data.SetMFlag(pc, mflag_set);

                pc++;
                modified++;
            } while (pc < size && data.GetFlag(pc) == Data.FlagType.Operand);

            return modified;
        }
    }
}

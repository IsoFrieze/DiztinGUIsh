using System;
using Diz.Core.model;

namespace Diz.Core.import
{
    public class BsnesUsageMapImporter
    {
        [Flags]
        private enum BsnesPlusUsage : byte
        {
            UsageRead = 0x80,
            UsageWrite = 0x40,
            UsageExec = 0x20,
            UsageOpcode = 0x10,
            UsageFlagM = 0x02,
            UsageFlagX = 0x01,
        }
        
        private int prevFlags;
        private byte[] usageMap;
        private Data data;

        public static int ImportUsageMap(byte[] usageMap, Data data)
        {
            return new BsnesUsageMapImporter { usageMap = usageMap, data = data }.Run();
        }
        
        private int Run()
        {
            prevFlags = 0;

            var modified = 0;
            for (var snesOffset = 0; snesOffset <= 0xFFFFFF; snesOffset++)
            {
                if (ProcessUsageMapAddress(snesOffset))
                    modified++;
            }

            return modified;
        }

        // return true if we modified this address
        private bool ProcessUsageMapAddress(int snesOffset)
        {
            var pc = data.ConvertSnesToPc(snesOffset);

            // branch predictor may optimize this
            if (pc == -1 || pc >= data.GetRomSize())
                return false;

            var flags = (BsnesPlusUsage)usageMap[snesOffset];

            // no information available
            if (flags == 0)
                return false;

            // skip if there is something already set..
            if (data.GetFlag(pc) != FlagType.Unreached)
                return false;

            // opcode: 0x30, operand: 0x20
            if (flags.HasFlag(BsnesPlusUsage.UsageExec))
            {
                data.SetFlag(pc, FlagType.Operand);

                if (flags.HasFlag(BsnesPlusUsage.UsageOpcode))
                {
                    prevFlags = ((int) flags & 3) << 4;
                    data.SetFlag(pc, FlagType.Opcode);
                }

                data.SetMxFlags(pc, prevFlags);
                return true;
            }
            
            if (flags.HasFlag(BsnesPlusUsage.UsageRead))
            {
                data.SetFlag(pc, FlagType.Data8Bit);
                return true;
            }

            return false;
        }
    }
}

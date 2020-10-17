using System;
using System.Runtime.ExceptionServices;
using Diz.Core.model;

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
}

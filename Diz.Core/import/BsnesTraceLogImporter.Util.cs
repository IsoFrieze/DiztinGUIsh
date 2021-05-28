using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Core.import
{
    public partial class BsnesTraceLogImporter
    {
        private int ConvertSnesToPc(int modDataSnesAddress)
        {
            // PERF: could use Data.ConvertSnesToPc(), but, by caching the two variables here,
            // we can save some locking and maybe speed things up.
            return RomUtil.ConvertSnesToPc(modDataSnesAddress, romMapModeCached, romSizeCached);
        }

        private static int GetNextSnesAddress(int modDataSnesAddress)
        {
            return RomUtil.CalculateSnesOffsetWithWrap(modDataSnesAddress, 1);
        }

        private static FlagType GetFlagForInstructionPosition(int currentIndex)
        {
            return currentIndex == 0 ? FlagType.Opcode : FlagType.Operand;
        }

        private void UpdatePcAddress(ModificationData modData)
        {
            modData.Pc = ConvertSnesToPc(modData.SnesAddress);
        }
    }
}
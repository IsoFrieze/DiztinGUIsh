using Diz.Core.model;
using Diz.Cpu._65816;

namespace Diz.Import.bsnes.usagemap;

public class BsnesUsageMapImporter
{
    [Flags]
    private enum BsnesPlusUsage : byte
    {
        UsageRead   = 0x80,
        UsageWrite  = 0x40,
        UsageExec   = 0x20,
        
        UsageOpcode = 0x10,
        UsageFlagE  = 0x04,
        UsageFlagM  = 0x02,
        UsageFlagX  = 0x01,
    }

    // the data we're importing from BSNES
    private readonly byte[] usageMap;
    
    // our existing Diz project
    private readonly ISnesData snesData;
    
    // if true, we'll only change things marked already as unreached.
    // this is the safest way to go but, turning it off has a chance of correcting desync'd manual assembly,
    // and that's a good thing.
    private readonly bool onlyMarkIfUnreached;

    public BsnesUsageMapImporter(byte[] usageMap, ISnesData snesData, bool onlyMarkIfUnreached = false)
    {
        this.usageMap = usageMap;
        this.snesData = snesData;
        this.onlyMarkIfUnreached = onlyMarkIfUnreached;
    }

    public int Run()
    {
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
        var pc = snesData.ConvertSnesToPc(snesOffset);

        // branch predictor may optimize this
        if (pc == -1 || pc >= snesData.GetRomSize())
            return false;

        var bsnesByteFlags = (BsnesPlusUsage)usageMap[snesOffset];

        // no information available
        if (bsnesByteFlags == 0)
            return false;

        var existingDizByteType = snesData.GetFlag(pc);
        
        if (onlyMarkIfUnreached && existingDizByteType != FlagType.Unreached)
            return false;

        var changed = false;
        
        // theoretically, these can overlap too (Read + Exec an opcode)
        // we prioritize marking bytes as code vs data
        if (bsnesByteFlags.HasFlag(BsnesPlusUsage.UsageExec))
        {
            if (bsnesByteFlags.HasFlag(BsnesPlusUsage.UsageOpcode))
            {
                if (existingDizByteType != FlagType.Opcode)
                    changed = true;

                snesData.MarkAsOpcodeAndOperandsStartingAt(
                    offset: pc,
                    xFlag: bsnesByteFlags.HasFlag(BsnesPlusUsage.UsageFlagX),
                    mFlag: bsnesByteFlags.HasFlag(BsnesPlusUsage.UsageFlagM)
                );
            }
            else
            {
                // it's an operand.
                // theoretically, it should be covered by the previous opcode calling MarkAsOpcodeAndOperandsStartingAt().
                // we'll set it again just in case.
                if (existingDizByteType != FlagType.Operand)
                    changed = true;
                
                // note: MX flags not given to use by BSNES for this,
                // we only get them for the opcode that came before this.
            }
        }
        else if (bsnesByteFlags.HasFlag(BsnesPlusUsage.UsageRead))
        {
            if (existingDizByteType == FlagType.Unreached)
            {
                snesData.SetFlag(pc, FlagType.Data8Bit);
                changed = true;
            }
        }
        
        return changed;
    }
}
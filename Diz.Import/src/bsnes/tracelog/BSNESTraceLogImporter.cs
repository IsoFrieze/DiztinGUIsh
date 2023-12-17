using Diz.Core.model;
using Diz.Cpu._65816;

namespace Diz.Import.bsnes.tracelog;

public partial class BsnesTraceLogImporter
{
    private readonly ISnesData? snesData = null;

    // these are cached mostly to save us from having to lock Data.
    // do not allow these to change over the life of the importer
    private readonly int romSizeCached;
    private readonly RomMapMode romMapModeCached;

    public BsnesTraceLogImporter(ISnesData? snesData)
    {
        this.snesData = snesData;
        romSizeCached = this.snesData?.GetRomSize() ?? 0;
        romMapModeCached = this.snesData?.RomMapMode ?? default;
        
        modificationDataPool = new ObjPool<ModificationData>();
        InitStats();
    }

    // Mark collected trace data for a RomByte (which should be an opcode) AND any of the operands that follow us.
    private void SetOpcodeAndOperandsFromTraceData(ModificationData modData, int instructionByteLen = -1) 
    {
        // extremely performance-intensive function. be really careful when adding stuff
        var currentOffset = 0;
        var numBytesAnalyzed = 0;
            
        bool Prep(ModificationData modData2)
        {
            numBytesAnalyzed++;
            UpdatePCAddress(modData2);
            if (!IsOkToSetThisRomByte(modData2.Pc, instructionByteLen, currentOffset)) 
                return false;
                
            modData2.FlagType = GetFlagForInstructionPosition(currentOffset);
            return true;
        }

        while (Prep(modData))
        {
            ApplyModification(modData);

            modData.SnesAddress = GetNextSNESAddress(modData.SnesAddress);
            currentOffset++;
        }
            
        FreeModificationData(ref modData); // sets to Null after called

        currentStats.NumRomBytesAnalyzed += numBytesAnalyzed;
    }

    private bool IsOkToSetThisRomByte(int pc, int instructionByteLen, int opIndex)
    {
        if (pc < 0 || pc >= romSizeCached)
            return false;

        ValidateInstructionByteLen(instructionByteLen); 
        if (opIndex is < 0 or > 4) // yes, using 4 here, not 3.  calling code will deal with invalid '4' case
        {
            throw new InvalidDataException($"Invalid opcode index {opIndex}. Must be between 0 and 4");
        }

        if (instructionByteLen != -1)
        {
            // just make sure we're in range if we know the amount of bytes we need to process
            return opIndex < instructionByteLen;
        }
            
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

        return GetFlag(pc) == FlagType.Operand;
    }

    private FlagType GetFlag(int pc)
    {
        snesData.Data.RomBytes[pc].Lock.EnterReadLock();
        try
        {
            return snesData.GetFlag(pc);
        }
        finally
        {
            snesData.Data.RomBytes[pc].Lock.ExitReadLock();       
        }
    }

    private static void ValidateInstructionByteLen(int instructionByteLen)
    {
        if (instructionByteLen != -1 && (instructionByteLen < 1 || instructionByteLen > 4))
        {
            throw new InvalidDataException(
                $"Invalid opcode+operand byte length {instructionByteLen}. Must be -1, or between 1 and 4");
        }
    }
}
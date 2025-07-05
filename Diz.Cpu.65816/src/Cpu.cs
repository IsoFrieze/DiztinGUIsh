using Diz.Core.Interfaces;
using Diz.Core.model;

namespace Diz.Cpu._65816;

public class Cpu<TByteSource> where TByteSource : IRomByteFlagsGettable, IRomByteFlagsSettable, ISnesAddressConverter, ISteppable 
{
    /// <summary>
    /// Interpret the bytes at Offset as an opcode + optional operands. Mark them as such,
    /// and return the next offset (which should, if everything went well, be another instruction)
    /// 
    /// </summary>
    /// <param name="data">bytesource to operate on</param>
    /// <param name="offset">starting offset in bytesource to operate on</param>
    /// <param name="branch">if true, and the instruction at offset is a branch, take the branch</param>
    /// <param name="force">
    /// if true, ignore control flow statements (branches, returns, etc), and set next offset right after.
    /// DANGEROUS and can cause you to run off the edge of a block of code and into data. usually avoid, unless you know what you're asking for</param>
    /// <param name="prevOffset">
    /// If available, set this to the offset of the instruction located before this one.
    /// Cpu state data like M,X,DB, and DP flags will be copied from the previous instruction.
    /// </param>
    /// <returns>The offset of the instruction located after this one, or returns the same offset if the Step operation failed or is not supported.</returns>
    public virtual int Step(TByteSource data, int offset, bool branch, bool force, int prevOffset = -1) => offset;
    
    
    public virtual int GetInstructionLength(TByteSource data, int offset) => 1;
    public virtual int GetIntermediateAddress(TByteSource data, int offset, bool resolve) => -1;
    public virtual void MarkInOutPoints(TByteSource data, int offset) {} // nop
    public virtual int CalculateInOutPointsFromOffset(
        TByteSource data,
        int offset,
        out InOutPoint newIaInOutPoint,
        out InOutPoint newOffsetInOutPoint
    )
    {
        newIaInOutPoint = InOutPoint.None;
        newOffsetInOutPoint = InOutPoint.None;
        return -1;
    }

    public virtual string GetInstructionStr(TByteSource data, int offset) => "";
    
    public virtual CpuInstructionDataFormatted GetInstructionData(TByteSource data, int offset) => new();
    
    public virtual int AutoStepSafe(TByteSource byteSource, int offset) => offset;

    public int AutoStepHarsh(TByteSource byteSource, int offset, int amount)
    {
        var newOffset = offset;
        var prevOffset = offset - 1;

        while (newOffset < offset + amount)
        {
            var nextOffset = byteSource.Step(newOffset, false, true, prevOffset);
            prevOffset = newOffset;
            newOffset = nextOffset;
        }

        return newOffset;
    }
}

public class CpuSpc700<TByteSource> : Cpu<TByteSource> where TByteSource : IRomByteFlagsGettable, IRomByteFlagsSettable, ISnesAddressConverter, ISteppable
{
    // implement me       
}
    
public class CpuSuperFx<TByteSource> : Cpu<TByteSource> where TByteSource : IRomByteFlagsGettable, IRomByteFlagsSettable, ISnesAddressConverter, ISteppable
{
    // implement me
}
using Diz.Core.Interfaces;
using Diz.Core.model;

namespace Diz.Cpu._65816;

// public class Cpu<TByteSource> where TByteSource : IRomByteFlagsGettable, IRomByteFlagsSettable, ISnesAddressConverter, ISteppable
public abstract class Cpu
{
    
    // public virtual int Step(TByteSource data, int offset, bool branch, bool force, int prevOffset = -1) => offset;
    
    //
    // public virtual int GetInstructionLength(TByteSource data, int offset) => 1;
    // public virtual int GetIntermediateAddress(TByteSource data, int offset, bool resolve) => -1;
    // public virtual void MarkInOutPoints(TByteSource data, int offset) {} // nop
    // public virtual int CalculateInOutPointsFromOffset(
    //     TByteSource data,
    //     int offset,
    //     out InOutPoint newIaInOutPoint,
    //     out InOutPoint newOffsetInOutPoint
    // )
    // {
    //     newIaInOutPoint = InOutPoint.None;
    //     newOffsetInOutPoint = InOutPoint.None;
    //     return -1;
    // }

    // public virtual string GetInstructionStr(TByteSource data, int offset, bool showMnemonicHint) => "";
    //
    // public virtual CpuInstructionDataFormatted GetInstructionData<TData>(TData data, int offset, bool showMnemonicHint) => new();
    //
    // public virtual int AutoStepSafe(TByteSource byteSource, int offset) => offset;

    public static int AutoStepHarsh<TByteSource>(TByteSource byteSource, int offset, int amount) where TByteSource : IRomByteFlagsGettable, IRomByteFlagsSettable, ISteppable 
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
//
// public class CpuSpc700<TByteSource> : Cpu<TByteSource> where TByteSource : IRomByteFlagsGettable, IRomByteFlagsSettable, ISnesAddressConverter, ISteppable
// {
//     // implement me       
// }
//     
// public class CpuSuperFx<TByteSource> : Cpu<TByteSource> where TByteSource : IRomByteFlagsGettable, IRomByteFlagsSettable, ISnesAddressConverter, ISteppable
// {
//     // implement me
// }
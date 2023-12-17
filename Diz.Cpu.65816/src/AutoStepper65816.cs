using Diz.Core.Interfaces;
using Diz.Core.model;

namespace Diz.Cpu._65816;

public class AutoStepper65816<TDataSource> where TDataSource : IRomByteFlagsGettable, IRomByteFlagsSettable, IReadOnlyByteSource, ISteppable
{
    public int Offset { get; private set; }
    private int prevOffset = -1;

    private readonly TDataSource byteSource;
    
    private readonly List<int> seenBranches;
    private readonly Stack<int> stack;

    public AutoStepper65816(TDataSource byteSource)
    {
        this.byteSource = byteSource;
        
        stack = new Stack<int>();
        seenBranches = new List<int>();
    }

    public void Run(int offset)
    {
        Offset = offset;
        prevOffset = offset - 1;

        while (ProcessAutoStepOne() && CanAutoStepAtCurrentOffset()) {}
    }

    private bool CanAutoStepAtCurrentOffset() => 
        byteSource.GetFlag(Offset) is FlagType.Unreached or FlagType.Opcode or FlagType.Operand;
    
    /// <summary>
    /// Complete one step in Safe AutoStep.
    /// modifies internal state and sets Offset to the next state required
    /// </summary>
    /// <returns>true if we should continue autostepping</returns>
    private bool ProcessAutoStepOne()
    {
        if (seenBranches.Contains(Offset))
            return false;

        var romByte = byteSource.GetRomByte(Offset);
        if (romByte == null)
            return false;
        
        var opcode = (byte)romByte;
        
        RememberIfBranch(opcode);
        
        if (!HandleOpcode(opcode))
            return false;

        var shouldContinue =
            opcode is not (
                0x40 or 0xCB or 0xDB or 0xF8 or // RTI WAI STP SED
                0xFB or 0x00 or 0x02 or 0x42 or // XCE BRK COP WDM
                0x6C or 0x7C or 0xDC or 0xFC    // JMP JMP JML JSR
                );

        return shouldContinue;
    }

    private (int nextOffsetIfNoJump, int nextOffsetIfJumped) StepAndMarkAllBranchesFromOffset()
    {
        // from our current offset, step (marks things) and get the offset for what happens if we're:
        // 1. at a branch and we take it
        // 2. at a branch and we don't take it
        // if we're not at a branch, this just grabs the next instruction normally
        
        // note: we don't actually modify Offset here to follow the jump. we're just using it to:
        // 1. peek ahead and see where we'd land if we did take vs not take the branch 
        // 2. mark the next instructions on either side of the branch as opcodes/operands
        
        return (
            nextOffsetIfNoJump: byteSource.Step(Offset, false, false, prevOffset),
            nextOffsetIfJumped: byteSource.Step(Offset, true, false, prevOffset)
        );
    }

    private bool HandleOpcode(byte opcode)
    {
        var (nextOffsetIfNoJump, nextOffsetIfJumped) = StepAndMarkAllBranchesFromOffset();

        if (!PushOrPopMxFlags(opcode)) 
            return false;

        switch (opcode) 
        {
            // RTS RTL
            case 0x60: case 0x6B:
                if (stack.Count == 0)
                    return false;

                AdvanceOffsetTo(stack.Pop());
                return true;

            // JSR JSL
            case 0x20: case 0x22:
                stack.Push(nextOffsetIfNoJump);
                AdvanceOffsetTo(nextOffsetIfJumped);
                return true;

            default:
                AdvanceOffsetTo(nextOffsetIfNoJump);
                return true;
        }
    }

    private void AdvanceOffsetTo(int offset)
    {
        prevOffset = Offset;
        Offset = offset;
    }

    private bool PushOrPopMxFlags(byte opcode)
    {
        if (opcode == 0x08)
        {
            // PHP
            stack.Push(byteSource.GetMxFlags(Offset));
        } 
        else if (opcode == 0x28)
        {
            // PLP
            if (stack.Count == 0)
                return false;

            byteSource.SetMxFlags(Offset, stack.Pop());
        }

        return true;
    }

    private void RememberIfBranch(byte opcode)
    {
        var opcodeIsBranch =
            opcode is
                0x4C or 0x5C or 0x80 or 0x82 or // JMP JML BRA BRL
                0x10 or 0x30 or 0x50 or 0x70 or // BPL BMI BVC BVS
                0x90 or 0xB0 or 0xD0 or 0xF0; // BCC BCS BNE BEQ

        if (opcodeIsBranch)
            seenBranches.Add(Offset);
    }
}
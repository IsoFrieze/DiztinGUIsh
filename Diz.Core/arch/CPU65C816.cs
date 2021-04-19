using System.Collections.Generic;
using System.IO;
using Diz.Core.export;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.model.snes;
using Diz.Core.util;

namespace Diz.Core.arch
{
    public class CpuDispatcher
    {
        private Cpu cpuDefault;
        private Cpu65C816 cpu65C816;
        private CpuSpc700 cpuSpc700;
        private CpuSuperFx cpuSuperFx;

        public Cpu Cpu(Data data, int offset)
        {
            var arch = data.GetArchitecture(offset);

            return arch switch
            {
                Architecture.Cpu65C816 => cpu65C816 ??= new Cpu65C816(),
                Architecture.Apuspc700 => cpuSpc700 ??= new CpuSpc700(),
                Architecture.GpuSuperFx => cpuSuperFx ??= new CpuSuperFx(),
                _ => cpuDefault ??= new Cpu()
            };
        }
    }

    public class Cpu
    {
        public virtual int Step(Data data, int offset, bool branch, bool force, int prevOffset) => offset;
        public virtual int GetInstructionLength(Data data, int offset) => 1;
        public virtual int GetIntermediateAddress(Data data, int offset, bool resolve) => -1;
        public virtual void MarkInOutPoints(Data data, int offset) {} // nop
        public virtual string GetInstruction(Data data, int offset) => "";

        // TODO: cleanup this function signature
        protected virtual bool DoOneAutoStepNormal(ICpuOperableByteSource byteSource, List<int> seenBranches, ref int newOffset, out int nextOffset, ref int prevOffset, Stack<int> stack)
        {
            nextOffset = -1;
            return false;
        }
        
        public int AutoStep(ICpuOperableByteSource byteSource, int offset, bool harsh, int amount)
        {
            return harsh
                ? AutoStepHarsh(byteSource, offset, amount)
                : AutoStepNormal(byteSource, offset);
        }

        private int AutoStepNormal(ICpuOperableByteSource byteSource, int offset)
        {
            var newOffset = offset;
            var prevOffset = newOffset - 1;
            
            var stack = new Stack<int>();
            var seenBranches = new List<int>();
            var keepGoing = true;

            while (keepGoing)
            {
                keepGoing = DoOneAutoStepNormal(byteSource, seenBranches, ref newOffset, out _, ref prevOffset, stack);

                var flag = byteSource.GetFlag(newOffset);
                if (!(flag == FlagType.Unreached || flag == FlagType.Opcode || flag == FlagType.Operand)) 
                    keepGoing = false;
            }

            return newOffset;
        }

        private int AutoStepHarsh(ICpuOperableByteSource byteSource, int offset, int amount)
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

    // a base Cpu for common things for real but mostly placeholder CPU types.
    public abstract class CpuGenericHelper : Cpu
    {
        protected override bool DoOneAutoStepNormal(ICpuOperableByteSource byteSource, List<int> seenBranches, ref int newOffset, out int nextOffset, ref int prevOffset, Stack<int> stack)
        {
            nextOffset = byteSource.Step(newOffset, false, true, prevOffset);
            prevOffset = newOffset;
            newOffset = nextOffset;
            return true;
        }
    }

    public class CpuSpc700 : CpuGenericHelper
    {
        
    }
    
    public class CpuSuperFx : CpuGenericHelper
    {
        
    }
    
    public class Cpu65C816 : Cpu
    {
        public override int Step(Data data, int offset, bool branch, bool force, int prevOffset)
        {
            var opcode = data.GetRomByte(offset);
            var prevDirectPage = data.GetDirectPage(offset);
            var prevDataBank = data.GetDataBank(offset);
            bool prevX = data.GetXFlag(offset), prevM = data.GetMFlag(offset);

            while (prevOffset >= 0 && data.GetFlag(prevOffset) == FlagType.Operand) prevOffset--;
            if (prevOffset >= 0 && data.GetFlag(prevOffset) == FlagType.Opcode)
            {
                prevDirectPage = data.GetDirectPage(prevOffset);
                prevDataBank = data.GetDataBank(prevOffset);
                prevX = data.GetXFlag(prevOffset);
                prevM = data.GetMFlag(prevOffset);
            }

            if (opcode == 0xC2 || opcode == 0xE2) // REP SEP
            {
                prevX = (data.GetRomByte(offset + 1) & 0x10) != 0 ? opcode == 0xE2 : prevX;
                prevM = (data.GetRomByte(offset + 1) & 0x20) != 0 ? opcode == 0xE2 : prevM;
            }

            // set first byte first, so the instruction length is correct
            data.SetFlag(offset, FlagType.Opcode);
            data.SetDataBank(offset, prevDataBank);
            data.SetDirectPage(offset, prevDirectPage);
            data.SetXFlag(offset, prevX);
            data.SetMFlag(offset, prevM);

            var length = GetInstructionLength(data, offset);

            // TODO: I don't think this is handling execution bank boundary wrapping correctly? -Dom
            // If we run over the edge of a bank, we need to go back to the beginning of that bank, not go into
            // the next one.  While this should be really rare, it's technically valid.
            //
            // docs: http://www.6502.org/tutorials/65c816opcodes.html#5.1.2
            // [Note that although the 65C816 has a 24-bit address space, the Program Counter is only a 16-bit register and
            // the Program Bank Register is a separate (8-bit) register. This means that instruction execution wraps at bank
            // boundaries. This is true even if the bank boundary occurs in the middle of the instruction.]
            //
            // TODO: check other areas, anywhere we're accessing a Rom address plus some offset, might need to wrap
            // in most situations.
            for (var i = 1; i < length; i++)
            {
                data.SetFlag(offset + i, FlagType.Operand);
                data.SetDataBank(offset + i, prevDataBank);
                data.SetDirectPage(offset + i, prevDirectPage);
                data.SetXFlag(offset + i, prevX);
                data.SetMFlag(offset + i, prevM);
            }

            MarkInOutPoints(data, offset);

            var nextOffset = offset + length;

            if (force || (opcode != 0x4C && opcode != 0x5C && opcode != 0x80 && opcode != 0x82 && (!branch ||
                (opcode != 0x10 && opcode != 0x30 && opcode != 0x50 && opcode != 0x70 && opcode != 0x90 &&
                 opcode != 0xB0 && opcode != 0xD0 && opcode != 0xF0 && opcode != 0x20 &&
                 opcode != 0x22)))) 
                return nextOffset;

            var iaNextOffsetPc = data.ConvertSnesToPc(GetIntermediateAddress(data, offset, true));
            if (iaNextOffsetPc >= 0) 
                nextOffset = iaNextOffsetPc;

            return nextOffset;
        }

        protected override bool DoOneAutoStepNormal(ICpuOperableByteSource byteSource, List<int> seenBranches, 
            ref int newOffset, out int nextOffset, ref int prevOffset, Stack<int> stack)
        {
            nextOffset = newOffset;
            if (seenBranches.Contains(newOffset))
                return false;

            var opcode = byteSource.GetRomByteUnsafe(newOffset);

            nextOffset = byteSource.Step(newOffset, false, false, prevOffset);
            var jumpOffset = byteSource.Step(newOffset, true, false, prevOffset);

            var shouldStop =
                   opcode == 0x40 || opcode == 0xCB || opcode == 0xDB || opcode == 0xF8     // RTI WAI STP SED
                || opcode == 0xFB || opcode == 0x00 || opcode == 0x02 || opcode == 0x42  // XCE BRK COP WDM
                || opcode == 0x6C || opcode == 0x7C || opcode == 0xDC || opcode == 0xFC; // JMP JMP JML JSR

            if (   opcode == 0x4C || opcode == 0x5C || opcode == 0x80 || opcode == 0x82 // JMP JML BRA BRL
                || opcode == 0x10 || opcode == 0x30 || opcode == 0x50 || opcode == 0x70 // BPL BMI BVC BVS
                || opcode == 0x90 || opcode == 0xB0 || opcode == 0xD0 || opcode == 0xF0 // BCC BCS BNE BEQ
            )
            {
                seenBranches.Add(newOffset);
            }

            switch (opcode)
            {
                // PHP
                case 0x08:
                    stack.Push(byteSource.GetMxFlags(newOffset));
                    break;
                
                // PLP
                case 0x28:
                    if (stack.Count == 0)
                        return false;
                    
                    byteSource.SetMxFlags(newOffset, stack.Pop());
                    break;
                
                // RTS RTL
                case 0x60: case 0x6B:
                    if (stack.Count == 0)
                        return false;

                    prevOffset = newOffset;
                    newOffset = stack.Pop();
                    break;

                    // JSR JSL
                case 0x20: case 0x22:
                    stack.Push(nextOffset);
                    prevOffset = newOffset;
                    newOffset = jumpOffset;
                    break;
                
                default:
                    prevOffset = newOffset;
                    newOffset = nextOffset;
                    break;
            }

            return !shouldStop;
        }

        // input: ROM offset
        // return: a SNES address
        public override int GetIntermediateAddress(Data data, int offset, bool resolve)
        {
            int bank;
            int programCounter;
            
            var byteEntry = GetByteEntryRom(data, offset);
            var opcode = byteEntry?.Byte;
            if (opcode == null)
                return -1;

            var mode = GetAddressMode(data, offset);
            switch (mode)
            {
                case AddressMode.DirectPage:
                case AddressMode.DirectPageXIndex:
                case AddressMode.DirectPageYIndex:
                case AddressMode.DirectPageIndirect:
                case AddressMode.DirectPageXIndexIndirect:
                case AddressMode.DirectPageIndirectYIndex:
                case AddressMode.DirectPageLongIndirect:
                case AddressMode.DirectPageLongIndirectYIndex:
                    if (resolve)
                    {
                        var directPage = data.GetDirectPage(offset);
                        var operand = data.GetRomByte(offset + 1);
                        if (!operand.HasValue)
                            return -1;
                        return (directPage + (int)operand) & 0xFFFF;
                    }
                    else
                    {
                        goto case AddressMode.DirectPageSIndex;
                    }
                case AddressMode.DirectPageSIndex:
                case AddressMode.DirectPageSIndexIndirectYIndex:
                    return data.GetRomByte(offset + 1) ?? -1;
                case AddressMode.Address:
                case AddressMode.AddressXIndex:
                case AddressMode.AddressYIndex:
                case AddressMode.AddressXIndexIndirect:
                {
                    bank = (opcode == 0x20 || opcode == 0x4C || opcode == 0x7C || opcode == 0xFC)
                        ? data.ConvertPCtoSnes(offset) >> 16
                        : data.GetDataBank(offset);
                    var operand = data.GetRomWord(offset + 1);
                    if (!operand.HasValue)
                        return -1;
                    
                    return (bank << 16) | (int)operand;
                }
                case AddressMode.AddressIndirect:
                case AddressMode.AddressLongIndirect:
                {
                    var operand = data.GetRomWord(offset + 1) ?? -1;
                    return operand;
                }
                case AddressMode.Long:
                case AddressMode.LongXIndex:
                {
                    var operand = data.GetRomLong(offset + 1) ?? -1;
                    return operand;
                }
                case AddressMode.Relative8:
                {
                    programCounter = data.ConvertPCtoSnes(offset + 2);
                    bank = programCounter >> 16;
                    var romByte = data.GetRomByte(offset + 1);
                    if (!romByte.HasValue)
                        return -1;
                    
                    return (bank << 16) | ((programCounter + (sbyte)romByte) & 0xFFFF);
                }
                case AddressMode.Relative16:
                {
                    programCounter = data.ConvertPCtoSnes(offset + 3);
                    bank = programCounter >> 16;
                    var romByte = data.GetRomWord(offset + 1);
                    if (!romByte.HasValue)
                        return -1;
                    
                    return (bank << 16) | ((programCounter + (short)romByte) & 0xFFFF);
                }
            }
            return -1;
        }

        // get a compiled byte entry representing all info at an offset contained in any layer.
        // return null if there's no entries in that index
        // input: ROM offset
        // new code should be migrated to use this instead of GetRomByte()
        private static ByteEntry GetByteEntryRom(Data data, int romOffset)
        {
            return GetByteEntrySnes(data, data.ConvertPCtoSnes(romOffset));
        }
        
        // get a compiled byte entry representing all info at an offset contained in any layer.
        // return null if there's no entries in that index
        // input: SNES address
        // new code should be migrated to use this instead of GetSnesByte()
        private static ByteEntry GetByteEntrySnes(Data data, int snesAddress)
        {
            return data.BuildFlatByteEntryForSnes(snesAddress);
        }

        public override string GetInstruction(Data data, int offset)
        {
            var mode = GetAddressMode(data, offset);
            if (mode == null)
                throw new InvalidDataException("Expected non-null mode");
            
            var format = GetInstructionFormatString(data, offset);
            var mnemonic = GetMnemonic(data, offset);
            
            int numDigits1 = 0, numDigits2 = 0;
            int? value1 = null, value2 = null;
            var identified = false;
            
            switch (mode)
            {
                case AddressMode.BlockMove:
                    identified = true;
                    numDigits1 = numDigits2 = 2;
                    value1 = data.GetRomByte(offset + 1);
                    value2 = data.GetRomByte(offset + 2);
                    break;
                case AddressMode.Constant8:
                case AddressMode.Immediate8:
                    identified = true;
                    numDigits1 = 2;
                    value1 = data.GetRomByte(offset + 1);
                    break;
                case AddressMode.Immediate16:
                    identified = true;
                    numDigits1 = 4;
                    value1 = data.GetRomWord(offset + 1);
                    break;
            }

            string op1, op2 = "";
            if (identified)
            {
                op1 = CreateHexStr(value1, numDigits1);
                op2 = CreateHexStr(value2, numDigits2);
            }
            else
            {
                // dom note: this is where we could inject expressions if needed. it gives stuff like "$F001".
                // we could substitute our expression of "$#F000 + $#01" or "some_struct.member" like "player.hp"
                // the expression must be verified to always match the bytes in the file [unless we allow overriding]
                op1 = FormatOperandAddress(data, offset, mode.Value);
            }
            
            return string.Format(format, mnemonic, op1, op2);
        }

        private static string CreateHexStr(int? v, int numDigits)
        {
            if (numDigits == 0)
                return "";

            if (v == null)
                throw new InvalidDataException("Expected non-null input value, got null");
            
            return Util.NumberToBaseString((int) v, Util.NumberBase.Hexadecimal, numDigits, true);
        }

        public override int GetInstructionLength(Data data, int offset)
        {
            var mode = GetAddressMode(data, offset);
            
            // not sure if this is the right thing. probably fine, if we hit this.
            // we're in a weird mess anyway.
            if (mode == null)
                return 1;
            
            return GetInstructionLength(mode.Value);
        }

        public override void MarkInOutPoints(Data data, int offset)
        {
            var opcode = data.GetRomByte(offset);
            var iaOffsetPc = data.ConvertSnesToPc(data.GetIntermediateAddress(offset, true));

            // set read point on EA
            if (iaOffsetPc >= 0 && ( // these are all read/write/math instructions
                ((opcode & 0x04) != 0) || ((opcode & 0x0F) == 0x01) || ((opcode & 0x0F) == 0x03) ||
                ((opcode & 0x1F) == 0x12) || ((opcode & 0x1F) == 0x19)) &&
                (opcode != 0x45) && (opcode != 0x55) && (opcode != 0xF5) && (opcode != 0x4C) &&
                (opcode != 0x5C) && (opcode != 0x6C) && (opcode != 0x7C) && (opcode != 0xDC) && (opcode != 0xFC)
            ) data.SetInOutPoint(iaOffsetPc, InOutPoint.ReadPoint);

            // set end point on offset
            if (opcode == 0x40 || opcode == 0x4C || opcode == 0x5C || opcode == 0x60 // RTI JMP JML RTS
                || opcode == 0x6B || opcode == 0x6C || opcode == 0x7C || opcode == 0x80 // RTL JMP JMP BRA
                || opcode == 0x82 || opcode == 0xDB || opcode == 0xDC // BRL STP JML
            ) data.SetInOutPoint(offset, InOutPoint.EndPoint);

            // set out point on offset
            // set in point on EA
            if (iaOffsetPc >= 0 && (
                opcode == 0x4C || opcode == 0x5C || opcode == 0x80 || opcode == 0x82 // JMP JML BRA BRL
                || opcode == 0x10 || opcode == 0x30 || opcode == 0x50 || opcode == 0x70  // BPL BMI BVC BVS
                || opcode == 0x90 || opcode == 0xB0 || opcode == 0xD0 || opcode == 0xF0  // BCC BCS BNE BEQ
                || opcode == 0x20 || opcode == 0x22)) // JSR JSL
            {
                data.SetInOutPoint(offset, InOutPoint.OutPoint);
                data.SetInOutPoint(iaOffsetPc, InOutPoint.InPoint);
            }
        }

        private static int GetInstructionLength(AddressMode mode)
        {
            switch (mode)
            {
                case AddressMode.Implied:
                case AddressMode.Accumulator:
                    return 1;
                case AddressMode.Constant8:
                case AddressMode.Immediate8:
                case AddressMode.DirectPage:
                case AddressMode.DirectPageXIndex:
                case AddressMode.DirectPageYIndex:
                case AddressMode.DirectPageSIndex:
                case AddressMode.DirectPageIndirect:
                case AddressMode.DirectPageXIndexIndirect:
                case AddressMode.DirectPageIndirectYIndex:
                case AddressMode.DirectPageSIndexIndirectYIndex:
                case AddressMode.DirectPageLongIndirect:
                case AddressMode.DirectPageLongIndirectYIndex:
                case AddressMode.Relative8:
                    return 2;
                case AddressMode.Immediate16:
                case AddressMode.Address:
                case AddressMode.AddressXIndex:
                case AddressMode.AddressYIndex:
                case AddressMode.AddressIndirect:
                case AddressMode.AddressXIndexIndirect:
                case AddressMode.AddressLongIndirect:
                case AddressMode.BlockMove:
                case AddressMode.Relative16:
                    return 3;
                case AddressMode.Long:
                case AddressMode.LongXIndex:
                    return 4;
                default:
                    return 1;
            }
        }

        private string FormatOperandAddress(IReadOnlySnesRom data, int offset, AddressMode mode)
        {
            var address = data.GetIntermediateAddress(offset);
            if (address < 0) 
                return "";

            var label = data.Labels.GetLabelName(address);
            if (label != "") 
                return label;

            var count = BytesToShow(mode);
            if (mode == AddressMode.Relative8 || mode == AddressMode.Relative16)
            {
                var romWord = data.GetRomWord(offset + 1);
                if (!romWord.HasValue)
                    return "";
                
                address = (int)romWord;
            }
            
            address &= ~(-1 << (8 * count));
            return Util.NumberToBaseString(address, Util.NumberBase.Hexadecimal, 2 * count, true);
        }

        private string GetMnemonic(IReadOnlyCpuOperableByteSource data, int offset, bool showHint = true)
        {
            var mn = Mnemonics[data.GetRomByteUnsafe(offset)];
            if (!showHint) 
                return mn;

            var mode = GetAddressMode(data, offset);
            if (mode == null)
                return mn;
                
            var count = BytesToShow(mode.Value);

            if (mode == AddressMode.Constant8 || mode == AddressMode.Relative16 || mode == AddressMode.Relative8) 
                return mn;

            return count switch
            {
                1 => mn + ".B",
                2 => mn + ".W",
                3 => mn + ".L",
                _ => mn
            };
        }

        private static int BytesToShow(AddressMode mode)
        {
            switch (mode)
            {
                case AddressMode.Constant8:
                case AddressMode.Immediate8:
                case AddressMode.DirectPage:
                case AddressMode.DirectPageXIndex:
                case AddressMode.DirectPageYIndex:
                case AddressMode.DirectPageSIndex:
                case AddressMode.DirectPageIndirect:
                case AddressMode.DirectPageXIndexIndirect:
                case AddressMode.DirectPageIndirectYIndex:
                case AddressMode.DirectPageSIndexIndirectYIndex:
                case AddressMode.DirectPageLongIndirect:
                case AddressMode.DirectPageLongIndirectYIndex:
                case AddressMode.Relative8:
                    return 1;
                case AddressMode.Immediate16:
                case AddressMode.Address:
                case AddressMode.AddressXIndex:
                case AddressMode.AddressYIndex:
                case AddressMode.AddressIndirect:
                case AddressMode.AddressXIndexIndirect:
                case AddressMode.AddressLongIndirect:
                case AddressMode.Relative16:
                    return 2;
                case AddressMode.Long:
                case AddressMode.LongXIndex:
                    return 3;
            }
            return 0;
        }

        // {0} = mnemonic
        // {1} = intermediate address / label OR operand 1 for block move
        // {2} = operand 2 for block move
        private string GetInstructionFormatString(IReadOnlyCpuOperableByteSource data, int offset)
        {
            var mode = GetAddressMode(data, offset);
            switch (mode)
            {
                case AddressMode.Implied:
                    return "{0}";
                case AddressMode.Accumulator:
                    return "{0} A";
                case AddressMode.Constant8:
                case AddressMode.Immediate8:
                case AddressMode.Immediate16:
                    return "{0} #{1}";
                case AddressMode.DirectPage:
                case AddressMode.Address:
                case AddressMode.Long:
                case AddressMode.Relative8:
                case AddressMode.Relative16:
                    return "{0} {1}";
                case AddressMode.DirectPageXIndex:
                case AddressMode.AddressXIndex:
                case AddressMode.LongXIndex:
                    return "{0} {1},X";
                case AddressMode.DirectPageYIndex:
                case AddressMode.AddressYIndex:
                    return "{0} {1},Y";
                case AddressMode.DirectPageSIndex:
                    return "{0} {1},S";
                case AddressMode.DirectPageIndirect:
                case AddressMode.AddressIndirect:
                    return "{0} ({1})";
                case AddressMode.DirectPageXIndexIndirect:
                case AddressMode.AddressXIndexIndirect:
                    return "{0} ({1},X)";
                case AddressMode.DirectPageIndirectYIndex:
                    return "{0} ({1}),Y";
                case AddressMode.DirectPageSIndexIndirectYIndex:
                    return "{0} ({1},S),Y";
                case AddressMode.DirectPageLongIndirect:
                case AddressMode.AddressLongIndirect:
                    return "{0} [{1}]";
                case AddressMode.DirectPageLongIndirectYIndex:
                    return "{0} [{1}],Y";
                case AddressMode.BlockMove:
                    return "{0} {1},{2}";
            }
            return "";
        }
        
        public static AddressMode? GetAddressMode(IReadOnlyCpuOperableByteSource data, int offset)
        {
            var opcode = data.GetRomByte(offset);
            if (!opcode.HasValue)
                return null;
            
            var mFlag = data.GetMFlag(offset);
            var xFlag = data.GetXFlag(offset);
            
            return GetAddressMode(opcode.Value, mFlag, xFlag);
        }

        public static AddressMode GetAddressMode(int opcode, bool mFlag, bool xFlag)
        {
            var mode = AddressingModes[opcode];
            return mode switch
            {
                AddressMode.ImmediateMFlagDependent => mFlag
                    ? AddressMode.Immediate8
                    : AddressMode.Immediate16,
                AddressMode.ImmediateXFlagDependent => xFlag
                    ? AddressMode.Immediate8
                    : AddressMode.Immediate16,
                _ => mode
            };
        }

        public enum AddressMode : byte
        {
            Implied, Accumulator, Constant8, Immediate8, Immediate16,
            ImmediateXFlagDependent, ImmediateMFlagDependent,
            DirectPage, DirectPageXIndex, DirectPageYIndex,
            DirectPageSIndex, DirectPageIndirect, DirectPageXIndexIndirect,
            DirectPageIndirectYIndex, DirectPageSIndexIndirectYIndex,
            DirectPageLongIndirect, DirectPageLongIndirectYIndex,
            Address, AddressXIndex, AddressYIndex, AddressIndirect,
            AddressXIndexIndirect, AddressLongIndirect,
            Long, LongXIndex, BlockMove, Relative8, Relative16
        }

        private static readonly string[] Mnemonics =
        {
            "BRK", "ORA", "COP", "ORA", "TSB", "ORA", "ASL", "ORA", "PHP", "ORA", "ASL", "PHD", "TSB", "ORA", "ASL", "ORA",
            "BPL", "ORA", "ORA", "ORA", "TRB", "ORA", "ASL", "ORA", "CLC", "ORA", "INC", "TCS", "TRB", "ORA", "ASL", "ORA",
            "JSR", "AND", "JSL", "AND", "BIT", "AND", "ROL", "AND", "PLP", "AND", "ROL", "PLD", "BIT", "AND", "ROL", "AND",
            "BMI", "AND", "AND", "AND", "BIT", "AND", "ROL", "AND", "SEC", "AND", "DEC", "TSC", "BIT", "AND", "ROL", "AND",
            "RTI", "EOR", "WDM", "EOR", "MVP", "EOR", "LSR", "EOR", "PHA", "EOR", "LSR", "PHK", "JMP", "EOR", "LSR", "EOR",
            "BVC", "EOR", "EOR", "EOR", "MVN", "EOR", "LSR", "EOR", "CLI", "EOR", "PHY", "TCD", "JML", "EOR", "LSR", "EOR",
            "RTS", "ADC", "PER", "ADC", "STZ", "ADC", "ROR", "ADC", "PLA", "ADC", "ROR", "RTL", "JMP", "ADC", "ROR", "ADC",
            "BVS", "ADC", "ADC", "ADC", "STZ", "ADC", "ROR", "ADC", "SEI", "ADC", "PLY", "TDC", "JMP", "ADC", "ROR", "ADC",
            "BRA", "STA", "BRL", "STA", "STY", "STA", "STX", "STA", "DEY", "BIT", "TXA", "PHB", "STY", "STA", "STX", "STA",
            "BCC", "STA", "STA", "STA", "STY", "STA", "STX", "STA", "TYA", "STA", "TXS", "TXY", "STZ", "STA", "STZ", "STA",
            "LDY", "LDA", "LDX", "LDA", "LDY", "LDA", "LDX", "LDA", "TAY", "LDA", "TAX", "PLB", "LDY", "LDA", "LDX", "LDA",
            "BCS", "LDA", "LDA", "LDA", "LDY", "LDA", "LDX", "LDA", "CLV", "LDA", "TSX", "TYX", "LDY", "LDA", "LDX", "LDA",
            "CPY", "CMP", "REP", "CMP", "CPY", "CMP", "DEC", "CMP", "INY", "CMP", "DEX", "WAI", "CPY", "CMP", "DEC", "CMP",
            "BNE", "CMP", "CMP", "CMP", "PEI", "CMP", "DEC", "CMP", "CLD", "CMP", "PHX", "STP", "JML", "CMP", "DEC", "CMP",
            "CPX", "SBC", "SEP", "SBC", "CPX", "SBC", "INC", "SBC", "INX", "SBC", "NOP", "XBA", "CPX", "SBC", "INC", "SBC",
            "BEQ", "SBC", "SBC", "SBC", "PEA", "SBC", "INC", "SBC", "SED", "SBC", "PLX", "XCE", "JSR", "SBC", "INC", "SBC"
        };

        private static readonly AddressMode[] AddressingModes =
        {
            AddressMode.Constant8, AddressMode.DirectPageXIndexIndirect, AddressMode.Constant8, AddressMode.DirectPageSIndex,
            AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPageLongIndirect,
            AddressMode.Implied, AddressMode.ImmediateMFlagDependent, AddressMode.Accumulator, AddressMode.Implied,
            AddressMode.Address, AddressMode.Address, AddressMode.Address, AddressMode.Long,
            AddressMode.Relative8, AddressMode.DirectPageIndirectYIndex, AddressMode.DirectPageIndirect, AddressMode.DirectPageSIndexIndirectYIndex,
            AddressMode.DirectPage, AddressMode.DirectPageXIndex, AddressMode.DirectPageXIndex, AddressMode.DirectPageLongIndirectYIndex,
            AddressMode.Implied, AddressMode.AddressYIndex, AddressMode.Accumulator, AddressMode.Implied,
            AddressMode.Address, AddressMode.AddressXIndex, AddressMode.AddressXIndex, AddressMode.LongXIndex,

            AddressMode.Address, AddressMode.DirectPageXIndexIndirect, AddressMode.Long, AddressMode.DirectPageSIndex,
            AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPageLongIndirect,
            AddressMode.Implied, AddressMode.ImmediateMFlagDependent, AddressMode.Accumulator, AddressMode.Implied,
            AddressMode.Address, AddressMode.Address, AddressMode.Address, AddressMode.Long,
            AddressMode.Relative8, AddressMode.DirectPageIndirectYIndex, AddressMode.DirectPageIndirect, AddressMode.DirectPageSIndexIndirectYIndex,
            AddressMode.DirectPageXIndex, AddressMode.DirectPageXIndex, AddressMode.DirectPageXIndex, AddressMode.DirectPageLongIndirectYIndex,
            AddressMode.Implied, AddressMode.AddressYIndex, AddressMode.Accumulator, AddressMode.Implied,
            AddressMode.AddressXIndex, AddressMode.AddressXIndex, AddressMode.AddressXIndex, AddressMode.LongXIndex,

            AddressMode.Implied, AddressMode.DirectPageXIndexIndirect, AddressMode.Constant8, AddressMode.DirectPageSIndex,
            AddressMode.BlockMove, AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPageLongIndirect,
            AddressMode.Implied, AddressMode.ImmediateMFlagDependent, AddressMode.Accumulator, AddressMode.Implied,
            AddressMode.Address, AddressMode.Address, AddressMode.Address, AddressMode.Long,
            AddressMode.Relative8, AddressMode.DirectPageIndirectYIndex, AddressMode.DirectPageIndirect, AddressMode.DirectPageSIndexIndirectYIndex,
            AddressMode.BlockMove, AddressMode.DirectPageXIndex, AddressMode.DirectPageXIndex, AddressMode.DirectPageLongIndirectYIndex,
            AddressMode.Implied, AddressMode.AddressYIndex, AddressMode.Implied, AddressMode.Implied,
            AddressMode.Long, AddressMode.AddressXIndex, AddressMode.AddressXIndex, AddressMode.LongXIndex,

            AddressMode.Implied, AddressMode.DirectPageXIndexIndirect, AddressMode.Relative16, AddressMode.DirectPageSIndex,
            AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPageLongIndirect,
            AddressMode.Implied, AddressMode.ImmediateMFlagDependent, AddressMode.Accumulator, AddressMode.Implied,
            AddressMode.AddressIndirect, AddressMode.Address, AddressMode.Address, AddressMode.Long,
            AddressMode.Relative8, AddressMode.DirectPageIndirectYIndex, AddressMode.DirectPageIndirect, AddressMode.DirectPageSIndexIndirectYIndex,
            AddressMode.DirectPageXIndex, AddressMode.DirectPageXIndex, AddressMode.DirectPageXIndex, AddressMode.DirectPageLongIndirectYIndex,
            AddressMode.Implied, AddressMode.AddressYIndex, AddressMode.Implied, AddressMode.Implied,
            AddressMode.AddressXIndexIndirect, AddressMode.AddressXIndex, AddressMode.AddressXIndex, AddressMode.LongXIndex,

            AddressMode.Relative8, AddressMode.DirectPageXIndexIndirect, AddressMode.Relative16, AddressMode.DirectPageSIndex,
            AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPageLongIndirect,
            AddressMode.Implied, AddressMode.ImmediateMFlagDependent, AddressMode.Implied, AddressMode.Implied,
            AddressMode.Address, AddressMode.Address, AddressMode.Address, AddressMode.Long,
            AddressMode.Relative8, AddressMode.DirectPageIndirectYIndex, AddressMode.DirectPageIndirect, AddressMode.DirectPageSIndexIndirectYIndex,
            AddressMode.DirectPageXIndex, AddressMode.DirectPageXIndex, AddressMode.DirectPageYIndex, AddressMode.DirectPageLongIndirectYIndex,
            AddressMode.Implied, AddressMode.AddressYIndex, AddressMode.Implied, AddressMode.Implied,
            AddressMode.Address, AddressMode.AddressXIndex, AddressMode.AddressXIndex, AddressMode.LongXIndex,

            AddressMode.ImmediateXFlagDependent, AddressMode.DirectPageXIndexIndirect, AddressMode.ImmediateXFlagDependent, AddressMode.DirectPageSIndex,
            AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPageLongIndirect,
            AddressMode.Implied, AddressMode.ImmediateMFlagDependent, AddressMode.Implied, AddressMode.Implied,
            AddressMode.Address, AddressMode.Address, AddressMode.Address, AddressMode.Long,
            AddressMode.Relative8, AddressMode.DirectPageIndirectYIndex, AddressMode.DirectPageIndirect, AddressMode.DirectPageSIndexIndirectYIndex,
            AddressMode.DirectPageXIndex, AddressMode.DirectPageXIndex, AddressMode.DirectPageYIndex, AddressMode.DirectPageLongIndirectYIndex,
            AddressMode.Implied, AddressMode.AddressYIndex, AddressMode.Implied, AddressMode.Implied,
            AddressMode.AddressXIndex, AddressMode.AddressXIndex, AddressMode.AddressYIndex, AddressMode.LongXIndex,

            AddressMode.ImmediateXFlagDependent, AddressMode.DirectPageXIndexIndirect, AddressMode.Constant8, AddressMode.DirectPageSIndex,
            AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPageLongIndirect,
            AddressMode.Implied, AddressMode.ImmediateMFlagDependent, AddressMode.Implied, AddressMode.Implied,
            AddressMode.Address, AddressMode.Address, AddressMode.Address, AddressMode.Long,
            AddressMode.Relative8, AddressMode.DirectPageIndirectYIndex, AddressMode.DirectPageIndirect, AddressMode.DirectPageSIndexIndirectYIndex,
            AddressMode.DirectPageIndirect, AddressMode.DirectPageXIndex, AddressMode.DirectPageXIndex, AddressMode.DirectPageLongIndirectYIndex,
            AddressMode.Implied, AddressMode.AddressYIndex, AddressMode.Implied, AddressMode.Implied,
            AddressMode.AddressLongIndirect, AddressMode.AddressXIndex, AddressMode.AddressXIndex, AddressMode.LongXIndex,

            AddressMode.ImmediateXFlagDependent, AddressMode.DirectPageXIndexIndirect, AddressMode.Constant8, AddressMode.DirectPageSIndex,
            AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPageLongIndirect,
            AddressMode.Implied, AddressMode.ImmediateMFlagDependent, AddressMode.Implied, AddressMode.Implied,
            AddressMode.Address, AddressMode.Address, AddressMode.Address, AddressMode.Long,
            AddressMode.Relative8, AddressMode.DirectPageIndirectYIndex, AddressMode.DirectPageIndirect, AddressMode.DirectPageSIndexIndirectYIndex,
            AddressMode.Address, AddressMode.DirectPageXIndex, AddressMode.DirectPageXIndex, AddressMode.DirectPageLongIndirectYIndex,
            AddressMode.Implied, AddressMode.AddressYIndex, AddressMode.Implied, AddressMode.Implied,
            AddressMode.AddressXIndexIndirect, AddressMode.AddressXIndex, AddressMode.AddressXIndex, AddressMode.LongXIndex,
        };
    }
}

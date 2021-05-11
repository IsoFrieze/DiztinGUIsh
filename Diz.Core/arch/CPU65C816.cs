using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Core.arch
{
    public class Cpu65C816
    {
        private readonly Data data;
        public Cpu65C816(Data data)
        {
            this.data = data;
        }
        public int Step(int offset, bool branch, bool force, int prevOffset)
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

            var length = GetInstructionLength(offset);

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

            MarkInOutPoints(offset);

            var nextOffset = offset + length;

            if (force || (opcode != 0x4C && opcode != 0x5C && opcode != 0x80 && opcode != 0x82 && (!branch ||
                (opcode != 0x10 && opcode != 0x30 && opcode != 0x50 && opcode != 0x70 && opcode != 0x90 &&
                 opcode != 0xB0 && opcode != 0xD0 && opcode != 0xF0 && opcode != 0x20 &&
                 opcode != 0x22)))) 
                return nextOffset;

            var iaNextOffsetPc = data.ConvertSnesToPc(GetIntermediateAddress(offset, true));
            if (iaNextOffsetPc >= 0) 
                nextOffset = iaNextOffsetPc;

            return nextOffset;
        }

        // input: ROM offset
        // return: a SNES address
        public int GetIntermediateAddress(int offset, bool resolve)
        {
            int bank, directPage, operand, programCounter;
            var opcode = data.GetRomByte(offset);

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
                        directPage = data.GetDirectPage(offset);
                        operand = data.GetRomByte(offset + 1);
                        return (directPage + operand) & 0xFFFF;
                    }
                    else
                    {
                        goto case AddressMode.DirectPageSIndex;
                    }
                case AddressMode.DirectPageSIndex:
                case AddressMode.DirectPageSIndexIndirectYIndex:
                    return data.GetRomByte(offset + 1);
                case AddressMode.Address:
                case AddressMode.AddressXIndex:
                case AddressMode.AddressYIndex:
                case AddressMode.AddressXIndexIndirect:
                    bank = (opcode == 0x20 || opcode == 0x4C || opcode == 0x7C || opcode == 0xFC) ?
                        data.ConvertPCtoSnes(offset) >> 16 :
                        data.GetDataBank(offset);
                    operand = data.GetRomWord(offset + 1); 
                    return (bank << 16) | operand;
                case AddressMode.AddressIndirect:
                case AddressMode.AddressLongIndirect:
                    operand = data.GetRomWord(offset + 1);
                    return operand;
                case AddressMode.Long:
                case AddressMode.LongXIndex:
                    operand = data.GetRomLong(offset + 1);
                    return operand;
                case AddressMode.Relative8:
                    programCounter = data.ConvertPCtoSnes(offset + 2);
                    bank = programCounter >> 16;
                    offset = (sbyte)data.GetRomByte(offset + 1);
                    return (bank << 16) | ((programCounter + offset) & 0xFFFF);
                case AddressMode.Relative16:
                    programCounter = data.ConvertPCtoSnes(offset + 3);
                    bank = programCounter >> 16;
                    offset = (short)data.GetRomWord(offset + 1);
                    return (bank << 16) | ((programCounter + offset) & 0xFFFF);
            }
            return -1;
        }

        public string GetInstruction(int offset)
        {
            AddressMode mode = GetAddressMode(data, offset);
            string format = GetInstructionFormatString(offset);
            string mnemonic = GetMnemonic(offset);
            string op1, op2 = "";
            if (mode == AddressMode.BlockMove)
            {
                op1 = Util.NumberToBaseString(data.GetRomByte(offset + 1), Util.NumberBase.Hexadecimal, 2, true);
                op2 = Util.NumberToBaseString(data.GetRomByte(offset + 2), Util.NumberBase.Hexadecimal, 2, true);
            }
            else if (mode == AddressMode.Constant8 || mode == AddressMode.Immediate8)
            {
                op1 = Util.NumberToBaseString(data.GetRomByte(offset + 1), Util.NumberBase.Hexadecimal, 2, true);
            }
            else if (mode == AddressMode.Immediate16)
            {
                op1 = Util.NumberToBaseString(data.GetRomWord(offset + 1), Util.NumberBase.Hexadecimal, 4, true);
            }
            else
            {
                // dom note: this is where we could inject expressions if needed. it gives stuff like "$F001".
                // we could substitute our expression of "$#F000 + $#01" or "some_struct.member" like "player.hp"
                // the expression must be verified to always match the bytes in the file [unless we allow overriding]
                op1 = FormatOperandAddress(offset, mode);
            }
            return string.Format(format, mnemonic, op1, op2);
        }

        public int GetInstructionLength(int offset)
        {
            var mode = GetAddressMode(data, offset);
            return InstructionLength(mode);
        }

        public static int InstructionLength(AddressMode mode)
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
            }

            return 1;
        }

        public void MarkInOutPoints(int offset)
        {
            int opcode = data.GetRomByte(offset);
            int iaOffsetPc = data.ConvertSnesToPc(data.GetIntermediateAddress(offset, true));

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

        private string FormatOperandAddress(int offset, AddressMode mode)
        {
            int address = data.GetIntermediateAddress(offset);
            if (address < 0) 
                return "";

            var label = data.GetLabelName(address);
            if (label != "") 
                return label;

            var count = BytesToShow(mode);
            if (mode == AddressMode.Relative8 || mode == AddressMode.Relative16) address = data.GetRomWord(offset + 1);
            address &= ~(-1 << (8 * count));
            return Util.NumberToBaseString(address, Util.NumberBase.Hexadecimal, 2 * count, true);
        }

        private string GetMnemonic(int offset, bool showHint = true)
        {
            var mn = Mnemonics[data.GetRomByte(offset)];
            if (!showHint) 
                return mn;

            var mode = GetAddressMode(data, offset);
            var count = BytesToShow(mode);

            if (mode == AddressMode.Constant8 || mode == AddressMode.Relative16 || mode == AddressMode.Relative8) return mn;

            return count switch
            {
                1 => mn += ".B",
                2 => mn += ".W",
                3 => mn += ".L",
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
        private string GetInstructionFormatString(int offset)
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

        public static AddressMode GetAddressMode(Data data, int offset)
        {
            var opcode = data.GetRomByte(offset);
            var mFlag = data.GetMFlag(offset);
            var xFlag = data.GetXFlag(offset);
            
            return GetAddressMode(opcode, mFlag, xFlag);
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiztinGUIsh
{
    public static class CPU65C816
    {
        public static int Step(int offset, bool branch, bool force, int prevOffset)
        {
            int opcode = Data.GetROMByte(offset);
            int prevDirectPage = Data.GetDirectPage(offset);
            int prevDataBank = Data.GetDataBank(offset);
            bool prevX = Data.GetXFlag(offset), prevM = Data.GetMFlag(offset);

            if (prevOffset >= 0 && Data.GetFlag(prevOffset) == Data.FlagType.Opcode)
            {
                prevDirectPage = Data.GetDirectPage(prevOffset);
                prevDataBank = Data.GetDataBank(prevOffset);
                prevX = Data.GetXFlag(prevOffset);
                prevM = Data.GetMFlag(prevOffset);
            }

            if (opcode == 0xC2) // REP
            {
                prevX = (Data.GetROMByte(offset + 1) & 0x10) != 0 ? false : prevX;
                prevM = (Data.GetROMByte(offset + 1) & 0x20) != 0 ? false : prevM;
            }
            else if (opcode == 0xE2) // SEP
            {
                prevX = (Data.GetROMByte(offset + 1) & 0x10) != 0 ? true : prevX;
                prevM = (Data.GetROMByte(offset + 1) & 0x20) != 0 ? true : prevM;
            }

            // set first byte first, so the instruction length is correct
            Data.SetFlag(offset, Data.FlagType.Opcode);
            Data.SetDataBank(offset, prevDataBank);
            Data.SetDirectPage(offset, prevDirectPage);
            Data.SetXFlag(offset, prevX);
            Data.SetMFlag(offset, prevM);

            int length = GetInstructionLength(offset);

            for (int i = 1; i < length; i++)
            {
                Data.SetFlag(offset + i, Data.FlagType.Operand);
                Data.SetDataBank(offset + i, prevDataBank);
                Data.SetDirectPage(offset + i, prevDirectPage);
                Data.SetXFlag(offset + i, prevX);
                Data.SetMFlag(offset + i, prevM);
            }

            // set read point on EA
            int eaOffsetPC = Util.ConvertSNEStoPC(Util.GetEffectiveAddress(offset));
            if (eaOffsetPC >= 0 && ( // these are all read/write/math instructions
                ((opcode & 0x04) != 0) || ((opcode & 0x0F) == 0x01) || ((opcode & 0x0F) == 0x03) ||
                ((opcode & 0x1F) == 0x12) || ((opcode & 0x1F) == 0x19)) &&
                (opcode != 0x45) && (opcode != 0x55) && (opcode != 0xF5) && (opcode != 0x4C) &&
                (opcode != 0x5C) && (opcode != 0x6C) && (opcode != 0x7C) && (opcode != 0xDC) && (opcode != 0xFC))
            {
                Data.SetInOutPoint(eaOffsetPC, Data.InOutPoint.ReadPoint);
            }

            // set end point on offset
            if (opcode == 0x40 || opcode == 0x4C || opcode == 0x5C || opcode == 0x60 // RTI JMP JML RTS
                || opcode == 0x6B || opcode == 0x6C || opcode == 0x7C || opcode == 0x80 // RTL JMP JMP BRA
                || opcode == 0x82 || opcode == 0xDB || opcode == 0xDC)
            { // BRL STP JML
                Data.SetInOutPoint(offset, Data.InOutPoint.EndPoint);
            }

            int nextOffset = offset + length;

            if (!force && (opcode == 0x4C || opcode == 0x5C || opcode == 0x80 || opcode == 0x82 // JMP JML BRA BRL
                || (branch && (opcode == 0x10 || opcode == 0x30 || opcode == 0x50 // BPL BMI BVC
                || opcode == 0x70 || opcode == 0x90 || opcode == 0xB0 || opcode == 0xD0 // BVS BCC BCS BNE
                || opcode == 0xF0 || opcode == 0x20 || opcode == 0x22 // BEQ JSR JSL
            ))))
            {
                int eaNextOffsetPC = Util.ConvertSNEStoPC(Util.GetEffectiveAddress(offset));
                if (eaNextOffsetPC >= 0)
                {
                    nextOffset = eaNextOffsetPC;
                    // set out point on offset
                    // set in point on EA
                    Data.SetInOutPoint(offset, Data.InOutPoint.OutPoint);
                    Data.SetInOutPoint(nextOffset, Data.InOutPoint.InPoint);
                }
            }

            return nextOffset;
        }

        public static int GetEffectiveAddress(int offset)
        {
            int bank, directPage, operand, programCounter;
            int opcode = Data.GetROMByte(offset);

            AddressMode mode = GetAddressMode(offset);
            switch (mode)
            {
                case AddressMode.DIRECT_PAGE:
                case AddressMode.DIRECT_PAGE_X_INDEX:
                case AddressMode.DIRECT_PAGE_Y_INDEX:
                case AddressMode.DIRECT_PAGE_S_INDEX:
                case AddressMode.DIRECT_PAGE_INDIRECT:
                case AddressMode.DIRECT_PAGE_X_INDEX_INDIRECT:
                case AddressMode.DIRECT_PAGE_INDIRECT_Y_INDEX:
                case AddressMode.DIRECT_PAGE_S_INDEX_INDIRECT_Y_INDEX:
                case AddressMode.DIRECT_PAGE_LONG_INDIRECT:
                case AddressMode.DIRECT_PAGE_LONG_INDIRECT_Y_INDEX:
                    bank = Data.GetDataBank(offset);
                    directPage = Data.GetDirectPage(offset);
                    operand = Data.GetROMByte(offset + 1);
                    return (bank << 16) | ((directPage + operand) & 0xFFFF);
                case AddressMode.ADDRESS:
                case AddressMode.ADDRESS_X_INDEX:
                case AddressMode.ADDRESS_Y_INDEX:
                case AddressMode.ADDRESS_X_INDEX_INDIRECT:
                    bank = (opcode == 0x20 || opcode == 0x4C) ?
                        Util.ConvertPCtoSNES(offset) >> 16 :
                        Data.GetDataBank(offset);
                    operand = Util.GetROMWord(offset + 1);
                    return (bank << 16) | operand;
                case AddressMode.ADDRESS_INDIRECT:
                case AddressMode.ADDRESS_LONG_INDIRECT:
                    operand = Util.GetROMWord(offset + 1);
                    return operand;
                case AddressMode.LONG:
                case AddressMode.LONG_X_INDEX:
                    operand = Util.GetROMLong(offset + 1);
                    return operand;
                case AddressMode.RELATIVE_8:
                    programCounter = Util.ConvertPCtoSNES(offset + 2);
                    bank = programCounter >> 16;
                    offset = (sbyte)Data.GetROMByte(offset + 1);
                    return (bank << 16) | ((programCounter + offset) & 0xFFFF);
                case AddressMode.RELATIVE_16:
                    programCounter = Util.ConvertPCtoSNES(offset + 3);
                    bank = programCounter >> 16;
                    offset = (short)Util.GetROMWord(offset + 1);
                    return (bank << 16) | ((programCounter + offset) & 0xFFFF);
            }
            return -1;
        }

        public static string GetInstruction(int offset)
        {
            AddressMode mode = GetAddressMode(offset);
            string format = GetInstructionFormatString(offset);
            string mnemonic = GetMnemonic(offset);
            string op1 = "", op2 = "";
            if (mode == AddressMode.BLOCK_MOVE)
            {
                op1 = Util.NumberToBaseString(Data.GetROMByte(offset + 1), Util.NumberBase.Hexadecimal, 2, true);
                op2 = Util.NumberToBaseString(Data.GetROMByte(offset + 2), Util.NumberBase.Hexadecimal, 2, true);
            }
            else if (mode == AddressMode.CONSTANT_8 || mode == AddressMode.IMMEDIATE_8)
            {
                op1 = Util.NumberToBaseString(Data.GetROMByte(offset + 1), Util.NumberBase.Hexadecimal, 2, true);
            }
            else if (mode == AddressMode.IMMEDIATE_16)
            {
                op1 = Util.NumberToBaseString(Util.GetROMWord(offset + 1), Util.NumberBase.Hexadecimal, 4, true);
            }
            else
            {
                op1 = FormatOperandAddress(Util.GetROMLong(offset + 1), mode);
            }
            return string.Format(format, mnemonic, op1, op2);
        }

        public static int GetInstructionLength(int offset)
        {
            AddressMode mode = GetAddressMode(offset);
            switch (mode)
            {
                case AddressMode.IMPLIED:
                case AddressMode.ACCUMULATOR:
                    return 1;
                case AddressMode.CONSTANT_8:
                case AddressMode.IMMEDIATE_8:
                case AddressMode.DIRECT_PAGE:
                case AddressMode.DIRECT_PAGE_X_INDEX:
                case AddressMode.DIRECT_PAGE_Y_INDEX:
                case AddressMode.DIRECT_PAGE_S_INDEX:
                case AddressMode.DIRECT_PAGE_INDIRECT:
                case AddressMode.DIRECT_PAGE_X_INDEX_INDIRECT:
                case AddressMode.DIRECT_PAGE_INDIRECT_Y_INDEX:
                case AddressMode.DIRECT_PAGE_S_INDEX_INDIRECT_Y_INDEX:
                case AddressMode.DIRECT_PAGE_LONG_INDIRECT:
                case AddressMode.DIRECT_PAGE_LONG_INDIRECT_Y_INDEX:
                case AddressMode.RELATIVE_8:
                    return 2;
                case AddressMode.IMMEDIATE_16:
                case AddressMode.ADDRESS:
                case AddressMode.ADDRESS_X_INDEX:
                case AddressMode.ADDRESS_Y_INDEX:
                case AddressMode.ADDRESS_INDIRECT:
                case AddressMode.ADDRESS_X_INDEX_INDIRECT:
                case AddressMode.ADDRESS_LONG_INDIRECT:
                case AddressMode.BLOCK_MOVE:
                case AddressMode.RELATIVE_16:
                    return 3;
                case AddressMode.LONG:
                case AddressMode.LONG_X_INDEX:
                    return 4;
            }
            return 1;
        }

        private static string FormatOperandAddress(int address, AddressMode mode)
        {
            if (address < 0) return "";
            int pc = Util.ConvertSNEStoPC(address);
            if (pc >= 0 && Data.GetLabel(pc) != "") return Data.GetLabel(pc);

            int count = BytesToShow(mode);
            address &= ~(-1 << (8 * count));
            return Util.NumberToBaseString(address, Util.NumberBase.Hexadecimal, 2 * count, true);
        }

        private static string GetMnemonic(int offset, bool showHint = true)
        {
            string mn = mnemonics[Data.GetROMByte(offset)];
            if (showHint)
            {
                AddressMode mode = GetAddressMode(offset);
                int count = BytesToShow(mode);

                if (mode == AddressMode.CONSTANT_8 || mode == AddressMode.RELATIVE_16 || mode == AddressMode.RELATIVE_8) return mn;
                
                switch (count)
                {
                    case 1: return mn += ".B";
                    case 2: return mn += ".W";
                    case 3: return mn += ".L";
                }
            }
            return mn;
        }

        private static int BytesToShow(AddressMode mode)
        {
            switch (mode)
            {
                case AddressMode.CONSTANT_8:
                case AddressMode.IMMEDIATE_8:
                case AddressMode.DIRECT_PAGE:
                case AddressMode.DIRECT_PAGE_X_INDEX:
                case AddressMode.DIRECT_PAGE_Y_INDEX:
                case AddressMode.DIRECT_PAGE_S_INDEX:
                case AddressMode.DIRECT_PAGE_INDIRECT:
                case AddressMode.DIRECT_PAGE_X_INDEX_INDIRECT:
                case AddressMode.DIRECT_PAGE_INDIRECT_Y_INDEX:
                case AddressMode.DIRECT_PAGE_S_INDEX_INDIRECT_Y_INDEX:
                case AddressMode.DIRECT_PAGE_LONG_INDIRECT:
                case AddressMode.DIRECT_PAGE_LONG_INDIRECT_Y_INDEX:
                case AddressMode.RELATIVE_8:
                    return 1;
                case AddressMode.IMMEDIATE_16:
                case AddressMode.ADDRESS:
                case AddressMode.ADDRESS_X_INDEX:
                case AddressMode.ADDRESS_Y_INDEX:
                case AddressMode.ADDRESS_INDIRECT:
                case AddressMode.ADDRESS_X_INDEX_INDIRECT:
                case AddressMode.ADDRESS_LONG_INDIRECT:
                case AddressMode.RELATIVE_16:
                    return 2;
                case AddressMode.LONG:
                case AddressMode.LONG_X_INDEX:
                    return 3;
            }
            return 0;
        }

        // {0} = mnemonic
        // {1} = effective address / label OR operand 1 for block move
        // {2} = operand 2 for block move
        private static string GetInstructionFormatString(int offset)
        {
            AddressMode mode = GetAddressMode(offset);
            switch (mode)
            {
                case AddressMode.IMPLIED:
                    return "{0}";
                case AddressMode.ACCUMULATOR:
                    return "{0} A";
                case AddressMode.CONSTANT_8:
                case AddressMode.IMMEDIATE_8:
                case AddressMode.IMMEDIATE_16:
                    return "{0} #{1}";
                case AddressMode.DIRECT_PAGE:
                case AddressMode.ADDRESS:
                case AddressMode.LONG:
                case AddressMode.RELATIVE_8:
                case AddressMode.RELATIVE_16:
                    return "{0} {1}";
                case AddressMode.DIRECT_PAGE_X_INDEX:
                case AddressMode.ADDRESS_X_INDEX:
                case AddressMode.LONG_X_INDEX:
                    return "{0} {1},X";
                case AddressMode.DIRECT_PAGE_Y_INDEX:
                case AddressMode.ADDRESS_Y_INDEX:
                    return "{0} {1},Y";
                case AddressMode.DIRECT_PAGE_S_INDEX:
                    return "{0} {1},S";
                case AddressMode.DIRECT_PAGE_INDIRECT:
                case AddressMode.ADDRESS_INDIRECT:
                    return "{0} ({1})";
                case AddressMode.DIRECT_PAGE_X_INDEX_INDIRECT:
                case AddressMode.ADDRESS_X_INDEX_INDIRECT:
                    return "{0} ({1},X)";
                case AddressMode.DIRECT_PAGE_INDIRECT_Y_INDEX:
                    return "{0} ({1}),Y";
                case AddressMode.DIRECT_PAGE_S_INDEX_INDIRECT_Y_INDEX:
                    return "{0} ({1},S),Y";
                case AddressMode.DIRECT_PAGE_LONG_INDIRECT:
                case AddressMode.ADDRESS_LONG_INDIRECT:
                    return "{0} [{1}]";
                case AddressMode.DIRECT_PAGE_LONG_INDIRECT_Y_INDEX:
                    return "{0} [{1}],Y";
                case AddressMode.BLOCK_MOVE:
                    return "{0} {2},{1}";
            }
            return "";
        }

        private static AddressMode GetAddressMode(int offset)
        {
            AddressMode mode = addressingModes[Data.GetROMByte(offset)];
            if (mode == AddressMode.IMMEDIATE_M_FLAG_DEPENDENT) return Data.GetMFlag(offset) ? AddressMode.IMMEDIATE_8 : AddressMode.IMMEDIATE_16;
            else if (mode == AddressMode.IMMEDIATE_X_FLAG_DEPENDENT) return Data.GetXFlag(offset) ? AddressMode.IMMEDIATE_8 : AddressMode.IMMEDIATE_16;
            return mode;
        }

        private enum AddressMode : byte
        {
            IMPLIED, ACCUMULATOR, CONSTANT_8, IMMEDIATE_8, IMMEDIATE_16,
            IMMEDIATE_X_FLAG_DEPENDENT, IMMEDIATE_M_FLAG_DEPENDENT,
            DIRECT_PAGE, DIRECT_PAGE_X_INDEX, DIRECT_PAGE_Y_INDEX,
            DIRECT_PAGE_S_INDEX, DIRECT_PAGE_INDIRECT, DIRECT_PAGE_X_INDEX_INDIRECT,
            DIRECT_PAGE_INDIRECT_Y_INDEX, DIRECT_PAGE_S_INDEX_INDIRECT_Y_INDEX,
            DIRECT_PAGE_LONG_INDIRECT, DIRECT_PAGE_LONG_INDIRECT_Y_INDEX,
            ADDRESS, ADDRESS_X_INDEX, ADDRESS_Y_INDEX, ADDRESS_INDIRECT,
            ADDRESS_X_INDEX_INDIRECT, ADDRESS_LONG_INDIRECT,
            LONG, LONG_X_INDEX, BLOCK_MOVE, RELATIVE_8, RELATIVE_16
        }

        private static string[] mnemonics =
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

        private static AddressMode[] addressingModes =
        {
            AddressMode.CONSTANT_8, AddressMode.DIRECT_PAGE_X_INDEX_INDIRECT, AddressMode.CONSTANT_8, AddressMode.DIRECT_PAGE_S_INDEX,
            AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE_LONG_INDIRECT,
            AddressMode.IMPLIED, AddressMode.IMMEDIATE_M_FLAG_DEPENDENT, AddressMode.ACCUMULATOR, AddressMode.IMPLIED,
            AddressMode.ADDRESS, AddressMode.ADDRESS, AddressMode.ADDRESS, AddressMode.LONG,
            AddressMode.RELATIVE_8, AddressMode.DIRECT_PAGE_INDIRECT_Y_INDEX, AddressMode.DIRECT_PAGE_INDIRECT, AddressMode.DIRECT_PAGE_S_INDEX_INDIRECT_Y_INDEX,
            AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE_X_INDEX, AddressMode.DIRECT_PAGE_X_INDEX, AddressMode.DIRECT_PAGE_LONG_INDIRECT_Y_INDEX,
            AddressMode.IMPLIED, AddressMode.ADDRESS_Y_INDEX, AddressMode.ACCUMULATOR, AddressMode.IMPLIED,
            AddressMode.ADDRESS, AddressMode.ADDRESS_X_INDEX, AddressMode.ADDRESS_X_INDEX, AddressMode.LONG_X_INDEX,

            AddressMode.ADDRESS, AddressMode.DIRECT_PAGE_X_INDEX_INDIRECT, AddressMode.LONG, AddressMode.DIRECT_PAGE_S_INDEX,
            AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE_LONG_INDIRECT,
            AddressMode.IMPLIED, AddressMode.IMMEDIATE_M_FLAG_DEPENDENT, AddressMode.ACCUMULATOR, AddressMode.IMPLIED,
            AddressMode.ADDRESS, AddressMode.ADDRESS, AddressMode.ADDRESS, AddressMode.LONG,
            AddressMode.RELATIVE_8, AddressMode.DIRECT_PAGE_INDIRECT_Y_INDEX, AddressMode.DIRECT_PAGE_INDIRECT, AddressMode.DIRECT_PAGE_S_INDEX_INDIRECT_Y_INDEX,
            AddressMode.DIRECT_PAGE_X_INDEX, AddressMode.DIRECT_PAGE_X_INDEX, AddressMode.DIRECT_PAGE_X_INDEX, AddressMode.DIRECT_PAGE_LONG_INDIRECT_Y_INDEX,
            AddressMode.IMPLIED, AddressMode.ADDRESS_Y_INDEX, AddressMode.ACCUMULATOR, AddressMode.IMPLIED,
            AddressMode.ADDRESS_X_INDEX, AddressMode.ADDRESS_X_INDEX, AddressMode.ADDRESS_X_INDEX, AddressMode.LONG_X_INDEX,

            AddressMode.IMPLIED, AddressMode.DIRECT_PAGE_X_INDEX_INDIRECT, AddressMode.CONSTANT_8, AddressMode.DIRECT_PAGE_S_INDEX,
            AddressMode.BLOCK_MOVE, AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE_LONG_INDIRECT,
            AddressMode.IMPLIED, AddressMode.IMMEDIATE_M_FLAG_DEPENDENT, AddressMode.ACCUMULATOR, AddressMode.IMPLIED,
            AddressMode.ADDRESS, AddressMode.ADDRESS, AddressMode.ADDRESS, AddressMode.LONG,
            AddressMode.RELATIVE_8, AddressMode.DIRECT_PAGE_INDIRECT_Y_INDEX, AddressMode.DIRECT_PAGE_INDIRECT, AddressMode.DIRECT_PAGE_S_INDEX_INDIRECT_Y_INDEX,
            AddressMode.BLOCK_MOVE, AddressMode.DIRECT_PAGE_X_INDEX, AddressMode.DIRECT_PAGE_X_INDEX, AddressMode.DIRECT_PAGE_LONG_INDIRECT_Y_INDEX,
            AddressMode.IMPLIED, AddressMode.ADDRESS_Y_INDEX, AddressMode.IMPLIED, AddressMode.IMPLIED,
            AddressMode.LONG, AddressMode.ADDRESS_X_INDEX, AddressMode.ADDRESS_X_INDEX, AddressMode.LONG_X_INDEX,

            AddressMode.IMPLIED, AddressMode.DIRECT_PAGE_X_INDEX_INDIRECT, AddressMode.RELATIVE_8, AddressMode.DIRECT_PAGE_S_INDEX,
            AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE_LONG_INDIRECT,
            AddressMode.IMPLIED, AddressMode.IMMEDIATE_M_FLAG_DEPENDENT, AddressMode.ACCUMULATOR, AddressMode.IMPLIED,
            AddressMode.ADDRESS_INDIRECT, AddressMode.ADDRESS, AddressMode.ADDRESS, AddressMode.LONG,
            AddressMode.RELATIVE_8, AddressMode.DIRECT_PAGE_INDIRECT_Y_INDEX, AddressMode.DIRECT_PAGE_INDIRECT, AddressMode.DIRECT_PAGE_S_INDEX_INDIRECT_Y_INDEX,
            AddressMode.DIRECT_PAGE_X_INDEX, AddressMode.DIRECT_PAGE_X_INDEX, AddressMode.DIRECT_PAGE_X_INDEX, AddressMode.DIRECT_PAGE_LONG_INDIRECT_Y_INDEX,
            AddressMode.IMPLIED, AddressMode.ADDRESS_Y_INDEX, AddressMode.IMPLIED, AddressMode.IMPLIED,
            AddressMode.ADDRESS_X_INDEX_INDIRECT, AddressMode.ADDRESS_X_INDEX, AddressMode.ADDRESS_X_INDEX, AddressMode.LONG_X_INDEX,

            AddressMode.RELATIVE_8, AddressMode.DIRECT_PAGE_X_INDEX_INDIRECT, AddressMode.RELATIVE_16, AddressMode.DIRECT_PAGE_S_INDEX,
            AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE_LONG_INDIRECT,
            AddressMode.IMPLIED, AddressMode.IMMEDIATE_M_FLAG_DEPENDENT, AddressMode.IMPLIED, AddressMode.IMPLIED,
            AddressMode.ADDRESS, AddressMode.ADDRESS, AddressMode.ADDRESS, AddressMode.LONG,
            AddressMode.RELATIVE_8, AddressMode.DIRECT_PAGE_INDIRECT_Y_INDEX, AddressMode.DIRECT_PAGE_INDIRECT, AddressMode.DIRECT_PAGE_S_INDEX_INDIRECT_Y_INDEX,
            AddressMode.DIRECT_PAGE_X_INDEX, AddressMode.DIRECT_PAGE_X_INDEX, AddressMode.DIRECT_PAGE_Y_INDEX, AddressMode.DIRECT_PAGE_LONG_INDIRECT_Y_INDEX,
            AddressMode.IMPLIED, AddressMode.ADDRESS_Y_INDEX, AddressMode.IMPLIED, AddressMode.IMPLIED,
            AddressMode.ADDRESS, AddressMode.ADDRESS_X_INDEX, AddressMode.ADDRESS_X_INDEX, AddressMode.LONG_X_INDEX,

            AddressMode.IMMEDIATE_X_FLAG_DEPENDENT, AddressMode.DIRECT_PAGE_X_INDEX_INDIRECT, AddressMode.IMMEDIATE_X_FLAG_DEPENDENT, AddressMode.DIRECT_PAGE_S_INDEX,
            AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE_LONG_INDIRECT,
            AddressMode.IMPLIED, AddressMode.IMMEDIATE_M_FLAG_DEPENDENT, AddressMode.IMPLIED, AddressMode.IMPLIED,
            AddressMode.ADDRESS, AddressMode.ADDRESS, AddressMode.ADDRESS, AddressMode.LONG,
            AddressMode.RELATIVE_8, AddressMode.DIRECT_PAGE_INDIRECT_Y_INDEX, AddressMode.DIRECT_PAGE_INDIRECT, AddressMode.DIRECT_PAGE_S_INDEX_INDIRECT_Y_INDEX,
            AddressMode.DIRECT_PAGE_X_INDEX, AddressMode.DIRECT_PAGE_X_INDEX, AddressMode.DIRECT_PAGE_Y_INDEX, AddressMode.DIRECT_PAGE_LONG_INDIRECT_Y_INDEX,
            AddressMode.IMPLIED, AddressMode.ADDRESS_Y_INDEX, AddressMode.IMPLIED, AddressMode.IMPLIED,
            AddressMode.ADDRESS_X_INDEX, AddressMode.ADDRESS_X_INDEX, AddressMode.ADDRESS_Y_INDEX, AddressMode.LONG_X_INDEX,

            AddressMode.IMMEDIATE_X_FLAG_DEPENDENT, AddressMode.DIRECT_PAGE_X_INDEX_INDIRECT, AddressMode.CONSTANT_8, AddressMode.DIRECT_PAGE_S_INDEX,
            AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE_LONG_INDIRECT,
            AddressMode.IMPLIED, AddressMode.IMMEDIATE_M_FLAG_DEPENDENT, AddressMode.IMPLIED, AddressMode.IMPLIED,
            AddressMode.ADDRESS, AddressMode.ADDRESS, AddressMode.ADDRESS, AddressMode.LONG,
            AddressMode.RELATIVE_8, AddressMode.DIRECT_PAGE_INDIRECT_Y_INDEX, AddressMode.DIRECT_PAGE_INDIRECT, AddressMode.DIRECT_PAGE_S_INDEX_INDIRECT_Y_INDEX,
            AddressMode.DIRECT_PAGE_INDIRECT, AddressMode.DIRECT_PAGE_X_INDEX, AddressMode.DIRECT_PAGE_X_INDEX, AddressMode.DIRECT_PAGE_LONG_INDIRECT_Y_INDEX,
            AddressMode.IMPLIED, AddressMode.ADDRESS_Y_INDEX, AddressMode.IMPLIED, AddressMode.IMPLIED,
            AddressMode.ADDRESS_LONG_INDIRECT, AddressMode.ADDRESS_X_INDEX, AddressMode.ADDRESS_X_INDEX, AddressMode.LONG_X_INDEX,

            AddressMode.IMMEDIATE_X_FLAG_DEPENDENT, AddressMode.DIRECT_PAGE_X_INDEX_INDIRECT, AddressMode.CONSTANT_8, AddressMode.DIRECT_PAGE_S_INDEX,
            AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE, AddressMode.DIRECT_PAGE_LONG_INDIRECT,
            AddressMode.IMPLIED, AddressMode.IMMEDIATE_M_FLAG_DEPENDENT, AddressMode.IMPLIED, AddressMode.IMPLIED,
            AddressMode.ADDRESS, AddressMode.ADDRESS, AddressMode.ADDRESS, AddressMode.LONG,
            AddressMode.RELATIVE_8, AddressMode.DIRECT_PAGE_INDIRECT_Y_INDEX, AddressMode.DIRECT_PAGE_INDIRECT, AddressMode.DIRECT_PAGE_S_INDEX_INDIRECT_Y_INDEX,
            AddressMode.ADDRESS, AddressMode.DIRECT_PAGE_X_INDEX, AddressMode.DIRECT_PAGE_X_INDEX, AddressMode.DIRECT_PAGE_LONG_INDIRECT_Y_INDEX,
            AddressMode.IMPLIED, AddressMode.ADDRESS_Y_INDEX, AddressMode.IMPLIED, AddressMode.IMPLIED,
            AddressMode.ADDRESS_X_INDEX_INDIRECT, AddressMode.ADDRESS_X_INDEX, AddressMode.ADDRESS_X_INDEX, AddressMode.LONG_X_INDEX,
        };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiztinGUIsh
{
    public static class CPU65C816_EMU
    {
        public static int Step(int offset, bool branch, bool force, int prevOffset)
        {
            int opcode = Data.GetROMByte(offset);
            int prevDirectPage = Data.GetDirectPage(offset);
            int prevDataBank = Data.GetDataBank(offset);
            bool prevX = Data.GetXFlag(offset), prevM = Data.GetMFlag(offset);
            
            while (prevOffset >= 0 && Data.GetFlag(prevOffset) == Data.FlagType.Operand) prevOffset--;
            if (prevOffset >= 0 && Data.GetFlag(prevOffset) == Data.FlagType.Opcode)
            {
                prevDirectPage = Data.GetDirectPage(prevOffset);
                prevDataBank = Data.GetDataBank(prevOffset);
                prevX = Data.GetXFlag(prevOffset);
                prevM = Data.GetMFlag(prevOffset);
            }
            
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
            
            MarkInOutPoints(offset);
            
            if (!force && (opcode == 0x4C || opcode == 0x80 // JMP BRA
                || (branch && (opcode == 0x10 || opcode == 0x30 || opcode == 0x50 // BPL BMI BVC
                || opcode == 0x70 || opcode == 0x90 || opcode == 0xB0 || opcode == 0xD0 // BVS BCC BCS BNE
                || opcode == 0xF0 || opcode == 0x20)))) // BEQ JSR
            {
                int iaNextOffsetPC = Util.ConvertSNEStoPC(Util.GetIntermediateAddress(offset));
                if (iaNextOffsetPC >= 0) nextOffset = iaNextOffsetPC;
            }

            return nextOffset;
        }
        
        public static int GetIntermediateAddress(int offset)
        {
        
        }
        
        public static string GetInstruction(int offset)
        {
        
        }
        
        public static int GetInstructionLength(int offset)
        {
        
        }
        
        public static void MarkInOutPoints(int offset)
        {
        
        }
        
        private static string FormatOperandAddress(int offset, AddressMode mode)
        {
        
        }
        
        private static string GetMnemonic(int offset, bool showHint = true)
        {
        
        }
        
        private static int BytesToShow(AddressMode mode)
        {
        
        }
        
        // {0} = mnemonic
        // {1} = intermediate address / label OR operand 1 for block move
        // {2} = operand 2 for block move
        private static string GetInstructionFormatString(int offset)
        {
        
        }
        
        private static AddressMode GetAddressMode(int offset)
        {
        
        }
        
        private enum AddressMode : byte
        {
            IMPLIED, ACCUMULATOR, CONSTANT, IMMEDIATE,
            ZERO_PAGE, ZERO_PAGE_X_INDEX, ZERO_PAGE_Y_INDEX,
            ZERO_PAGE_INDIRECT, ZERO_PAGE_X_INDEX_INDIRECT, ZERO_PAGE_INDIRECT_Y_INDEX,
            ZERO_PAGE_AND_RELATIVE
            ADDRESS, ADDRESS_X_INDEX, ADDRESS_Y_INDEX,
            RELATIVE
        }
        
        private static string[] mnemonics =
        {
            "BRK", "ORA", "COP", "NOP", "TSB", "ORA", "ASL", "RMB0", "PHP", "ORA", "ASL", "NOP", "TSB", "ORA", "ASL", "BBR0",
            "BPL", "ORA", "ORA", "NOP", "TRB", "ORA", "ASL", "RMB1", "CLC", "ORA", "INC", "NOP", "TRB", "ORA", "ASL", "BBR1",
            "JSR", "AND", "NOP", "NOP", "BIT", "AND", "ROL", "RMB2", "PLP", "AND", "ROL", "NOP", "BIT", "AND", "ROL", "BBR2",
            "BPL", "AND", "AND", "NOP", "BIT", "AND", "ROL", "RMB3", "SEC", "AND", "DEC", "NOP", "BIT", "AND", "ROL", "BBR3",
            "RTI", "EOR", "NOP", "NOP", "NOP", "EOR", "LSR", "RMB4", "PHA", "EOR", "LSR", "NOP", "JMP", "EOR", "LSR", "BBR4",
            "BVC", "EOR", "EOR", "NOP", "NOP", "EOR", "LSR", "RMB5", "CLI", "EOR", "PHY", "NOP", "NOP", "EOR", "LSR", "BBR5",
            "RTS", "ADC", "NOP", "NOP", "STZ", "ADC", "ROR", "RMB6", "PLA", "ADC", "ROR", "NOP", "JMP", "ADC", "ROR", "BBR6",
            "BVS", "ADC", "ADC", "NOP", "STZ", "ADC", "ROR", "RMB7", "SEI", "ADC", "PLY", "NOP", "JMP", "ADC", "ROR", "BBR7",
            "BRA", "STA", "NOP", "NOP", "STY", "STA", "STX", "SMB0", "DEY", "BIT", "TXA", "NOP", "STY", "STA", "STX", "BBS0",
            "BCC", "STA", "STA", "NOP", "STY", "STA", "STX", "SMB1", "TYA", "STA", "TXS", "NOP", "STZ", "STA", "STZ", "BBS1",
            "LDY", "LDA", "NOP", "NOP", "LDY", "LDA", "LDX", "SMB2", "TAY", "LDA", "TAX", "NOP", "LDY", "LDA", "LDX", "BBS2",
            "BCS", "LDA", "LDA", "NOP", "LDY", "LDA", "LDX", "SMB3", "CLV", "LDA", "TSX", "NOP", "LDY", "LDA", "LDX", "BBS3",
            "CPY", "CMP", "NOP", "NOP", "CPY", "CMP", "DEC", "SMB4", "INY", "CMP", "DEX", "WAI", "CPY", "CMP", "DEC", "BBS4",
            "BNE", "CMP", "CMP", "NOP", "NOP", "CMP", "DEC", "SMB5", "CLD", "CMP", "PHX", "STP", "NOP", "CMP", "DEC", "BBS5",
            "CPX", "SBC", "NOP", "NOP", "CPX", "SBC", "INC", "SMB6", "INX", "SBC", "NOP", "NOP", "CPX", "SBC", "INC", "BBS6",
            "BEQ", "SBC", "SBC", "NOP", "NOP", "SBC", "INC", "SMB7", "SED", "SBC", "PLX", "XCE", "NOP", "SBC", "INC", "BBS7"
        };
        
        private static AddressMode[] addressingModes =
        {
            AddressMode.CONSTANT, AddressMode.ZERO_PAGE_X_INDEX_INDIRECT, AddressMode.CONSTANT, AddressMode.IMPLIED,
            AddressMode.ZERO_PAGE, AddressMode.ZERO_PAGE, AddressMode.ZERO_PAGE, AddressMode.ZERO_PAGE,
            AddressMode.IMPLIED, AddressMode.IMMEDIATE, AddressMode.ACCUMULATOR, AddressMode.IMPLIED,
            AddressMode.ADDRESS, AddressMode.ADDRESS, AddressMode.ADDRESS, AddressMode.ZERO_PAGE_AND_RELATIVE,
            AddressMode.RELATIVE, AddressMode.ZERO_PAGE_Y_INDEX_INDIRECT, AddressMode.ZERO_PAGE_INDIRECT, AddressMode.IMPLIED,
            AddressMode.ZERO_PAGE, AddressMode.ZERO_PAGE_X_INDEX, AddressMode.ZERO_PAGE_X_INDEX, AddressMode.ZERO_PAGE,
            AddressMode.IMPLIED, AddressMode.ADDRESS_Y_INDEX, AddressMode.ACCUMULATOR, AddressMode.IMPLIED,
            AddressMode.ADDRESS, AddressMode.ADDRESS_X_INDEX, AddressMode.ADDRESS_X_INDEX, AddressMode.ZERO_PAGE_AND_RELATIVE,
        };
    }
}

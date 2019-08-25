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
        
        }
        
        private static string[] mnemonics =
        {
        
        };
        
        private static AddressMode[] addressingModes =
        {
        
        };
    }
}

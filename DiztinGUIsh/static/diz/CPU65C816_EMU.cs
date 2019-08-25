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

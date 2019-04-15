using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiztinGUIsh
{
    public static class Util
    {
        public enum NumberBase
        {
            Decimal = 3, Hexadecimal = 2, Binary = 8
        }

        public static int GetROMWord(int offset)
        {
            if (offset + 1 < Data.GetROMSize())
                return Data.GetROMByte(offset) + (Data.GetROMByte(offset + 1) << 8);
            return -1;
        }

        public static int GetROMLong(int offset)
        {
            if (offset + 2 < Data.GetROMSize())
                return Data.GetROMByte(offset) + (Data.GetROMByte(offset + 1) << 8) + (Data.GetROMByte(offset + 2) << 16);
            return -1;
        }

        public static int GetEffectiveAddress(int offset)
        {
            switch (Data.GetArchitechture(offset))
            {
                case Data.Architechture.CPU65C816: return CPU65C816.GetEffectiveAddress(offset);
                case Data.Architechture.APUSPC700: return -1;
                case Data.Architechture.GPUSuperFX: return -1;
            }
            return -1;
        }

        public static string GetInstruction(int offset)
        {
            switch (Data.GetArchitechture(offset))
            {
                case Data.Architechture.CPU65C816: return CPU65C816.GetInstruction(offset);
                case Data.Architechture.APUSPC700: return "";
                case Data.Architechture.GPUSuperFX: return "";
            }
            return "";
        }

        public static int ConvertPCtoSNES(int offset)
        {
            if (Data.GetROMMapMode() == Data.ROMMapMode.LoROM)
            {
                offset = ((offset & 0x3F8000) << 1) | 0x8000 | (offset & 0x7FFF);
                if (Data.GetROMSpeed() == Data.ROMSpeed.FastROM || offset >= 0x7E0000) offset |= 0x800000;
            }
            else if (Data.GetROMMapMode() == Data.ROMMapMode.HiROM)
            {
                offset |= 0x400000;
                if (Data.GetROMSpeed() == Data.ROMSpeed.FastROM || offset >= 0x7E0000) offset |= 0x800000;
            }
            else // if (Data.GetROMMapMode() == Data.ROMMapMode.ExHiROM)
            {
                if (offset < 0x40000) offset |= 0xC00000;
                else if (offset >= 0x7E0000) offset &= 0x3FFFFF;
            }
            return offset;
        }

        public static int ConvertSNEStoPC(int address)
        {
            // WRAM is N/A to PC addressing
            if ((address & 0xFE0000) == 0x7E0000) return -1;

            // WRAM mirror & PPU regs are N/A to PC addressing
            if (((address & 0x400000) == 0) && ((address & 0x8000) == 0)) return -1;

            if (Data.GetROMMapMode() == Data.ROMMapMode.LoROM)
            {
                // SRAM is N/A to PC addressing
                if (((address & 0x700000) == 0x700000) && ((address & 0x8000) == 0)) return -1;

                return UnmirroredOffset(((address & 0x7F0000) >> 1) | (address & 0x7FFF));
            } else if (Data.GetROMMapMode() == Data.ROMMapMode.HiROM)
            {
                return UnmirroredOffset(address & 0x3FFFFF);
            } else // if (Data.GetROMMapMode() == Data.ROMMapMode.ExHiROM)
            {
                return UnmirroredOffset(((~address & 0x800000) >> 1) | (address & 0x3FFFFF));
            }
        }

        private static int UnmirroredOffset(int offset)
        {
            int size = Data.GetROMSize();

            // most of the time this is true; for efficiency
            if (offset < size) return offset;

            int repeatSize = 0x8000;
            while (repeatSize < size) repeatSize <<= 1;

            int repeatedOffset = offset % repeatSize;

            // this will then be true for ROM sizes of powers of 2
            if (repeatedOffset < size) return repeatedOffset;

            // for ROM sizes not powers of 2, it's kinda ugly
            int i = 0;
            int sizeOfSmallerSection = repeatSize / (4 << i);

            while (repeatedOffset >= repeatSize / 2 + sizeOfSmallerSection)
            {
                i++;
                sizeOfSmallerSection = repeatSize / (4 << i);
            }

            while (repeatedOffset >= size) repeatedOffset -= sizeOfSmallerSection;
            return repeatedOffset;
        }

        public static byte[] IntegerToByteArray(int a)
        {
            return new byte[]
            {
                (byte)a,
                (byte)(a >> 8),
                (byte)(a >> 16),
                (byte)(a >> 24)
            };
        }

        public static void IntegerIntoByteArray(int a, byte[] data, int offset)
        {
            byte[] arr = IntegerToByteArray(a);
            for (int i = 0; i < arr.Length; i++) data[offset + i] = arr[i];
        }

        public static void IntegerIntoByteList(int a, List<byte> list)
        {
            byte[] arr = IntegerToByteArray(a);
            for (int i = 0; i < arr.Length; i++) list.Add(arr[i]);
        }

        public static int ByteArrayToInteger(byte[] data, int offset = 0)
        {
            return
                data[offset] |
                (data[offset + 1] << 8) |
                (data[offset + 2] << 16) |
                (data[offset + 3] << 24);
        }

        public static string TypeToString(Data.FlagType flag)
        {
            switch (flag)
            {
                case Data.FlagType.Unreached: return "Unreached";
                case Data.FlagType.Opcode: return "Opcode";
                case Data.FlagType.Operand: return "Operand";
                case Data.FlagType.Data8Bit: return "Data (8-bit)";
                case Data.FlagType.Graphics: return "Graphics";
                case Data.FlagType.Music: return "Music";
                case Data.FlagType.Empty: return "Empty";
                case Data.FlagType.Data16Bit: return "Data (16-bit)";
                case Data.FlagType.Pointer16Bit: return "Pointer (16-bit)";
                case Data.FlagType.Data24Bit: return "Data (24-bit)";
                case Data.FlagType.Pointer24Bit: return "Pointer (24-bit)";
                case Data.FlagType.Data32Bit: return "Data (32-bit)";
                case Data.FlagType.Pointer32Bit: return "Pointer (32-bit)";
                case Data.FlagType.Text: return "Text";
            }
            return "";
        }

        public static string ArchToString(Data.Architechture arch)
        {
            switch (arch)
            {
                case Data.Architechture.CPU65C816: return "65C816";
                case Data.Architechture.APUSPC700: return "SPC700";
                case Data.Architechture.GPUSuperFX: return "SuperFX";
            }
            return "";
        }

        public static string PointToString(Data.InOutPoint point)
        {
            string result;

            if ((point & Data.InOutPoint.EndPoint) == Data.InOutPoint.EndPoint) result = "X";
            else if ((point & Data.InOutPoint.OutPoint) == Data.InOutPoint.OutPoint) result = "<";
            else result = " ";

            result += ((point & Data.InOutPoint.ReadPoint) == Data.InOutPoint.ReadPoint) ? "*" : " ";
            result += ((point & Data.InOutPoint.InPoint) == Data.InOutPoint.InPoint) ? ">" : " ";

            return result;
        }

        public static string BoolToSize(bool b)
        {
            return b ? "8" : "16";
        }

        public static string NumberToBaseString(int v, NumberBase noBase, int d = -1, bool showPrefix = false)
        {
            int digits = d < 0 ? (int)noBase : d;
            switch (noBase)
            {
                case NumberBase.Decimal:
                    if (digits == 0) return v.ToString("D");
                    return v.ToString("D" + digits);
                case NumberBase.Hexadecimal:
                    if (digits == 0) return v.ToString("X");
                    return (showPrefix ? "$" : "") + v.ToString("X" + digits);
                case NumberBase.Binary:
                    string b = "";
                    int i = 0;
                    while (digits == 0 ? v > 0 : i < digits)
                    {
                        b += (v & 1);
                        v >>= 1;
                        i++;
                    }
                    return (showPrefix ? "%" : "") + b;
            }
            return "";
        }
    }
}

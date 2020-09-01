using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

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

        public static int GetROMDoubleWord(int offset)
        {
            if (offset + 3 < Data.GetROMSize())
                return Data.GetROMByte(offset) + (Data.GetROMByte(offset + 1) << 8) + (Data.GetROMByte(offset + 2) << 16) + (Data.GetROMByte(offset + 3) << 24);
            return -1;
        }

        public static int GetIntermediateAddressOrPointer(int offset)
        {
            switch (Data.GetFlag(offset))
            {
                case Data.FlagType.Unreached:
                case Data.FlagType.Opcode:
                    return GetIntermediateAddress(offset);
                case Data.FlagType.Pointer16Bit:
                    int bank = Data.GetDataBank(offset);
                    return (bank << 16) | GetROMWord(offset);
                case Data.FlagType.Pointer24Bit:
                case Data.FlagType.Pointer32Bit:
                    return GetROMLong(offset);
            }
            return -1;
        }

        public static int GetIntermediateAddress(int offset)
        {
            switch (Data.GetArchitechture(offset))
            {
                case Data.Architechture.CPU65C816: return CPU65C816.GetIntermediateAddress(offset);
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

        public static string GetFormattedBytes(int offset, int step, int bytes)
        {
            string res = "";
            switch (step)
            {
                case 1: res = "db "; break;
                case 2: res = "dw "; break;
                case 3: res = "dl "; break;
                case 4: res = "dd "; break;
            }

            for (int i = 0; i < bytes; i += step)
            {
                if (i > 0) res += ",";

                switch (step)
                {
                    case 1: res += NumberToBaseString(Data.GetROMByte(offset + i), NumberBase.Hexadecimal, 2, true); break;
                    case 2: res += NumberToBaseString(GetROMWord(offset + i), NumberBase.Hexadecimal, 4, true); break;
                    case 3: res += NumberToBaseString(GetROMLong(offset + i), NumberBase.Hexadecimal, 6, true); break;
                    case 4: res += NumberToBaseString(GetROMDoubleWord(offset + i), NumberBase.Hexadecimal, 8, true); break;
                }
            }

            return res;
        }

        public static string GetPointer(int offset, int bytes)
        {
            int ia = -1;
            string format = "", param = "";
            switch (bytes)
            {
                case 2:
                    ia = (Data.GetDataBank(offset) << 16) | GetROMWord(offset);
                    format = "dw {0}";
                    param = NumberToBaseString(GetROMWord(offset), NumberBase.Hexadecimal, 4, true);
                    break;
                case 3:
                    ia = GetROMLong(offset);
                    format = "dl {0}";
                    param = NumberToBaseString(GetROMLong(offset), NumberBase.Hexadecimal, 6, true);
                    break;
                case 4:
                    ia = GetROMLong(offset);
                    format = "dl {0}" + string.Format(" : db {0}", NumberToBaseString(Data.GetROMByte(offset + 3), NumberBase.Hexadecimal, 2, true));
                    param = NumberToBaseString(GetROMLong(offset), NumberBase.Hexadecimal, 6, true);
                    break;
            }

            int pc = ConvertSNEStoPC(ia);
            if (pc >= 0 && Data.GetLabelName(ia) != "") param = Data.GetLabelName(ia);
            return string.Format(format, param);
        }

        public static string GetFormattedText(int offset, int bytes)
        {
            string text = "db \"";
            for (int i = 0; i < bytes; i++) text += (char)Data.GetROMByte(offset + i);
            return text + "\"";
        }

        public static string GetDefaultLabel(int address)
        {
            int pc = ConvertSNEStoPC(address);
            return string.Format("{0}_{1}", TypeToLabel(Data.GetFlag(pc)), NumberToBaseString(address, NumberBase.Hexadecimal, 6));
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
            else if (Data.GetROMMapMode() == Data.ROMMapMode.ExHiROM)
            {
                if (offset < 0x40000) offset |= 0xC00000;
                else if (offset >= 0x7E0000) offset &= 0x3FFFFF;
            }
            else
            {
                if (offset >= 0x400000 && Data.GetROMMapMode() == Data.ROMMapMode.ExSA1ROM)
                {
                    offset += 0x800000;
                }
                else
                {
                    offset = ((offset & 0x3F8000) << 1) | 0x8000 | (offset & 0x7FFF);
                    if (offset >= 0x400000) offset += 0x400000;
                }
            }
            return offset;
        }

        public static int ConvertSNEStoPC(int address)
        {
            // WRAM is N/A to PC addressing
            if ((address & 0xFE0000) == 0x7E0000) return -1;

            // WRAM mirror & PPU regs are N/A to PC addressing
            if (((address & 0x400000) == 0) && ((address & 0x8000) == 0)) return -1;

            switch (Data.GetROMMapMode())
            {
                case Data.ROMMapMode.LoROM:
                    {
                        // SRAM is N/A to PC addressing
                        if (((address & 0x700000) == 0x700000) && ((address & 0x8000) == 0)) return -1;

                        return UnmirroredOffset(((address & 0x7F0000) >> 1) | (address & 0x7FFF));
                    }
                case Data.ROMMapMode.HiROM:
                    {
                        return UnmirroredOffset(address & 0x3FFFFF);
                    }
                case Data.ROMMapMode.SuperMMC:
                    {
                        return UnmirroredOffset(address & 0x3FFFFF); // todo, treated as hirom atm
                    }
                case Data.ROMMapMode.SA1ROM:
                case Data.ROMMapMode.ExSA1ROM:
                    {
                        // BW-RAM is N/A to PC addressing
                        if (address >= 0x400000 && address <= 0x7FFFFF) return -1;

                        if (address >= 0xC00000)
                        {
                            if (Data.GetROMMapMode() == Data.ROMMapMode.ExSA1ROM)
                                return UnmirroredOffset(address & 0x7FFFFF);
                            else
                                return UnmirroredOffset(address & 0x3FFFFF);
                        }
                        else
                        {
                            if (address >= 0x800000) address -= 0x400000;

                            // SRAM is N/A to PC addressing
                            if (((address & 0x8000) == 0)) return -1;

                            return UnmirroredOffset(((address & 0x7F0000) >> 1) | (address & 0x7FFF));
                        }
                    }
                case Data.ROMMapMode.SuperFX:
                    {
                        // BW-RAM is N/A to PC addressing
                        if (address >= 0x600000 && address <= 0x7FFFFF) return -1;

                        if (address < 0x400000)
                        {
                            return UnmirroredOffset(((address & 0x7F0000) >> 1) | (address & 0x7FFF));
                        } else if (address < 0x600000)
                        {
                            return UnmirroredOffset(address & 0x3FFFFF);
                        } else if (address < 0xC00000)
                        {
                            return 0x200000 + UnmirroredOffset(((address & 0x7F0000) >> 1) | (address & 0x7FFF));
                        } else
                        {
                            return 0x400000 + UnmirroredOffset(address & 0x3FFFFF);
                        }
                    }
                case Data.ROMMapMode.ExHiROM:
                    {
                        return UnmirroredOffset(((~address & 0x800000) >> 1) | (address & 0x3FFFFF));
                    }
                case Data.ROMMapMode.ExLoROM:
                    {
                        // SRAM is N/A to PC addressing
                        if (((address & 0x700000) == 0x700000) && ((address & 0x8000) == 0)) return -1;

                        return UnmirroredOffset((((address ^ 0x800000) & 0xFF0000) >> 1) | (address & 0x7FFF));
                    }
                default:
                    {
                        return -1;
                    }
            }
        }

        public static string ReadZipString(byte[] unzipped, ref int pointer)
        {
            var label = "";
            while (unzipped[pointer] != 0)
                label += (char)unzipped[pointer++];
            pointer++;
            return label;
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
            int sizeOfSmallerSection = 0x8000;
            while (size % (sizeOfSmallerSection << 1) == 0) sizeOfSmallerSection <<= 1;

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

        // deal with addresses that look like this,
        // might be pasted from other editors
        // C0FFFF
        // $C0FFFF
        // C7/AAAA
        // $C6/BBBB
        public static bool StripFormattedAddress(ref string addressTxt, NumberStyles style, out int address)
        {
            address = -1;

            if (string.IsNullOrEmpty(addressTxt))
                return false;

            var inputText = new string(Array.FindAll<char>(addressTxt.ToCharArray(), (c => 
                (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            )));

            if (int.TryParse(inputText, style, null, out address))
            {
                addressTxt = inputText;
                return true;
            }

            return false;
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

        public static string TypeToLabel(Data.FlagType flag)
        {
            switch (flag)
            {
                case Data.FlagType.Unreached: return "UNREACH";
                case Data.FlagType.Opcode: return "CODE";
                case Data.FlagType.Operand: return "LOOSE_OP";
                case Data.FlagType.Data8Bit: return "DATA8";
                case Data.FlagType.Graphics: return "GFX";
                case Data.FlagType.Music: return "MUSIC";
                case Data.FlagType.Empty: return "EMPTY";
                case Data.FlagType.Data16Bit: return "DATA16";
                case Data.FlagType.Pointer16Bit: return "PTR16";
                case Data.FlagType.Data24Bit: return "DATA24";
                case Data.FlagType.Pointer24Bit: return "PTR24";
                case Data.FlagType.Data32Bit: return "DATA32";
                case Data.FlagType.Pointer32Bit: return "PTR32";
                case Data.FlagType.Text: return "TEXT";
            }
            return "";
        }

        public static int TypeStepSize(Data.FlagType flag)
        {
            switch (flag)
            {
                case Data.FlagType.Unreached:
                case Data.FlagType.Opcode:
                case Data.FlagType.Operand:
                case Data.FlagType.Data8Bit:
                case Data.FlagType.Graphics:
                case Data.FlagType.Music:
                case Data.FlagType.Empty:
                case Data.FlagType.Text:
                    return 1;
                case Data.FlagType.Data16Bit:
                case Data.FlagType.Pointer16Bit:
                    return 2;
                case Data.FlagType.Data24Bit:
                case Data.FlagType.Pointer24Bit:
                    return 3;
                case Data.FlagType.Data32Bit:
                case Data.FlagType.Pointer32Bit:
                    return 4;
            }
            return 0;
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

        public static byte[] StringToByteArray(string s)
        {
            byte[] array = new byte[s.Length + 1];
            for (int i = 0; i < s.Length; i++) array[i] = (byte)s[i];
            array[s.Length] = 0;
            return array;
        }

        public static void PaintCell(int offset, DataGridViewCellStyle style, int column, int selOffset)
        {
            // editable cells show up green
            if (column == 0 || column == 8 || column == 9 || column == 12) style.SelectionBackColor = Color.Chartreuse;

            switch (Data.GetFlag(offset))
            {
                case Data.FlagType.Unreached:
                    style.BackColor = Color.LightGray;
                    style.ForeColor = Color.DarkSlateGray;
                    break;
                case Data.FlagType.Opcode:
                    int opcode = Data.GetROMByte(offset);
                    switch (column)
                    {
                        case 4: // <*>
                            Data.InOutPoint point = Data.GetInOutPoint(offset);
                            int r = 255, g = 255, b = 255;
                            if ((point & (Data.InOutPoint.EndPoint | Data.InOutPoint.OutPoint)) != 0) g -= 50;
                            if ((point & (Data.InOutPoint.InPoint)) != 0) r -= 50;
                            if ((point & (Data.InOutPoint.ReadPoint)) != 0) b -= 50;
                            style.BackColor = Color.FromArgb(r, g, b);
                            break;
                        case 5: // Instruction
                            if (opcode == 0x40 || opcode == 0xCB || opcode == 0xDB || opcode == 0xF8 // RTI WAI STP SED
                                || opcode == 0xFB || opcode == 0x00 || opcode == 0x02 || opcode == 0x42 // XCE BRK COP WDM
                            ) style.BackColor = Color.Yellow;
                            break;
                        case 8: // Data Bank
                            if (opcode == 0xAB || opcode == 0x44 || opcode == 0x54) // PLB MVP MVN
                                style.BackColor = Color.OrangeRed;
                            else if (opcode == 0x8B) // PHB
                                style.BackColor = Color.Yellow;
                            break;
                        case 9: // Direct Page
                            if (opcode == 0x2B || opcode == 0x5B) // PLD TCD
                                style.BackColor = Color.OrangeRed;
                            if (opcode == 0x0B || opcode == 0x7B) // PHD TDC
                                style.BackColor = Color.Yellow;
                            break;
                        case 10: // M Flag
                        case 11: // X Flag
                            int mask = column == 10 ? 0x20 : 0x10;
                            if (opcode == 0x28 || ((opcode == 0xC2 || opcode == 0xE2) // PLP SEP REP
                                && (Data.GetROMByte(offset + 1) & mask) != 0)) // relevant bit set
                                style.BackColor = Color.OrangeRed;
                            if (opcode == 0x08) // PHP
                                style.BackColor = Color.Yellow;
                            break;
                    }
                    break;
                case Data.FlagType.Operand:
                    style.ForeColor = Color.LightGray;
                    break;
                case Data.FlagType.Graphics:
                    style.BackColor = Color.LightPink;
                    break;
                case Data.FlagType.Music:
                    style.BackColor = Color.PowderBlue;
                    break;
                case Data.FlagType.Data8Bit:
                case Data.FlagType.Data16Bit:
                case Data.FlagType.Data24Bit:
                case Data.FlagType.Data32Bit:
                    style.BackColor = Color.NavajoWhite;
                    break;
                case Data.FlagType.Pointer16Bit:
                case Data.FlagType.Pointer24Bit:
                case Data.FlagType.Pointer32Bit:
                    style.BackColor = Color.Orchid;
                    break;
                case Data.FlagType.Text:
                    style.BackColor = Color.Aquamarine;
                    break;
                case Data.FlagType.Empty:
                    style.BackColor = Color.DarkSlateGray;
                    style.ForeColor = Color.LightGray;
                    break;
            }

            if (selOffset >= 0 && selOffset < Data.GetROMSize())
            {
                if (column == 1
                    //&& (Data.GetFlag(selOffset) == Data.FlagType.Opcode || Data.GetFlag(selOffset) == Data.FlagType.Unreached)
                    && ConvertSNEStoPC(GetIntermediateAddressOrPointer(selOffset)) == offset
                ) style.BackColor = Color.DeepPink;

                if (column == 6
                    //&& (Data.GetFlag(offset) == Data.FlagType.Opcode || Data.GetFlag(offset) == Data.FlagType.Unreached)
                    && ConvertSNEStoPC(GetIntermediateAddressOrPointer(offset)) == selOffset
                ) style.BackColor = Color.DeepPink;
            }
        }
    }
}

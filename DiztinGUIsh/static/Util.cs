using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Forms;

namespace DiztinGUIsh
{
    public static class Util
    {
        public enum NumberBase
        {
            Decimal = 3, Hexadecimal = 2, Binary = 8
        }

        public static int ConvertSNESToPC(int address, Data.ROMMapMode mode, int size)
        {
            int _UnmirroredOffset(int offset)
            {
                return Util.UnmirroredOffset(offset, size);
            }

            // WRAM is N/A to PC addressing
            if ((address & 0xFE0000) == 0x7E0000) return -1;

            // WRAM mirror & PPU regs are N/A to PC addressing
            if (((address & 0x400000) == 0) && ((address & 0x8000) == 0)) return -1;

            switch (mode)
            {
                case Data.ROMMapMode.LoROM:
                    {
                        // SRAM is N/A to PC addressing
                        if (((address & 0x700000) == 0x700000) && ((address & 0x8000) == 0)) return -1;

                        return _UnmirroredOffset(((address & 0x7F0000) >> 1) | (address & 0x7FFF));
                    }
                case Data.ROMMapMode.HiROM:
                    {
                        return _UnmirroredOffset(address & 0x3FFFFF);
                    }
                case Data.ROMMapMode.SuperMMC:
                    {
                        return _UnmirroredOffset(address & 0x3FFFFF); // todo, treated as hirom atm
                    }
                case Data.ROMMapMode.SA1ROM:
                case Data.ROMMapMode.ExSA1ROM:
                    {
                        // BW-RAM is N/A to PC addressing
                        if (address >= 0x400000 && address <= 0x7FFFFF) return -1;

                        if (address >= 0xC00000)
                        {
                            if (mode == Data.ROMMapMode.ExSA1ROM)
                                return _UnmirroredOffset(address & 0x7FFFFF);
                            else
                                return _UnmirroredOffset(address & 0x3FFFFF);
                        }
                        else
                        {
                            if (address >= 0x800000) address -= 0x400000;

                            // SRAM is N/A to PC addressing
                            if (((address & 0x8000) == 0)) return -1;

                            return _UnmirroredOffset(((address & 0x7F0000) >> 1) | (address & 0x7FFF));
                        }
                    }
                case Data.ROMMapMode.SuperFX:
                    {
                        // BW-RAM is N/A to PC addressing
                        if (address >= 0x600000 && address <= 0x7FFFFF) return -1;

                        if (address < 0x400000)
                        {
                            return _UnmirroredOffset(((address & 0x7F0000) >> 1) | (address & 0x7FFF));
                        }
                        else if (address < 0x600000)
                        {
                            return _UnmirroredOffset(address & 0x3FFFFF);
                        }
                        else if (address < 0xC00000)
                        {
                            return 0x200000 + _UnmirroredOffset(((address & 0x7F0000) >> 1) | (address & 0x7FFF));
                        }
                        else
                        {
                            return 0x400000 + _UnmirroredOffset(address & 0x3FFFFF);
                        }
                    }
                case Data.ROMMapMode.ExHiROM:
                    {
                        return _UnmirroredOffset(((~address & 0x800000) >> 1) | (address & 0x3FFFFF));
                    }
                case Data.ROMMapMode.ExLoROM:
                    {
                        // SRAM is N/A to PC addressing
                        if (((address & 0x700000) == 0x700000) && ((address & 0x8000) == 0)) return -1;

                        return _UnmirroredOffset((((address ^ 0x800000) & 0xFF0000) >> 1) | (address & 0x7FFF));
                    }
                default:
                    {
                        return -1;
                    }
            }
        }

        public static int ConvertPCtoSNES(int offset, Data.ROMMapMode romMapMode, Data.ROMSpeed romSpeed)
        {
            switch (romMapMode)
            {
                case Data.ROMMapMode.LoROM:
                    offset = ((offset & 0x3F8000) << 1) | 0x8000 | (offset & 0x7FFF);
                    if (romSpeed == Data.ROMSpeed.FastROM || offset >= 0x7E0000) offset |= 0x800000;
                    return offset;
                case Data.ROMMapMode.HiROM:
                    offset |= 0x400000;
                    if (romSpeed == Data.ROMSpeed.FastROM || offset >= 0x7E0000) offset |= 0x800000;
                    return offset;
                case Data.ROMMapMode.ExHiROM when offset < 0x40000:
                    offset |= 0xC00000;
                    return offset;
                case Data.ROMMapMode.ExHiROM:
                    if (offset >= 0x7E0000) offset &= 0x3FFFFF;
                    return offset;
                case Data.ROMMapMode.ExSA1ROM when offset >= 0x400000:
                    offset += 0x800000;
                    return offset;
            }

            offset = ((offset & 0x3F8000) << 1) | 0x8000 | (offset & 0x7FFF);
            if (offset >= 0x400000) offset += 0x400000;

            return offset;
        }

        public delegate int AddressConverter(int address);

        public static int ReadStringsTable(byte[] bytes, int starting_index, int stringsPerEntry, AddressConverter converter, Action<int, string[]> processTableEntry)
        {
            var strings = new List<string>();

            var pos = starting_index;
            var num_table_entries = Util.ByteArrayToInteger(bytes, pos);
            pos += 4;

            for (var entry = 0; entry < num_table_entries; ++entry)
            {
                var offset = converter(Util.ByteArrayToInteger(bytes, pos));
                pos += 4;

                strings.Clear();
                for (var j = 0; j < stringsPerEntry; ++j)
                {
                    pos += Util.ReadNullTerminatedString(bytes, pos, out var str);
                    strings.Add(str);
                }
                processTableEntry(offset, strings.ToArray());
            }

            return pos - starting_index;
        }

        public static int ReadNullTerminatedString(byte[] bytes, int starting_offset, out string str)
        {
            str = "";
            var pos = starting_offset;
            while (bytes[pos] != 0)
                str += (char)bytes[pos++];
            pos++;
            return pos - starting_offset;
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

        public static int UnmirroredOffset(int offset, int size)
        {
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

        public static string GetRomMapModeName(Data.ROMMapMode mode)
        {
            switch (mode)
            {
                case Data.ROMMapMode.ExSA1ROM:
                    return "SA-1 ROM (FuSoYa's 8MB mapper)";

                case Data.ROMMapMode.SA1ROM:
                    return "SA-1 ROM";

                case Data.ROMMapMode.SuperFX:
                    return "SuperFX";

                case Data.ROMMapMode.LoROM:
                    return "LoROM";

                case Data.ROMMapMode.HiROM:
                    return "HiROM";

                case Data.ROMMapMode.SuperMMC:
                    return "Super MMC";

                case Data.ROMMapMode.ExHiROM:
                    return "ExHiROM";

                case Data.ROMMapMode.ExLoROM:
                    return "ExLoROM";

                default:
                    return "Unknown mapping";
            }
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

        public static Data.ROMMapMode DetectROMMapMode(IReadOnlyList<byte> rom_bytes, out bool couldnt_detect)
        {
            couldnt_detect = false;

            if ((rom_bytes[Data.LOROM_SETTING_OFFSET] & 0xEF) == 0x23)
            {
                return rom_bytes.Count > 0x400000 ? Data.ROMMapMode.ExSA1ROM : Data.ROMMapMode.SA1ROM;
            }
            else if ((rom_bytes[Data.LOROM_SETTING_OFFSET] & 0xEC) == 0x20)
            {
                return (rom_bytes[Data.LOROM_SETTING_OFFSET + 1] & 0xF0) == 0x10 ? Data.ROMMapMode.SuperFX : Data.ROMMapMode.LoROM;
            }
            else if (rom_bytes.Count >= 0x10000 && (rom_bytes[Data.HIROM_SETTING_OFFSET] & 0xEF) == 0x21)
            {
                return Data.ROMMapMode.HiROM;
            }
            else if (rom_bytes.Count >= 0x10000 && (rom_bytes[Data.HIROM_SETTING_OFFSET] & 0xE7) == 0x22)
            {
                return Data.ROMMapMode.SuperMMC;
            }
            else if (rom_bytes.Count >= 0x410000 && (rom_bytes[Data.EXHIROM_SETTING_OFFSET] & 0xEF) == 0x25)
            {
                return Data.ROMMapMode.ExHiROM;
            }
            else
            {
                // detection failed. take our best guess.....
                couldnt_detect = true;
                return rom_bytes.Count > 0x40000 ? Data.ROMMapMode.ExLoROM : Data.ROMMapMode.LoROM;
            }
        }

        public static IEnumerable<string> ReadLines(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 0x1000, FileOptions.SequentialScan))
            using (var sr = new StreamReader(fs, Encoding.UTF8))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    yield return line;
                }
            }
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

        // read a fixed length string from an array of bytes. does not check for null termination
        public static string ReadStringFromByteArray(byte[] bytes, int count, int offset)
        {
            var myName = "";
            for (var i = 0; i < count; i++)
                myName += (char)bytes[offset - count + i];
            return myName;
        }

        public static long GetFileSizeInBytes(string filename)
        {
            FileInfo fi = new FileInfo(filename);
            if (!fi.Exists)
                return -1;

            return fi.Length;
        }
        public static void InvokeIfRequired(this ISynchronizeInvoke obj, MethodInvoker action)
        {
            if (obj.InvokeRequired)
            {
                var args = new object[0];
                obj.Invoke(action, args);
            }
            else
            {
                action();
            }
        }


        // https://stackoverflow.com/questions/33119119/unzip-byte-array-in-c-sharp
        public static byte[] TryUnzip(byte[] data)
        {
            try
            {
                using (MemoryStream comp = new MemoryStream(data))
                using (GZipStream gzip = new GZipStream(comp, CompressionMode.Decompress))
                using (MemoryStream res = new MemoryStream())
                {
                    gzip.CopyTo(res);
                    return res.ToArray();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
        public static byte[] TryZip(byte[] data)
        {
            try
            {
                using (MemoryStream comp = new MemoryStream())
                using (GZipStream gzip = new GZipStream(comp, CompressionMode.Compress))
                {
                    gzip.Write(data, 0, data.Length);
                    gzip.Close();
                    return comp.ToArray();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static byte[] ReadAllRomBytesFromFile(string filename)
        {
            var smc = File.ReadAllBytes(filename);
            var rom = new byte[smc.Length & 0x7FFFFC00];

            if ((smc.Length & 0x3FF) == 0x200)
                for (int i = 0; i < rom.Length; i++)
                    rom[i] = smc[i + 0x200];
            else if ((smc.Length & 0x3FF) != 0)
                throw new InvalidDataException("This ROM has an unusual size. It can't be opened.");
            else
                rom = smc;

            if (rom.Length < 0x8000)
                throw new InvalidDataException("This ROM is too small. It can't be opened.");

            return rom;
        }
        public static string PromptToSelectFile(string initialDirectory = null)
        {
            var open = new OpenFileDialog { InitialDirectory = initialDirectory };
            return open.ShowDialog() == DialogResult.OK ? open.FileName : null;
        }
    }
}

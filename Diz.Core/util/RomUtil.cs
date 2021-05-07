using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using Diz.Core.export;
using Diz.Core.model;
using JetBrains.Annotations;

namespace Diz.Core.util
{
    public enum RomSpeed : byte
    {
        SlowRom,
        FastRom,
        Unknown
    }
    
    public enum RomMapMode : byte
    {
        LoRom,

        HiRom,

        ExHiRom,

        [Description("SA - 1 ROM")] Sa1Rom,

        [Description("SA-1 ROM (FuSoYa's 8MB mapper)")]
        ExSa1Rom,

        SuperFx,

        [Description("Super MMC")] SuperMmc,

        ExLoRom
    }
    
    public static class RomUtil
    {
        public static int CalculateSnesOffsetWithWrap(int snesAddress, int offset)
        {
            return (GetBankFromSnesAddress(snesAddress) << 16) + ((snesAddress + offset) & 0xFFFF);
        }

        private static int GetBankFromSnesAddress(int snesAddress)
        {
            return (snesAddress >> 16) & 0xFF;
        }

        public static int GetBankSize(RomMapMode mode)
        {
            // todo
            return mode == RomMapMode.LoRom ? 0x8000 : 0x10000;
        }

        public static RomSpeed GetRomSpeed(int offset, IReadOnlyList<byte> romBytes) =>
            offset < romBytes.Count
                ? (romBytes[offset] & 0x10) != 0 ? RomSpeed.FastRom : RomSpeed.SlowRom
                : RomSpeed.Unknown;


        /// <summary>
        /// Return the "title" information (i.e. the name of the game) from the SNES ROM header
        /// </summary>
        /// <param name="allRomBytes">All the bytes in a ROM</param>
        /// <param name="romSettingOffset">Offset of the start of the SNES header section (title info is before this)</param>
        /// <returns>UTF8 string of the title, padded with spaces</returns>
        public static string GetCartridgeTitleFromRom(byte[] allRomBytes, int romSettingOffset) => 
            GetCartridgeTitleFromBuffer(allRomBytes, GetCartridgeTitleStartingRomOffset(romSettingOffset));

        // input: ROM setting offset (pcOffset, NOT snes address)
        public static int GetCartridgeTitleStartingRomOffset(int romSettingOffset) => 
            romSettingOffset - LengthOfTitleName;

        /// <summary>
        /// Return the "title" information (i.e. the name of the game) from an arbitrary buffer
        /// </summary>
        /// <param name="buffer">Array of bytes</param>
        /// <param name="index">Index into the array to start with</param>
        /// <returns>UTF8 string of the title, padded with spaces</returns>
        public static string GetCartridgeTitleFromBuffer(byte[] buffer, int index = 0) => 
            ByteUtil.ReadShiftJisEncodedString(buffer, index, LengthOfTitleName);

        public static int ConvertSnesToPc(int address, RomMapMode mode, int size)
        {
            int UnmirroredOffset(int offset) => RomUtil.UnmirroredOffset(offset, size);

            // WRAM is N/A to PC addressing
            if ((address & 0xFE0000) == 0x7E0000) return -1;

            // WRAM mirror & PPU regs are N/A to PC addressing
            if (((address & 0x400000) == 0) && ((address & 0x8000) == 0)) return -1;

            switch (mode)
            {
                case RomMapMode.LoRom:
                {
                    // SRAM is N/A to PC addressing
                    if (((address & 0x700000) == 0x700000) && ((address & 0x8000) == 0)) 
                        return -1;

                    return UnmirroredOffset(((address & 0x7F0000) >> 1) | (address & 0x7FFF));
                }
                case RomMapMode.HiRom:
                {
                    return UnmirroredOffset(address & 0x3FFFFF);
                }
                case RomMapMode.SuperMmc:
                {
                    return UnmirroredOffset(address & 0x3FFFFF); // todo, treated as hirom atm
                }
                case RomMapMode.Sa1Rom:
                case RomMapMode.ExSa1Rom:
                {
                    // BW-RAM is N/A to PC addressing
                    if (address >= 0x400000 && address <= 0x7FFFFF) return -1;

                    if (address >= 0xC00000)
                        return mode == RomMapMode.ExSa1Rom ? UnmirroredOffset(address & 0x7FFFFF) : UnmirroredOffset(address & 0x3FFFFF);

                    if (address >= 0x800000) address -= 0x400000;

                    // SRAM is N/A to PC addressing
                    if (((address & 0x8000) == 0)) return -1;

                    return UnmirroredOffset(((address & 0x7F0000) >> 1) | (address & 0x7FFF));
                }
                case RomMapMode.SuperFx:
                {
                    // BW-RAM is N/A to PC addressing
                    if (address >= 0x600000 && address <= 0x7FFFFF) 
                        return -1;

                    if (address < 0x400000)
                        return UnmirroredOffset(((address & 0x7F0000) >> 1) | (address & 0x7FFF));

                    if (address < 0x600000)
                        return UnmirroredOffset(address & 0x3FFFFF);

                    if (address < 0xC00000)
                        return 0x200000 + UnmirroredOffset(((address & 0x7F0000) >> 1) | (address & 0x7FFF));

                    return 0x400000 + UnmirroredOffset(address & 0x3FFFFF);
                }
                case RomMapMode.ExHiRom:
                {
                    return UnmirroredOffset(((~address & 0x800000) >> 1) | (address & 0x3FFFFF));
                }
                case RomMapMode.ExLoRom:
                {
                    // SRAM is N/A to PC addressing
                    if (((address & 0x700000) == 0x700000) && ((address & 0x8000) == 0)) 
                        return -1;

                    return UnmirroredOffset((((address ^ 0x800000) & 0xFF0000) >> 1) | (address & 0x7FFF));
                }
                default:
                {
                    return -1;
                }
            }
        }

        public static int ConvertPCtoSnes(int offset, RomMapMode romMapMode, RomSpeed romSpeed)
        {
            switch (romMapMode)
            {
                case RomMapMode.LoRom:
                    offset = ((offset & 0x3F8000) << 1) | 0x8000 | (offset & 0x7FFF);
                    if (romSpeed == RomSpeed.FastRom || offset >= 0x7E0000) offset |= 0x800000;
                    return offset;
                case RomMapMode.HiRom:
                    offset |= 0x400000;
                    if (romSpeed == RomSpeed.FastRom || offset >= 0x7E0000) offset |= 0x800000;
                    return offset;
                case RomMapMode.ExHiRom when offset < 0x40000:
                    offset |= 0xC00000;
                    return offset;
                case RomMapMode.ExHiRom:
                    if (offset >= 0x7E0000) offset &= 0x3FFFFF;
                    return offset;
                case RomMapMode.ExSa1Rom when offset >= 0x400000:
                    offset += 0x800000;
                    return offset;
            }

            offset = ((offset & 0x3F8000) << 1) | 0x8000 | (offset & 0x7FFF);
            if (offset >= 0x400000) offset += 0x400000;

            return offset;
        }

        public static int UnmirroredOffset(int offset, int size)
        {
            // most of the time this is true; for efficiency
            if (offset < size) 
                return offset;

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

        // TODO: these can be attributes on the enum itself. like [AsmLabel("UNREACH")]
        public static string TypeToLabel(Data.FlagType flag)
        {
            return flag switch
            {
                Data.FlagType.Unreached => "UNREACH",
                Data.FlagType.Opcode => "CODE",
                Data.FlagType.Operand => "LOOSE_OP",
                Data.FlagType.Data8Bit => "DATA8",
                Data.FlagType.Graphics => "GFX",
                Data.FlagType.Music => "MUSIC",
                Data.FlagType.Empty => "EMPTY",
                Data.FlagType.Data16Bit => "DATA16",
                Data.FlagType.Pointer16Bit => "PTR16",
                Data.FlagType.Data24Bit => "DATA24",
                Data.FlagType.Pointer24Bit => "PTR24",
                Data.FlagType.Data32Bit => "DATA32",
                Data.FlagType.Pointer32Bit => "PTR32",
                Data.FlagType.Text => "TEXT",
                _ => ""
            };
        }

        public static int GetByteLengthForFlag(Data.FlagType flag)
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

        public static RomMapMode DetectRomMapMode(IReadOnlyList<byte> romBytes, out bool detectedValidRomMapType)
        {
            detectedValidRomMapType = true;

            if ((romBytes[Data.LoromSettingOffset] & 0xEF) == 0x23)
                return romBytes.Count > 0x400000 ? RomMapMode.ExSa1Rom : RomMapMode.Sa1Rom;

            if ((romBytes[Data.LoromSettingOffset] & 0xEC) == 0x20)
                return (romBytes[Data.LoromSettingOffset + 1] & 0xF0) == 0x10 ? RomMapMode.SuperFx : RomMapMode.LoRom;

            if (romBytes.Count >= 0x10000 && (romBytes[Data.HiromSettingOffset] & 0xEF) == 0x21)
                return RomMapMode.HiRom;

            if (romBytes.Count >= 0x10000 && (romBytes[Data.HiromSettingOffset] & 0xE7) == 0x22)
                return RomMapMode.SuperMmc;

            if (romBytes.Count >= 0x410000 && (romBytes[Data.ExhiromSettingOffset] & 0xEF) == 0x25)
                return RomMapMode.ExHiRom;

            // detection failed. take our best guess.....
            detectedValidRomMapType = false;
            return romBytes.Count > 0x40000 ? RomMapMode.ExLoRom : RomMapMode.LoRom;
        }

        public static int GetRomSettingOffset(RomMapMode mode)
        {
            return mode switch
            {
                RomMapMode.LoRom => Data.LoromSettingOffset,
                RomMapMode.HiRom => Data.HiromSettingOffset,
                RomMapMode.ExHiRom => Data.ExhiromSettingOffset,
                RomMapMode.ExLoRom => Data.ExloromSettingOffset,
                _ => Data.LoromSettingOffset
            };
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

        public static byte[] ReadAllRomBytesFromFile(string filename)
        {
            var smc = File.ReadAllBytes(filename);
            var rom = new byte[smc.Length & 0x7FFFFC00];

            if ((smc.Length & 0x3FF) == 0x200)
                // skip and dont include the SMC header
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

        public static Dictionary<int, Data.FlagType> GenerateHeaderFlags(int romSettingsOffset, byte[] romBytes)
        {
            var flags = new Dictionary<int, Data.FlagType>();
            
            for (int i = 0; i < LengthOfTitleName; i++)
                flags.Add(romSettingsOffset - LengthOfTitleName + i, Data.FlagType.Text);
            
            for (int i = 0; i < 7; i++) 
                flags.Add(romSettingsOffset + i, Data.FlagType.Data8Bit);
            
            for (int i = 0; i < 4; i++) 
                flags.Add(romSettingsOffset + 7 + i, Data.FlagType.Data16Bit);
            
            for (int i = 0; i < 0x20; i++) 
                flags.Add(romSettingsOffset + 11 + i, Data.FlagType.Pointer16Bit);

            if (romBytes[romSettingsOffset - 1] == 0)
            {
                flags.Remove(romSettingsOffset - 1);
                flags.Add(romSettingsOffset - 1, Data.FlagType.Data8Bit);
                for (int i = 0; i < 0x10; i++) 
                    flags.Add(romSettingsOffset - 0x25 + i, Data.FlagType.Data8Bit);
            }
            else if (romBytes[romSettingsOffset + 5] == 0x33)
            {
                for (int i = 0; i < 6; i++) 
                    flags.Add(romSettingsOffset - 0x25 + i, Data.FlagType.Text);

                for (int i = 0; i < 10; i++) 
                    flags.Add(romSettingsOffset - 0x1F + i, Data.FlagType.Data8Bit);
            }

            return flags;
        }


        public static Dictionary<int, Label> GenerateVectorLabels(Dictionary<string, bool> vectorNames, int romSettingsOffset, IReadOnlyList<byte> romBytes, RomMapMode mode)
        {
            // TODO: probably better to just use a data structure for this instead of generating the 
            // offsets with table/entry vars

            var labels = new Dictionary<int, Label>();

            var baseOffset = romSettingsOffset + 15;

            var table = 0; const int tableCount = 2;
            var entry = 0; const int entryCount = 6;
            foreach (var vectorEntry in vectorNames)
            {
                Debug.Assert(table >= 0 && table < tableCount);
                Debug.Assert(entry >= 0 && entry < entryCount);
                // table = 0,1              // which table of Native vs Emulation
                // entry = 0,1,2,3,4,5      // which offset
                //
                // 16*i = 16,32,

                var index = baseOffset + (16 * table) + (2 * entry);
                var offset = romBytes[index] + (romBytes[index + 1] << 8);
                var pc = ConvertSnesToPc(offset, mode, romBytes.Count);
                if (pc >= 0 && pc < romBytes.Count && !labels.ContainsKey(offset))
                    labels.Add(offset, new Label() { Name = vectorEntry.Key });

                if (++entry < entryCount)
                    continue;

                entry = 0;
                if (++table >= tableCount)
                    break;
            }

            return labels;
        }

        public const int LengthOfTitleName = 0x15;

        public static LogCreator.OutputResult GetSampleAssemblyOutput(LogWriterSettings sampleSettings)
        {
            var sampleRomData = SampleRomData.CreateSampleData();
            sampleSettings.Structure = LogCreator.FormatStructure.SingleFile;
            sampleSettings.FileOrFolderOutPath = "";
            sampleSettings.OutputToString = true;
            sampleSettings.RomSizeOverride = sampleRomData.OriginalRomSizeBeforePadding;
            var lc = new LogCreator()
            {
                Settings = sampleSettings,
                Data = sampleRomData,
            };
            return lc.CreateLog();
        }
    }
}

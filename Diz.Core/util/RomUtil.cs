using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Diz.Core.export;
using Diz.Core.model;
using Diz.Core.model.byteSources;

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
        public const int LoromSettingOffset = 0x7FD5;
        public const int HiromSettingOffset = 0xFFD5;
        public const int ExhiromSettingOffset = 0x40FFD5;
        public const int ExloromSettingOffset = 0x407FD5;
        
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

        // verify the data in the provided ROM bytes matches the data we expect it to have.
        // returns error message if it's not identical, or null if everything is OK.
        public static string IsThisRomIsIdenticalToUs(byte[] rom,
            RomMapMode mode, string requiredGameNameMatch, int requiredRomChecksumMatch)
        {
            var romSettingsOffset = GetRomSettingOffset(mode);
            if (rom.Length <= romSettingsOffset + 10)
                return "The linked ROM is too small. It can't be opened.";

            var internalGameNameToVerify = GetRomTitleName(rom, romSettingsOffset);
            var checksumToVerify = ByteUtil.ByteArrayToInt32(rom, romSettingsOffset + 7);

            if (internalGameNameToVerify != requiredGameNameMatch)
                return $"The linked ROM's internal name '{internalGameNameToVerify}' doesn't " +
                       $"match the project's internal name of '{requiredGameNameMatch}'.";

            if (checksumToVerify != requiredRomChecksumMatch)
                return $"The linked ROM's checksums '{checksumToVerify:X8}' " +
                       $"don't match the project's checksums of '{requiredRomChecksumMatch:X8}'.";

            return null;
        }

        public static string GetRomTitleName(byte[] rom, int romSettingOffset)
        {
            var offsetOfGameTitle = romSettingOffset - LengthOfTitleName;
            var internalGameNameToVerify = ReadStringFromByteArray(rom, LengthOfTitleName, offsetOfGameTitle);
            return internalGameNameToVerify;
        }

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
        public static string TypeToLabel(FlagType flag)
        {
            return flag switch
            {
                FlagType.Unreached => "UNREACH",
                FlagType.Opcode => "CODE",
                FlagType.Operand => "LOOSE_OP",
                FlagType.Data8Bit => "DATA8",
                FlagType.Graphics => "GFX",
                FlagType.Music => "MUSIC",
                FlagType.Empty => "EMPTY",
                FlagType.Data16Bit => "DATA16",
                FlagType.Pointer16Bit => "PTR16",
                FlagType.Data24Bit => "DATA24",
                FlagType.Pointer24Bit => "PTR24",
                FlagType.Data32Bit => "DATA32",
                FlagType.Pointer32Bit => "PTR32",
                FlagType.Text => "TEXT",
                _ => ""
            };
        }

        public static int GetByteLengthForFlag(FlagType flag)
        {
            switch (flag)
            {
                case FlagType.Unreached:
                case FlagType.Opcode:
                case FlagType.Operand:
                case FlagType.Data8Bit:
                case FlagType.Graphics:
                case FlagType.Music:
                case FlagType.Empty:
                case FlagType.Text:
                    return 1;
                case FlagType.Data16Bit:
                case FlagType.Pointer16Bit:
                    return 2;
                case FlagType.Data24Bit:
                case FlagType.Pointer24Bit:
                    return 3;
                case FlagType.Data32Bit:
                case FlagType.Pointer32Bit:
                    return 4;
            }
            return 0;
        }

        public static RomMapMode DetectRomMapMode(IReadOnlyList<byte> romBytes, out bool detectedValidRomMapType)
        {
            detectedValidRomMapType = true;

            if ((romBytes[RomUtil.LoromSettingOffset] & 0xEF) == 0x23)
                return romBytes.Count > 0x400000 ? RomMapMode.ExSa1Rom : RomMapMode.Sa1Rom;

            if ((romBytes[RomUtil.LoromSettingOffset] & 0xEC) == 0x20)
                return (romBytes[RomUtil.LoromSettingOffset + 1] & 0xF0) == 0x10 ? RomMapMode.SuperFx : RomMapMode.LoRom;

            if (romBytes.Count >= 0x10000 && (romBytes[RomUtil.HiromSettingOffset] & 0xEF) == 0x21)
                return RomMapMode.HiRom;

            if (romBytes.Count >= 0x10000 && (romBytes[RomUtil.HiromSettingOffset] & 0xE7) == 0x22)
                return RomMapMode.SuperMmc;

            if (romBytes.Count >= 0x410000 && (romBytes[RomUtil.ExhiromSettingOffset] & 0xEF) == 0x25)
                return RomMapMode.ExHiRom;

            // detection failed. take our best guess.....
            detectedValidRomMapType = false;
            return romBytes.Count > 0x40000 ? RomMapMode.ExLoRom : RomMapMode.LoRom;
        }

        public static int GetRomSettingOffset(RomMapMode mode)
        {
            return mode switch
            {
                RomMapMode.LoRom => RomUtil.LoromSettingOffset,
                RomMapMode.HiRom => RomUtil.HiromSettingOffset,
                RomMapMode.ExHiRom => RomUtil.ExhiromSettingOffset,
                RomMapMode.ExLoRom => RomUtil.ExloromSettingOffset,
                _ => RomUtil.LoromSettingOffset
            };
        }

        public static string PointToString(InOutPoint point)
        {
            string result;

            if ((point & InOutPoint.EndPoint) == InOutPoint.EndPoint) result = "X";
            else if ((point & InOutPoint.OutPoint) == InOutPoint.OutPoint) result = "<";
            else result = " ";

            result += ((point & InOutPoint.ReadPoint) == InOutPoint.ReadPoint) ? "*" : " ";
            result += ((point & InOutPoint.InPoint) == InOutPoint.InPoint) ? ">" : " ";

            return result;
        }

        public static string BoolToSize(bool b)
        {
            return b ? "8" : "16";
        }

        // read a fixed length string from an array of bytes. does not check for null termination
        public static string ReadStringFromByteArray(byte[] bytes, int count, int offset)
        {
            var myName = "";
            for (var i = 0; i < count; i++)
                myName += (char)bytes[offset + i];

            return myName;
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

        public static void GenerateHeaderFlags(int romSettingsOffset, IDictionary<int, FlagType> flags, byte[] romBytes)
        {
            for (int i = 0; i < LengthOfTitleName; i++)
                flags.Add(romSettingsOffset - LengthOfTitleName + i, FlagType.Text);
            
            for (int i = 0; i < 7; i++) 
                flags.Add(romSettingsOffset + i, FlagType.Data8Bit);
            
            for (int i = 0; i < 4; i++) 
                flags.Add(romSettingsOffset + 7 + i, FlagType.Data16Bit);
            
            for (int i = 0; i < 0x20; i++) 
                flags.Add(romSettingsOffset + 11 + i, FlagType.Pointer16Bit);

            if (romBytes[romSettingsOffset - 1] == 0)
            {
                flags.Remove(romSettingsOffset - 1);
                flags.Add(romSettingsOffset - 1, FlagType.Data8Bit);
                for (int i = 0; i < 0x10; i++) 
                    flags.Add(romSettingsOffset - 0x25 + i, FlagType.Data8Bit);
            }
            else if (romBytes[romSettingsOffset + 5] == 0x33)
            {
                for (int i = 0; i < 6; i++) 
                    flags.Add(romSettingsOffset - 0x25 + i, FlagType.Text);

                for (int i = 0; i < 10; i++) 
                    flags.Add(romSettingsOffset - 0x1F + i, FlagType.Data8Bit);
            }
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

        public static LogCreatorOutput.OutputResult GetSampleAssemblyOutput(LogWriterSettings sampleSettings)
        {
            var sampleRomData = SampleRomData.SampleData;

            sampleSettings.Structure = LogWriterSettings.FormatStructure.SingleFile;
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
        
        public static ByteSourceMapping CreateRomMappingFromRomByteSource(ByteSource romByteSource, RomMapMode romMapMode, RomSpeed romSpeed)
        {
            return new()
            {
                ByteSource = romByteSource,
                RegionMapping = new RegionMappingSnesRom
                {
                    RomSpeed = romSpeed,
                    RomMapMode = romMapMode,
                }
            };
        }

        public static ByteSourceMapping CreateRomMappingFromRomRawBytes(
            IReadOnlyCollection<byte> actualRomBytes, RomMapMode romMapMode, RomSpeed romSpeed)
        {
            return CreateRomMappingFromRomByteSource(new ByteSource(actualRomBytes) { Name = "Snes ROM" }, romMapMode, romSpeed);
        }
    }
}

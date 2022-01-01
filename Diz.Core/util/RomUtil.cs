﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using Diz.Core.export;
using Diz.Core.Interfaces;
using Diz.Core.model;
using JetBrains.Annotations;

namespace Diz.Core.util
{
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

        public static int GetBankFromSnesAddress(int snesAddress)
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
            var index = ConvertSnesToPcRaw(address, mode, size);
            return index < 0 ? -1 : index;
        }

        private static int ConvertSnesToPcRaw(int address, RomMapMode mode, int size)
        {
            int GetUnmirroredOffset(int offset) => UnmirroredOffset(offset, size);

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

                    return GetUnmirroredOffset(((address & 0x7F0000) >> 1) | (address & 0x7FFF));
                }
                case RomMapMode.HiRom:
                {
                    return GetUnmirroredOffset(address & 0x3FFFFF);
                }
                case RomMapMode.SuperMmc:
                {
                    return GetUnmirroredOffset(address & 0x3FFFFF); // todo, treated as hirom atm
                }
                case RomMapMode.Sa1Rom:
                case RomMapMode.ExSa1Rom:
                {
                    // BW-RAM is N/A to PC addressing
                    if (address >= 0x400000 && address <= 0x7FFFFF) return -1;

                    if (address >= 0xC00000)
                        return mode == RomMapMode.ExSa1Rom
                            ? GetUnmirroredOffset(address & 0x7FFFFF)
                            : GetUnmirroredOffset(address & 0x3FFFFF);

                    if (address >= 0x800000) address -= 0x400000;

                    // SRAM is N/A to PC addressing
                    if (((address & 0x8000) == 0)) return -1;

                    return GetUnmirroredOffset(((address & 0x7F0000) >> 1) | (address & 0x7FFF));
                }
                case RomMapMode.SuperFx:
                {
                    // BW-RAM is N/A to PC addressing
                    if (address >= 0x600000 && address <= 0x7FFFFF)
                        return -1;

                    if (address < 0x400000)
                        return GetUnmirroredOffset(((address & 0x7F0000) >> 1) | (address & 0x7FFF));

                    if (address < 0x600000)
                        return GetUnmirroredOffset(address & 0x3FFFFF);

                    if (address < 0xC00000)
                        return 0x200000 + GetUnmirroredOffset(((address & 0x7F0000) >> 1) | (address & 0x7FFF));

                    return 0x400000 + GetUnmirroredOffset(address & 0x3FFFFF);
                }
                case RomMapMode.ExHiRom:
                {
                    return GetUnmirroredOffset(((~address & 0x800000) >> 1) | (address & 0x3FFFFF));
                }
                case RomMapMode.ExLoRom:
                {
                    // SRAM is N/A to PC addressing
                    if (((address & 0x700000) == 0x700000) && ((address & 0x8000) == 0))
                        return -1;

                    return GetUnmirroredOffset((((address ^ 0x800000) & 0xFF0000) >> 1) | (address & 0x7FFF));
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

            if ((romBytes[LoromSettingOffset] & 0xEF) == 0x23)
                return romBytes.Count > 0x400000 ? RomMapMode.ExSa1Rom : RomMapMode.Sa1Rom;

            if ((romBytes[LoromSettingOffset] & 0xEC) == 0x20)
                return (romBytes[LoromSettingOffset + 1] & 0xF0) == 0x10 ? RomMapMode.SuperFx : RomMapMode.LoRom;

            if (romBytes.Count >= 0x10000 && (romBytes[HiromSettingOffset] & 0xEF) == 0x21)
                return RomMapMode.HiRom;

            if (romBytes.Count >= 0x10000 && (romBytes[HiromSettingOffset] & 0xE7) == 0x22)
                return RomMapMode.SuperMmc;

            if (romBytes.Count >= 0x410000 && (romBytes[ExhiromSettingOffset] & 0xEF) == 0x25)
                return RomMapMode.ExHiRom;

            // detection failed. take our best guess.....
            detectedValidRomMapType = false;
            return romBytes.Count > 0x40000 ? RomMapMode.ExLoRom : RomMapMode.LoRom;
        }

        public static int GetRomSettingOffset(RomMapMode mode)
        {
            return mode switch
            {
                RomMapMode.LoRom => LoromSettingOffset,
                RomMapMode.HiRom => HiromSettingOffset,
                RomMapMode.ExHiRom => ExhiromSettingOffset,
                RomMapMode.ExLoRom => ExloromSettingOffset,
                _ => LoromSettingOffset
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

        public static byte[] ReadRomFileBytes(string filename)
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
        
        public static Dictionary<int, FlagType> GenerateHeaderFlags(int romSettingsOffset, byte[] romBytes)
        {
            var flags = new Dictionary<int, FlagType>();
            
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

            return flags;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="vectorNames"></param>
        /// <param name="romSettingsOffset"></param>
        /// <param name="romBytes"></param>
        /// <param name="mode"></param>
        /// <returns>A list of ROM offsets [NOT snes addresses] to add these labels to</returns>
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
                Debug.Assert(table is >= 0 and < tableCount);
                Debug.Assert(entry is >= 0 and < entryCount);
                // table = 0,1              // which table of Native vs Emulation
                // entry = 0,1,2,3,4,5      // which offset
                //
                // 16*i = 16,32,

                var index = baseOffset + (16 * table) + (2 * entry);
                var offset = romBytes[index] + (romBytes[index + 1] << 8);
                var pc = ConvertSnesToPc(offset, mode, romBytes.Count);
                if (pc >= 0 && pc < romBytes.Count && !labels.ContainsKey(offset))
                    labels.Add(offset, new Label { Name = vectorEntry.Key });

                if (++entry < entryCount)
                    continue;

                entry = 0;
                if (++table >= tableCount)
                    break;
            }

            return labels;
        }

        public const int LengthOfTitleName = 0x15;

        #if DIZ_3_BRANCH
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
            var data = actualRomBytes.Select(b => new ByteEntry() {Byte = b}).ToList();

            var romByteSource = new ByteSource
            {
                Bytes = new StorageList<ByteEntry>(data),
                Name = "Snes ROM"
            };
            
            return CreateRomMappingFromRomByteSource(romByteSource, romMapMode, romSpeed);
        }

        public static ByteSource CreateSnesAddressSpace()
        {
            const int snesAddressableBytes = 0x1000000;
            return new ByteSource
            {
                Bytes = new StorageSparse<ByteEntry>(snesAddressableBytes),
                Name = "SNES Main Cpu BUS",
            };
        }
        #endif
        
        public static bool IsLocationPoint(this IInOutPointGettable data, int pointer, InOutPoint mustHaveFlag) =>
            (data.GetInOutPoint(pointer) & mustHaveFlag) != 0;

        public static bool IsLocationAnEndPoint(this IInOutPointGettable data, int pointer) => 
            IsLocationPoint(data, pointer, InOutPoint.EndPoint);
        
        public static bool IsLocationAReadPoint(this IInOutPointGettable data, int pointer) => 
            IsLocationPoint(data, pointer, InOutPoint.ReadPoint);
    }
}

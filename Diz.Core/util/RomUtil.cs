using System.Collections.Generic;
using System.IO;
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

        public static RomSpeed GetRomSpeed(RomMapMode mode, IReadOnlyList<byte> romBytes) =>
            GetRomSpeed(GetRomSettingOffset(mode), romBytes);

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
        public static string GetCartridgeTitleFromRom(IReadOnlyList<byte> allRomBytes, int romSettingOffset) => 
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
        public static string GetCartridgeTitleFromBuffer(IReadOnlyList<byte> buffer, int index = 0) => 
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
            // this is not bulletproof.
            // some of these detections may be false positives if you have bad luck.
            // for instance, if your rom is really HiRom, but, romBytes[LoromSettingOffset] & 0xEC) == 0x20, 
            // then it will incorrect detect as lorom.
            //
            // best to add some other scoring capabilities in here, like, check if the detected settings give you
            // the offset of a cartridge title that's all ascii (instead of a bunch of garbage).
            //
            // maybe steal the detection code from other emulators and paste it in here.
            
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

            // all detection failed. let's pick a reasonable default, this is now jut a guess
            detectedValidRomMapType = false;
            return romBytes.Count > 0x40000 ? RomMapMode.ExLoRom : RomMapMode.LoRom;
        }

        public static bool DetectRomMapModeBustedGames(IReadOnlyList<byte> romBytes, out RomMapMode detectedRomMapMode, out RomSpeed detectedRomSpeed)
        {
            detectedRomMapMode = RomMapMode.LoRom; // default
            detectedRomSpeed = RomSpeed.Unknown;
            
            // ReSharper disable once InvertIf
            if (romBytes.Count >= 0x7FFF)
            {
                var gameNameFromRomBytes = GetCartridgeTitleFromRom(romBytes, LoromSettingOffset);
                
                // Contra3 overflowed it's buffer and uses 0x16 chars (1 over the max limit of 0x15).
                // the final character "S" in "CONTRA3 THE ALIEN WARS" is incorrectly reported as the map ("S" = 0x53).
                // this is invalid and confuses everything. it SHOULD be 0x20 for LoRom + SlowRom, but, the official 
                // gamedata has a broken header. wild.
                // ReSharper disable once InvertIf
                if (gameNameFromRomBytes == "CONTRA3 THE ALIEN WAR" && romBytes[LoromSettingOffset] == 0x53)
                {
                    detectedRomMapMode = RomMapMode.LoRom;
                    detectedRomSpeed = RomSpeed.SlowRom;    // YES, SlowRom, not fastrom. matches japanese version. fastrom bit being set in 0x53 is a LIE
                    return true;
                }
            }

            return false;
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

        /// <summary>
        /// Read a ROM file from disk. discard the SMC header of 0x200 bytes if it exists.
        /// </summary>
        /// <param name="filename">Rom filename to read</param>
        /// <returns>Raw bytes</returns>
        /// <exception cref="InvalidDataException"></exception>
        [NotNull]
        public static byte[] ReadRomFileBytes(string filename) => 
            RemoveSmcHeader(File.ReadAllBytes(filename));

        /// <summary>
        /// Take all ROM file bytes from disk, remove SMC header if present
        /// </summary>
        /// <param name="allFileBytes"></param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException"></exception>
        private static byte[] RemoveSmcHeader(byte[] allFileBytes)
        {
            var rom = new byte[allFileBytes.Length & 0x7FFFFC00];

            if ((allFileBytes.Length & 0x3FF) == 0x200)
                // skip and dont include the SMC header
                for (var i = 0; i < rom.Length; i++)
                    rom[i] = allFileBytes[i + 0x200];
            else if ((allFileBytes.Length & 0x3FF) != 0)
                throw new InvalidDataException("This ROM has an unusual size. It can't be opened.");
            else
                rom = allFileBytes;

            if (rom.Length < 0x8000)
                throw new InvalidDataException("This ROM is too small. It can't be opened.");

            return rom;
        }

        public static Dictionary<int, FlagType> GenerateHeaderFlags(int romSettingsOffset, IReadOnlyList<byte> romBytes)
        {
            var flags = new Dictionary<int, FlagType>();

            if (romSettingsOffset == -1)
                return flags;
            
            for (var i = 0; i < LengthOfTitleName; i++)
                flags.Add(romSettingsOffset - LengthOfTitleName + i, FlagType.Text);
            
            for (var i = 0; i < 7; i++) 
                flags.Add(romSettingsOffset + i, FlagType.Data8Bit);
            
            for (var i = 0; i < 4; i++) 
                flags.Add(romSettingsOffset + 7 + i, FlagType.Data16Bit);
            
            for (var i = 0; i < 0x20; i++) 
                flags.Add(romSettingsOffset + 11 + i, FlagType.Pointer16Bit);

            if (romBytes[romSettingsOffset - 1] == 0)
            {
                flags.Remove(romSettingsOffset - 1);
                flags.Add(romSettingsOffset - 1, FlagType.Data8Bit);
                for (var i = 0; i < 0x10; i++) 
                    flags.Add(romSettingsOffset - 0x25 + i, FlagType.Data8Bit);
            }
            else if (romBytes[romSettingsOffset + 5] == 0x33)
            {
                for (var i = 0; i < 6; i++) 
                    flags.Add(romSettingsOffset - 0x25 + i, FlagType.Text);

                for (var i = 0; i < 10; i++) 
                    flags.Add(romSettingsOffset - 0x1F + i, FlagType.Data8Bit);
            }

            return flags;
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

        // Take a WRAM address (from 0x0 to 0x1FFFF) and convert it to the equivalent
        // main SNES address between $7E0000 through $7FFFFF
        public static int GetSnesAddressFromWramAddress(int wramAddress)
        {
            if (wramAddress is < 0 or > 0x1FFFF)
                return -1;
            
            return wramAddress + 0x7E0000;
        }

        // This takes a SNES address and returns the offset into WRAM address space, if it exists. It deals with mirroring
        // valid return ranges are 0 through 0x1FFFFF (the offset into WRAM address space, NO LONGER in SNES address space)
        // return -1 if it doesn't map to any offset in WRAM address space
        // NOTE: this is the offset in WRAM, and not it's mirrored value.
        // i.e. if you give it $0013, this function will return $0013, and **NOT** $7E0013
        public static int GetWramAddressFromSnesAddress(int snesAddress)
        {
            if (snesAddress == -1)
                return -1;
            
            // option 1:
            // Primary WRAM range is mapped directly into SNES address space: 0x7E0000 - 0x7FFFFF
            // this directly maps 1:1 into WRAM address space
            if (snesAddress is >= 0x7E0000 and <= 0x7FFFFF)
            {
                return snesAddress - 0x7E0000;
            }
            
            // option 2:
            // WRAM is mapped into certain ranges in banks $00-$3F and $80-$BF
            var bank = (snesAddress >> 16) & 0xFF; // Extract the high byte (bank number)

            // Check if the bank is within WRAM-mirroring ranges: $00-$3F or $80-$BF
            var bankContainsWramMirror = bank is (>= 0x00 and <= 0x3F) or (>= 0x80 and <= 0xBF);
            if (!bankContainsWramMirror)
            {
                return -1;
            }
            
            // if it's in a bank with a WRAM mirror, is the 1st 16bits of address in range for WRAM?
            var addrNoBank = snesAddress & 0x00FFFF;
            if (addrNoBank > 0x1FFF)
                return -1; // out of range
	
            return addrNoBank; // in range!
        }
        
        public static bool AreLabelsSameMirror(int snesAddress, int labelAddress)
        {
            if (snesAddress == -1 || labelAddress == -1)
                return false;

            // early out shortcut 
            if ((snesAddress & 0xFFFF) != (labelAddress & 0xFFFF))
                return false;
            
            // this function is a crappy and probably error-prone way to do this. gotta start somewhere.
            // it would be better to do this by mapping out the memory regions than trying to go backwards
            // from any arbitrary SNES address back to the mapped region. still, we'll give it a shot.
            // we MOST care about things affecting labels that humans care about: WRAM mirrors and SNES registers
            
            // check WRAM mirroring
            if (AreSnesAddressesSameMirroredWramAddresses(snesAddress, labelAddress))
                return true;

            // check other IO mirroring (overlaps with above for LowRAM too, but that's OK)
            if (AreSnesAddressesSameMirroredIoRegion(snesAddress, labelAddress)) 
                return true;

            return false;
        }
        
        // if a SNES addres is mapped to WRAM, ensure the address is in the primary mapped region.
        // if not, or if it's already normalized to that region, just return the original address
        // example: $0000FE will return $7E00FE
        public static int NormalizeSnesWramAddress(int snesAddress)
        {
            var normalizedSnesAddress = GetSnesAddressFromWramAddress(GetWramAddressFromSnesAddress(snesAddress));
            return normalizedSnesAddress != -1 ? normalizedSnesAddress : snesAddress;
        }

        public static bool AreSnesAddressesSameMirroredIoRegion(int snesAddress1, int snesAddress2)
        {
            var reducedSnesAddr1 = GetUnmirroredIoRegionFromBank(snesAddress1);
            var reducedSnesAddr2 = GetUnmirroredIoRegionFromBank(snesAddress2);
            
            return reducedSnesAddr1 != -1 && reducedSnesAddr2 == reducedSnesAddr1;
        }

        public static int GetUnmirroredIoRegionFromBank(int snesAddress)
        {
            if (snesAddress == -1)
                return -1;
            
            // Mirrored WRAM range in banks $00-$3F and $80-$BF
            var bank = (snesAddress >> 16) & 0xFF; // Extract the high byte (bank number)

            // Check if the bank is within WRAM-mirroring ranges: $00-$3F or $80-$BF
            var bankContainsMirror = bank is (>= 0x00 and <= 0x3F) or (>= 0x80 and <= 0xBF);
            if (!bankContainsMirror) 
                return -1;
            
            // are we in the mirrored region?
            var low16Addr = snesAddress & 0xFFFF;
            if (low16Addr is < 0x0000 or > 0x7FFF) 
                return -1;

            return low16Addr;
        }

            
        public static bool AreSnesAddressesSameMirroredWramAddresses(int snesAddress1, int snesAddress2)
        {
            var wramAddress1 = GetWramAddressFromSnesAddress(snesAddress1);
            var wramAddress2 = GetWramAddressFromSnesAddress(snesAddress2);
        
            return wramAddress1 != -1 && wramAddress2 == wramAddress1;
        }
        
        // detect if this is a +/- label.  like "+", "-", or "++", "--" etc.
        public static bool IsValidPlusMinusLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
                return false;
        
            var firstChar = label[0];
            if (firstChar != '+' && firstChar != '-')
                return false;
        
            for (var i = 1; i < label.Length; i++)
            {
                if (label[i] != firstChar)
                    return false;
            }
    
            return true;
        }
    }
}
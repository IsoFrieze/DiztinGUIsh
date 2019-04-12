using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiztinGUIsh
{
    public static class Util
    {
        public static int GetEffectiveAddress(int offset)
        {
            return 0;
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
    }
}

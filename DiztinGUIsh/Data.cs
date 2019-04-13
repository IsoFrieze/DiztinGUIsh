using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiztinGUIsh
{
    public static class Data
    {
        public enum FlagType : byte
        {
            Unreached = 0x00,
            Opcode = 0x10,
            Operand = 0x11,
            Data8Bit = 0x20,
            Graphics = 0x21,
            Music = 0x22,
            Empty = 0x23,
            Data16Bit = 0x30,
            Pointer16Bit = 0x31,
            Data24Bit = 0x40,
            Pointer24Bit = 0x41,
            Data32Bit = 0x50,
            Pointer32Bit = 0x51,
            Text = 0x60
        }

        public enum Architechture : byte
        {
            CPU65C816 = 0x00,
            APUSPC700 = 0x01,
            GPUSuperFX = 0x02
        }

        [Flags]
        public enum InOutPoint : byte
        {
            InPoint = 0x01,
            OutPoint = 0x02,
            EndPoint = 0x04,
            ReadPoint = 0x08
        }

        public enum ROMMapMode : byte
        {
            LoROM, HiROM, ExHiROM
        }

        public enum ROMSpeed : byte
        {
            SlowROM, FastROM, Unknown
        }

        public const int
            LOROM_SETTING_OFFSET = 0x7FD5,
            HIROM_SETTING_OFFSET = 0xFFD5,
            EXHIROM_SETTING_OFFSET = 0x40FFD5;

        private static ROMMapMode rom_map_mode;
        private static ROMSpeed rom_speed;
        private static byte[] rom, data_bank, direct_page_hi, direct_page_low, x_flag, m_flag;
        private static FlagType[] flags;
        private static Architechture[] architechture;
        private static InOutPoint[] points;
        private static Dictionary<int, string> labels, comments;

        public static void Initiate(byte[] data, ROMMapMode mode, ROMSpeed speed)
        {
            rom_map_mode = mode;
            rom_speed = speed;
            rom = data;
            int size = rom.Length;
            data_bank = new byte[size];
            direct_page_hi = new byte[size];
            direct_page_low = new byte[size];
            x_flag = new byte[size];
            m_flag = new byte[size];
            flags = new FlagType[size];
            architechture = new Architechture[size];
            points = new InOutPoint[size];
            labels = new Dictionary<int, string>();
            comments = new Dictionary<int, string>();
        }

        public static ROMMapMode GetROMMapMode()
        {
            return rom_map_mode;
        }

        public static ROMSpeed GetROMSpeed()
        {
            return rom_speed;
        }

        public static int GetROMByte(int i)
        {
            return 0xFF & rom[i];
        }

        public static int GetROMSize()
        {
            return rom.Length;
        }

        public static FlagType GetFlag(int i)
        {
            return flags[i];
        }

        public static void SetFlag(int i, FlagType flag)
        {
            flags[i] = flag;
        }

        public static Architechture GetArchitechture(int i)
        {
            return architechture[i];
        }

        public static void SetArchitechture(int i, Architechture arch)
        {
            architechture[i] = arch;
        }

        public static InOutPoint GetInOutPoint(int i)
        {
            return points[i];
        }

        public static void SetInOutPoint(int i, InOutPoint point)
        {
            points[i] |= point;
        }

        public static void FlipInOutPoint(int i, InOutPoint point)
        {
            points[i] ^= point;
        }

        public static int GetDataBank(int i)
        {
            return 0xFF & data_bank[i];
        }

        public static void SetDataBank(int i, int dbank)
        {
            data_bank[i] = (byte)dbank;
        }

        public static int GetDirectPage(int i)
        {
            return ((0xFF & direct_page_hi[i]) << 8) | (0xFF & direct_page_low[i]);
        }

        public static void SetDirectPage(int i, int dpage)
        {
            direct_page_hi[i] = (byte)(dpage >> 8);
            direct_page_low[i] = (byte)dpage;
        }

        public static bool GetXFlag(int i)
        {
            return x_flag[i] != 0;
        }

        public static void SetXFlag(int i, bool x)
        {
            x_flag[i] = (byte)(x ? 1 : 0);
        }

        public static bool GetMFlag(int i)
        {
            return m_flag[i] != 0;
        }

        public static void SetMFlag(int i, bool m)
        {
            m_flag[i] = (byte)(m ? 1 : 0);
        }

        public static string GetLabel(int i)
        {
            if (labels.TryGetValue(i, out string val)) return val;
            return "";
        }

        public static void AddLabel(int i, string v)
        {
            if (labels.ContainsKey(i)) labels.Remove(i);
            labels.Add(i, v);
        }

        public static Dictionary<int, string> GetAllLabels()
        {
            return labels;
        }

        public static string GetComment(int i)
        {
            if (comments.TryGetValue(i, out string val)) return val;
            return "";
        }

        public static void AddComment(int i, string v)
        {
            if (comments.ContainsKey(i)) comments.Remove(i);
            comments.Add(i, v);
        }

        public static Dictionary<int, string> GetAllComments()
        {
            return comments;
        }
    }
}

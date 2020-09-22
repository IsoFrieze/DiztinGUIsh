using DiztinGUIsh.window;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
            LoROM, HiROM, ExHiROM, SA1ROM, ExSA1ROM, SuperFX, SuperMMC, ExLoROM
        }

        public enum ROMSpeed : byte
        {
            SlowROM, FastROM, Unknown
        }

        public const int
            LOROM_SETTING_OFFSET = 0x7FD5,
            HIROM_SETTING_OFFSET = 0xFFD5,
            EXHIROM_SETTING_OFFSET = 0x40FFD5,
            EXLOROM_SETTING_OFFSET = 0x407FD5;

        private static ROMMapMode rom_map_mode;
        private static ROMSpeed rom_speed;
        private static List<ROMByte> table;
        private static Dictionary<int, string> comment;

        public class AliasInfo
        {
            public string name = "";        // name of the label
            public string comment = "";     // user-generated text, comment only

            public void CleanUp()
            {
                if (comment == null) comment = "";
                if (name == null) name = "";
            }
        }
        private static Dictionary<int, AliasInfo> alias;

        public static void Initiate(byte[] data, ROMMapMode mode, ROMSpeed speed)
        {
            rom_map_mode = mode;
            rom_speed = speed;
            int size = data.Length;
            alias = new Dictionary<int, AliasInfo>();
            comment = new Dictionary<int, string>();
            table = new List<ROMByte>();
            for (int i = 0; i < size; i++)
            {
                ROMByte r = new ROMByte
                {
                    Rom = data[i],
                    DataBank = 0,
                    DirectPage = 0,
                    XFlag = false,
                    MFlag = false,
                    TypeFlag = FlagType.Unreached,
                    Arch = Architechture.CPU65C816,
                    Point = 0
                };
                table.Add(r);
            }
        }

        public static void Restore(List<ROMByte> l = null, ROMMapMode m = ROMMapMode.LoROM, ROMSpeed s = ROMSpeed.Unknown, Dictionary<int, AliasInfo> a = null, Dictionary<int, string> c = null)
        {
            table = l ?? table;
            rom_map_mode = s == ROMSpeed.Unknown ? rom_map_mode : m;
            rom_speed = s == ROMSpeed.Unknown ? rom_speed : s;
            alias = a ?? alias;
            comment = c ?? comment;
        }

        public static ROMMapMode GetROMMapMode()
        {
            return rom_map_mode;
        }

        public static ROMSpeed GetROMSpeed()
        {
            return rom_speed;
        }

        public static List<ROMByte> GetTable()
        {
            return table;
        }

        public static int GetROMByte(int i)
        {
            return table[i].Rom;
        }

        public static int GetROMSize()
        {
            return table == null ? 0 : table.Count;
        }

        public static FlagType GetFlag(int i)
        {
            return table[i].TypeFlag;
        }

        public static void SetFlag(int i, FlagType flag)
        {
            table[i].TypeFlag = flag;
        }

        public static Architechture GetArchitechture(int i)
        {
            return table[i].Arch;
        }

        public static void SetArchitechture(int i, Architechture arch)
        {
            table[i].Arch = arch;
        }

        public static InOutPoint GetInOutPoint(int i)
        {
            return table[i].Point;
        }

        public static void SetInOutPoint(int i, InOutPoint point)
        {
            table[i].Point |= point;
        }

        public static void ClearInOutPoint(int i)
        {
            table[i].Point = 0;
        }

        public static int GetDataBank(int i)
        {
            return table[i].DataBank;
        }

        public static void SetDataBank(int i, int dbank)
        {
            table[i].DataBank = (byte)dbank;
        }

        public static int GetDirectPage(int i)
        {
            return table[i].DirectPage;
        }

        public static void SetDirectPage(int i, int dpage)
        {
            table[i].DirectPage = 0xFFFF & dpage;
        }

        public static bool GetXFlag(int i)
        {
            return table[i].XFlag;
        }

        public static void SetXFlag(int i, bool x)
        {
            table[i].XFlag = x;
        }

        public static bool GetMFlag(int i)
        {
            return table[i].MFlag;
        }

        public static void SetMFlag(int i, bool m)
        {
            table[i].MFlag = m;
        }

        public static int GetMXFlags(int i)
        {
            return (table[i].MFlag ? 0x20 : 0) | (table[i].XFlag ? 0x10 : 0);
        }

        public static void SetMXFlags(int i, int mx)
        {
            table[i].MFlag = ((mx & 0x20) != 0);
            table[i].XFlag = ((mx & 0x10) != 0);
        }

        public static string GetLabelName(int i)
        {
            if (alias.TryGetValue(i, out AliasInfo val)) 
                return val?.name ?? "";

            return "";
        }
        public static string GetLabelComment(int i)
        {
            if (alias.TryGetValue(i, out AliasInfo val)) 
                return val?.comment ?? "";

            return "";
        }

        public static void AddLabel(int i, AliasInfo v, bool overwrite)
        {
            if (v == null)
            {
                if (alias.ContainsKey(i))
                {
                    alias.Remove(i);
                    AliasList.me.RemoveRow(i);
                }
            } else {
                if (alias.ContainsKey(i) && overwrite)
                {
                    alias.Remove(i);
                    AliasList.me.RemoveRow(i);
                }
                if (!alias.ContainsKey(i))
                {
                    v.CleanUp();

                    alias.Add(i, v);
                    AliasList.me.AddRow(i, v);
                }
            }
        }

        public static Dictionary<int, AliasInfo> GetAllLabels()
        {
            return alias;
        }

        public static string GetComment(int i)
        {
            if (comment.TryGetValue(i, out string val)) return val;
            return "";
        }

        public static void AddComment(int i, string v, bool overwrite)
        {
            if (v == null)
            {
                if (comment.ContainsKey(i)) comment.Remove(i);
            } else
            {
                if (comment.ContainsKey(i) && overwrite) comment.Remove(i);
                if (!comment.ContainsKey(i)) comment.Add(i, v);
            }
        }

        public static Dictionary<int, string> GetAllComments()
        {
            return comment;
        }

        public static int GetRomSettingOffset(ROMMapMode mode)
        {
            switch (mode)
            {
                case ROMMapMode.LoROM: return LOROM_SETTING_OFFSET;
                case ROMMapMode.HiROM: return HIROM_SETTING_OFFSET;
                case ROMMapMode.ExHiROM: return EXHIROM_SETTING_OFFSET;
                case ROMMapMode.ExLoROM: return EXLOROM_SETTING_OFFSET;
            }
            return LOROM_SETTING_OFFSET;
        }
    }
}

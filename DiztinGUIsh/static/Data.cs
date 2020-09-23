using DiztinGUIsh.window;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiztinGUIsh
{
    public class TableData
    {
        protected bool Equals(TableData other)
        {
            return RomBytes.SequenceEqual(other.RomBytes);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TableData) obj);
        }

        public override int GetHashCode()
        {
            return (RomBytes != null ? RomBytes.GetHashCode() : 0);
        }

        public List<ROMByte> RomBytes { get; } = new List<ROMByte>();
        public ROMByte this[int i] {
            get => RomBytes[i];
            set => RomBytes[i] = value;
        }
    }

    public class Data
    {
        protected bool Equals(Data other)
        {
            return alias.SequenceEqual(other.alias) && RomMapMode == other.RomMapMode && rom_speed == other.rom_speed && comment.SequenceEqual(other.comment) && table.Equals(other.table);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Data) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = alias.GetHashCode();
                hashCode = (hashCode * 397) ^ (int) RomMapMode;
                hashCode = (hashCode * 397) ^ (int) rom_speed;
                hashCode = (hashCode * 397) ^ comment.GetHashCode();
                hashCode = (hashCode * 397) ^ table.GetHashCode();
                return hashCode;
            }
        }

        // singleton
        // private static readonly Lazy<Data> instance = new Lazy<Data>(() => new Data());
        // public static Data Inst => instance.Value;

        // backwards compatibility only. from here on out, everything should reference Project.Inst.Data
        public static Data Inst => Project.Inst.Data;


        // public Data() {} // should be non-public for singleton but whatev for now.
        // we shouldn't use singleton anyway, we should just pass around Data.

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

        [Flags]
        public enum BsnesPlusUsage : byte
        {
            UsageRead = 0x80,
            UsageWrite = 0x40,
            UsageExec = 0x20,
            UsageOpcode = 0x10,
            UsageFlagM = 0x02,
            UsageFlagX = 0x01,
        };

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

        // Note: order of these properties matters for the load/save process. Keep 'table' LAST
        public ROMMapMode RomMapMode { get; set; }
        public ROMSpeed rom_speed { get; set; }
        public Dictionary<int, string> comment { get; set; }
        public TableData table { get; set; }


        public class AliasInfo
        {
            protected bool Equals(AliasInfo other)
            {
                return name == other.name && comment == other.comment;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((AliasInfo) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((name != null ? name.GetHashCode() : 0) * 397) ^ (comment != null ? comment.GetHashCode() : 0);
                }
            }

            public string name = "";        // name of the label
            public string comment = "";     // user-generated text, comment only

            public void CleanUp()
            {
                if (comment == null) comment = "";
                if (name == null) name = "";
            }
        }
        public Dictionary<int, AliasInfo> alias;

        public void Initiate(byte[] data, ROMMapMode mode, ROMSpeed speed)
        {
            RomMapMode = mode;
            rom_speed = speed;
            int size = data.Length;
            alias = new Dictionary<int, AliasInfo>();
            comment = new Dictionary<int, string>();
            table = new TableData();
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
                table.RomBytes.Add(r);
            }
        }

        public void CopyRomDataIn(byte[] data)
        {
            int size = data.Length;
            Debug.Assert(table.RomBytes.Count == size);
            for (int i = 0; i < size; i++)
            {
                table[i].Rom = data[i];
            }
        }

        public void Restore(TableData l = null, ROMMapMode m = ROMMapMode.LoROM, ROMSpeed s = ROMSpeed.Unknown, Dictionary<int, AliasInfo> a = null, Dictionary<int, string> c = null)
        {
            table = l ?? table;
            RomMapMode = s == ROMSpeed.Unknown ? RomMapMode : m;
            rom_speed = s == ROMSpeed.Unknown ? rom_speed : s;
            alias = a ?? alias;
            comment = c ?? comment;
        }

        public ROMMapMode GetROMMapMode()
        {
            return RomMapMode;
        }

        public ROMSpeed GetROMSpeed()
        {
            return rom_speed;
        }

        public TableData GetTable()
        {
            return table;
        }

        public int GetROMByte(int i)
        {
            return table[i].Rom;
        }

        public int GetROMSize()
        {
            return table == null ? 0 : table.RomBytes.Count;
        }

        public FlagType GetFlag(int i)
        {
            return table[i].TypeFlag;
        }

        public void SetFlag(int i, FlagType flag)
        {
            table[i].TypeFlag = flag;
        }

        public Architechture GetArchitechture(int i)
        {
            return table[i].Arch;
        }

        public void SetArchitechture(int i, Architechture arch)
        {
            table[i].Arch = arch;
        }

        public InOutPoint GetInOutPoint(int i)
        {
            return table[i].Point;
        }

        public void SetInOutPoint(int i, InOutPoint point)
        {
            table[i].Point |= point;
        }

        public void ClearInOutPoint(int i)
        {
            table[i].Point = 0;
        }

        public int GetDataBank(int i)
        {
            return table[i].DataBank;
        }

        public void SetDataBank(int i, int dbank)
        {
            table[i].DataBank = (byte)dbank;
        }

        public int GetDirectPage(int i)
        {
            return table[i].DirectPage;
        }

        public void SetDirectPage(int i, int dpage)
        {
            table[i].DirectPage = 0xFFFF & dpage;
        }

        public bool GetXFlag(int i)
        {
            return table[i].XFlag;
        }

        public void SetXFlag(int i, bool x)
        {
            table[i].XFlag = x;
        }

        public bool GetMFlag(int i)
        {
            return table[i].MFlag;
        }

        public void SetMFlag(int i, bool m)
        {
            table[i].MFlag = m;
        }

        public int GetMXFlags(int i)
        {
            return (table[i].MFlag ? 0x20 : 0) | (table[i].XFlag ? 0x10 : 0);
        }

        public void SetMXFlags(int i, int mx)
        {
            table[i].MFlag = ((mx & 0x20) != 0);
            table[i].XFlag = ((mx & 0x10) != 0);
        }

        public string GetLabelName(int i)
        {
            if (alias.TryGetValue(i, out AliasInfo val)) 
                return val?.name ?? "";

            return "";
        }
        public string GetLabelComment(int i)
        {
            if (alias.TryGetValue(i, out AliasInfo val)) 
                return val?.comment ?? "";

            return "";
        }

        public void DeleteAllLabels()
        {
            alias.Clear();
        }

        public void AddLabel(int i, AliasInfo v, bool overwrite)
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

        public Dictionary<int, AliasInfo> GetAllLabels()
        {
            return alias;
        }

        public string GetComment(int i)
        {
            string val;
            if (comment.TryGetValue(i, out val)) return val;
            return "";
        }

        public void AddComment(int i, string v, bool overwrite)
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

        public Dictionary<int, string> GetAllComments()
        {
            return comment;
        }

        public int GetRomSettingOffset(ROMMapMode mode)
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

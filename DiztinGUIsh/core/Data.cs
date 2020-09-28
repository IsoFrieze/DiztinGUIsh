using DiztinGUIsh.window;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DiztinGUIsh
{
    public class Data
    {
        // Note: order of these properties matters for the load/save process. Keep 'RomBytes' LAST
        // TODO: should be a way in the XML serializer to control the order, remove this comment
        // when we figure it out.
        public ROMMapMode RomMapMode { get; set; }
        public ROMSpeed RomSpeed { get; set; }
        public Dictionary<int, string> Comments { get; set; }
        public Dictionary<int, Label> Labels { get; set; }
        public RomBytes RomBytes { get; set; }

        private CPU65C816 CPU65C816 { get; set; }

        public Data()
        {
            CPU65C816 = new CPU65C816(this);
        }

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

        // TODO: move BsnesPlusUsage stuff to its own class outside of Data
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


        public void Initiate(byte[] data, ROMMapMode mode, ROMSpeed speed)
        {
            RomMapMode = mode;
            RomSpeed = speed;
            int size = data.Length;
            Labels = new Dictionary<int, Label>();
            Comments = new Dictionary<int, string>();
            RomBytes = new RomBytes();
            for (int i = 0; i < size; i++)
            {
                var r = new ROMByte
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
                RomBytes.Add(r);
            }
        }

        private byte[] GetRomBytes(int pcOffset, int count)
        {
            byte[] output = new byte[count];
            for (int i = 0; i < output.Length; i++)
                output[i] = (byte)GetROMByte(ConvertSNEStoPC(pcOffset + i));

            return output;
        }

        public string GetRomNameFromRomBytes()
        {
            return System.Text.Encoding.UTF8.GetString(GetRomBytes(0xFFC0, 21));
        }

        public int GetRomCheckSumsFromRomBytes()
        {
            return Util.ByteArrayToInteger(GetRomBytes(0xFFDC, 4));
        }

        public void CopyRomDataIn(byte[] data)
        {
            int size = data.Length;
            Debug.Assert(RomBytes.Count == size);
            for (int i = 0; i < size; i++)
            {
                RomBytes[i].Rom = data[i];
            }
        }

        public ROMMapMode GetROMMapMode()
        {
            return RomMapMode;
        }

        public ROMSpeed GetROMSpeed()
        {
            return RomSpeed;
        }

        public RomBytes GetTable()
        {
            return RomBytes;
        }

        public int GetROMByte(int i)
        {
            return RomBytes[i].Rom;
        }

        public int GetROMSize()
        {
            return RomBytes?.Count ?? 0;
        }

        public FlagType GetFlag(int i)
        {
            return RomBytes[i].TypeFlag;
        }

        public void SetFlag(int i, FlagType flag)
        {
            RomBytes[i].TypeFlag = flag;
        }

        public Architechture GetArchitechture(int i)
        {
            return RomBytes[i].Arch;
        }

        public void SetArchitechture(int i, Architechture arch)
        {
            RomBytes[i].Arch = arch;
        }

        public InOutPoint GetInOutPoint(int i)
        {
            return RomBytes[i].Point;
        }

        public void SetInOutPoint(int i, InOutPoint point)
        {
            RomBytes[i].Point |= point;
        }

        public void ClearInOutPoint(int i)
        {
            RomBytes[i].Point = 0;
        }

        public int GetDataBank(int i)
        {
            return RomBytes[i].DataBank;
        }

        public void SetDataBank(int i, int dbank)
        {
            RomBytes[i].DataBank = (byte)dbank;
        }

        public int GetDirectPage(int i)
        {
            return RomBytes[i].DirectPage;
        }

        public void SetDirectPage(int i, int dpage)
        {
            RomBytes[i].DirectPage = 0xFFFF & dpage;
        }

        public bool GetXFlag(int i)
        {
            return RomBytes[i].XFlag;
        }

        public void SetXFlag(int i, bool x)
        {
            RomBytes[i].XFlag = x;
        }

        public bool GetMFlag(int i)
        {
            return RomBytes[i].MFlag;
        }

        public void SetMFlag(int i, bool m)
        {
            RomBytes[i].MFlag = m;
        }

        public int GetMXFlags(int i)
        {
            return (RomBytes[i].MFlag ? 0x20 : 0) | (RomBytes[i].XFlag ? 0x10 : 0);
        }

        public void SetMXFlags(int i, int mx)
        {
            RomBytes[i].MFlag = ((mx & 0x20) != 0);
            RomBytes[i].XFlag = ((mx & 0x10) != 0);
        }

        public string GetLabelName(int i)
        {
            if (Labels.TryGetValue(i, out var val)) 
                return val?.name ?? "";

            return "";
        }
        public string GetLabelComment(int i)
        {
            if (Labels.TryGetValue(i, out var val)) 
                return val?.comment ?? "";

            return "";
        }

        public void DeleteAllLabels()
        {
            Labels.Clear();
        }

        public void AddLabel(int i, Label v, bool overwrite)
        {
            if (v == null)
            {
                if (Labels.ContainsKey(i))
                {
                    Labels.Remove(i);
                    // TODO: notify observers     AliasList.me.RemoveRow(i);
                }
            } else {
                if (Labels.ContainsKey(i) && overwrite)
                {
                    Labels.Remove(i);
                    // // TODO: notify observers     AliasList.me.RemoveRow(i);
                }

                if (Labels.ContainsKey(i)) 
                    return;

                v.CleanUp();

                Labels.Add(i, v);
                // // TODO: notify observers     AliasList.me.AddRow(i, v);
            }
        }

        public Dictionary<int, Label> GetAllLabels()
        {
            return Labels;
        }

        public string GetComment(int i)
        {
            return Comments.TryGetValue(i, out var val) ? val : "";
        }

        public void AddComment(int i, string v, bool overwrite)
        {
            if (v == null)
            {
                if (Comments.ContainsKey(i)) Comments.Remove(i);
            } else
            {
                if (Comments.ContainsKey(i) && overwrite) Comments.Remove(i);
                if (!Comments.ContainsKey(i)) Comments.Add(i, v);
            }
        }

        public Dictionary<int, string> GetAllComments()
        {
            return Comments;
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


        public int ConvertPCtoSNES(int offset)
        {
            return Util.ConvertPCtoSNES(offset, RomMapMode, GetROMSpeed());
        }

        public int GetROMWord(int offset)
        {
            if (offset + 1 < GetROMSize())
                return GetROMByte(offset) + (GetROMByte(offset + 1) << 8);
            return -1;
        }

        public int GetROMLong(int offset)
        {
            if (offset + 2 < GetROMSize())
                return GetROMByte(offset) + (GetROMByte(offset + 1) << 8) + (GetROMByte(offset + 2) << 16);
            return -1;
        }

        public int GetROMDoubleWord(int offset)
        {
            if (offset + 3 < GetROMSize())
                return GetROMByte(offset) + (GetROMByte(offset + 1) << 8) + (GetROMByte(offset + 2) << 16) + (GetROMByte(offset + 3) << 24);
            return -1;
        }

        public int GetIntermediateAddressOrPointer(int offset)
        {
            switch (GetFlag(offset))
            {
                case Data.FlagType.Unreached:
                case Data.FlagType.Opcode:
                    return GetIntermediateAddress(offset, true);
                case Data.FlagType.Pointer16Bit:
                    int bank = GetDataBank(offset);
                    return (bank << 16) | GetROMWord(offset);
                case Data.FlagType.Pointer24Bit:
                case Data.FlagType.Pointer32Bit:
                    return GetROMLong(offset);
            }
            return -1;
        }

        public int OpcodeByteLength(int offset)
        {
            switch (GetArchitechture(offset))
            {
                case Data.Architechture.CPU65C816: return CPU65C816.GetInstructionLength(offset);
                case Data.Architechture.APUSPC700: return 1;
                case Data.Architechture.GPUSuperFX: return 1;
            }

            return 1;
        }

        private int UnmirroredOffset(int offset)
        {
            return Util.UnmirroredOffset(offset, GetROMSize());
        }

        public string GetFormattedBytes(int offset, int step, int bytes)
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
                    case 1: res += Util.NumberToBaseString(GetROMByte(offset + i), Util.NumberBase.Hexadecimal, 2, true); break;
                    case 2: res += Util.NumberToBaseString(GetROMWord(offset + i), Util.NumberBase.Hexadecimal, 4, true); break;
                    case 3: res += Util.NumberToBaseString(GetROMLong(offset + i), Util.NumberBase.Hexadecimal, 6, true); break;
                    case 4: res += Util.NumberToBaseString(GetROMDoubleWord(offset + i), Util.NumberBase.Hexadecimal, 8, true); break;
                }
            }

            return res;
        }

        public int ConvertSNEStoPC(int address)
        {
            return Util.ConvertSNESToPC(address, RomMapMode, GetROMSize());
        }

        public string GetPointer(int offset, int bytes)
        {
            int ia = -1;
            string format = "", param = "";
            switch (bytes)
            {
                case 2:
                    ia = (GetDataBank(offset) << 16) | GetROMWord(offset);
                    format = "dw {0}";
                    param = Util.NumberToBaseString(GetROMWord(offset), Util.NumberBase.Hexadecimal, 4, true);
                    break;
                case 3:
                    ia = GetROMLong(offset);
                    format = "dl {0}";
                    param = Util.NumberToBaseString(GetROMLong(offset), Util.NumberBase.Hexadecimal, 6, true);
                    break;
                case 4:
                    ia = GetROMLong(offset);
                    format = "dl {0}" +
                             $" : db {Util.NumberToBaseString(GetROMByte(offset + 3), Util.NumberBase.Hexadecimal, 2, true)}";
                    param = Util.NumberToBaseString(GetROMLong(offset), Util.NumberBase.Hexadecimal, 6, true);
                    break;
            }

            int pc = ConvertSNEStoPC(ia);
            if (pc >= 0 && GetLabelName(ia) != "") param = GetLabelName(ia);
            return string.Format(format, param);
        }

        public string GetFormattedText(int offset, int bytes)
        {
            string text = "db \"";
            for (int i = 0; i < bytes; i++) text += (char)GetROMByte(offset + i);
            return text + "\"";
        }

        public string GetDefaultLabel(int offset)
        {
            var snes = ConvertPCtoSNES(offset);
            return string.Format("{0}_{1}", Util.TypeToLabel(GetFlag(offset)), Util.NumberToBaseString(snes, Util.NumberBase.Hexadecimal, 6));
        }

        public int Step(int offset, bool branch, bool force, int prevOffset)
        {
            switch (GetArchitechture(offset))
            {
                case Data.Architechture.CPU65C816: return CPU65C816.Step(offset, branch, force, prevOffset);
                case Data.Architechture.APUSPC700: return offset;
                case Data.Architechture.GPUSuperFX: return offset;
            }
            return offset;
        }

        public int AutoStep(int offset, bool harsh, int amount)
        {
            int newOffset = offset, prevOffset = offset - 1, nextOffset = offset;
            if (harsh)
            {
                while (newOffset < offset + amount)
                {
                    nextOffset = Step(newOffset, false, true, prevOffset);
                    prevOffset = newOffset;
                    newOffset = nextOffset;
                }
            }
            else
            {
                Stack<int> stack = new Stack<int>();
                List<int> seenBranches = new List<int>();
                bool keepGoing = true;

                while (keepGoing)
                {
                    switch (GetArchitechture(newOffset))
                    {
                        case Data.Architechture.CPU65C816:
                            if (seenBranches.Contains(newOffset))
                            {
                                keepGoing = false;
                                break;
                            }

                            int opcode = GetROMByte(newOffset);

                            nextOffset = Step(newOffset, false, false, prevOffset);
                            int jumpOffset = Step(newOffset, true, false, prevOffset);

                            if (opcode == 0x40 || opcode == 0xCB || opcode == 0xDB || opcode == 0xF8 // RTI WAI STP SED
                                || opcode == 0xFB || opcode == 0x00 || opcode == 0x02 || opcode == 0x42 // XCE BRK COP WDM
                                || opcode == 0x6C || opcode == 0x7C || opcode == 0xDC || opcode == 0xFC // JMP JMP JML JSR
                            ) keepGoing = false;

                            if (opcode == 0x4C || opcode == 0x5C || opcode == 0x80 || opcode == 0x82 // JMP JML BRA BRL
                                || opcode == 0x10 || opcode == 0x30 || opcode == 0x50 || opcode == 0x70 // BPL BMI BVC BVS
                                || opcode == 0x90 || opcode == 0xB0 || opcode == 0xD0 || opcode == 0xF0 // BCC BCS BNE BEQ
                            ) seenBranches.Add(newOffset);

                            if (opcode == 0x08) // PHP
                            {
                                stack.Push(GetMXFlags(newOffset));
                            }
                            else if (opcode == 0x28) // PLP
                            {
                                if (stack.Count == 0)
                                {
                                    keepGoing = false; break;
                                }
                                else
                                {
                                    SetMXFlags(newOffset, stack.Pop());
                                }
                            }

                            if (opcode == 0x60 || opcode == 0x6B) // RTS RTL
                            {
                                if (stack.Count == 0)
                                {
                                    keepGoing = false;
                                    break;
                                }
                                else
                                {
                                    prevOffset = newOffset;
                                    newOffset = stack.Pop();
                                }
                            }
                            else if (opcode == 0x20 || opcode == 0x22) // JSR JSL
                            {
                                stack.Push(nextOffset);
                                prevOffset = newOffset;
                                newOffset = jumpOffset;
                            }
                            else
                            {
                                prevOffset = newOffset;
                                newOffset = nextOffset;
                            }
                            break;
                        case Data.Architechture.APUSPC700:
                        case Data.Architechture.GPUSuperFX:
                            nextOffset = Step(newOffset, false, true, prevOffset);
                            prevOffset = newOffset;
                            newOffset = nextOffset;
                            break;
                    }

                    Data.FlagType flag = GetFlag(newOffset);
                    if (!(flag == Data.FlagType.Unreached || flag == Data.FlagType.Opcode || flag == Data.FlagType.Operand)) keepGoing = false;
                }
            }
            return newOffset;
        }

        public int Mark(int offset, Data.FlagType type, int count)
        {
            int i, size = GetROMSize();
            for (i = 0; i < count && offset + i < size; i++) SetFlag(offset + i, type);
            return offset + i < size ? offset + i : size - 1;
        }

        public int MarkDataBank(int offset, int db, int count)
        {
            int i, size = GetROMSize();
            for (i = 0; i < count && offset + i < size; i++) SetDataBank(offset + i, db);
            return offset + i < size ? offset + i : size - 1;
        }

        public int MarkDirectPage(int offset, int dp, int count)
        {
            int i, size = GetROMSize();
            for (i = 0; i < count && offset + i < size; i++) SetDirectPage(offset + i, dp);
            return offset + i < size ? offset + i : size - 1;
        }

        public int MarkXFlag(int offset, bool x, int count)
        {
            int i, size = GetROMSize();
            for (i = 0; i < count && offset + i < size; i++) SetXFlag(offset + i, x);
            return offset + i < size ? offset + i : size - 1;
        }

        public int MarkMFlag(int offset, bool m, int count)
        {
            int i, size = GetROMSize();
            for (i = 0; i < count && offset + i < size; i++) SetMFlag(offset + i, m);
            return offset + i < size ? offset + i : size - 1;
        }

        public int MarkArchitechture(int offset, Data.Architechture arch, int count)
        {
            int i, size = GetROMSize();
            for (i = 0; i < count && offset + i < size; i++) SetArchitechture(offset + i, arch);
            return offset + i < size ? offset + i : size - 1;
        }

        public int GetInstructionLength(int offset)
        {
            switch (GetArchitechture(offset))
            {
                case Data.Architechture.CPU65C816: return CPU65C816.GetInstructionLength(offset);
                case Data.Architechture.APUSPC700: return 1;
                case Data.Architechture.GPUSuperFX: return 1;
            }
            return 1;
        }

        public int FixMisalignedFlags()
        {
            int count = 0, size = GetROMSize();

            for (int i = 0; i < size; i++)
            {
                Data.FlagType flag = GetFlag(i);

                if (flag == Data.FlagType.Opcode)
                {
                    int len = GetInstructionLength(i);
                    for (int j = 1; j < len && i + j < size; j++)
                    {
                        if (GetFlag(i + j) != Data.FlagType.Operand)
                        {
                            SetFlag(i + j, Data.FlagType.Operand);
                            count++;
                        }
                    }
                    i += len - 1;
                }
                else if (flag == Data.FlagType.Operand)
                {
                    SetFlag(i, Data.FlagType.Opcode);
                    count++;
                    i--;
                }
                else if (Util.TypeStepSize(flag) > 1)
                {
                    int step = Util.TypeStepSize(flag);
                    for (int j = 1; j < step; j++)
                    {
                        if (GetFlag(i + j) != flag)
                        {
                            SetFlag(i + j, flag);
                            count++;
                        }
                    }
                    i += step - 1;
                }
            }

            return count;
        }

        public void RescanInOutPoints()
        {
            for (int i = 0; i < GetROMSize(); i++) ClearInOutPoint(i);

            for (int i = 0; i < GetROMSize(); i++)
            {
                if (GetFlag(i) == Data.FlagType.Opcode)
                {
                    switch (GetArchitechture(i))
                    {
                        case Data.Architechture.CPU65C816: CPU65C816.MarkInOutPoints(i); break;
                        case Data.Architechture.APUSPC700: break;
                        case Data.Architechture.GPUSuperFX: break;
                    }
                }
            }
        }

        public int ImportUsageMap(byte[] usageMap)
        {
            int size = GetROMSize();
            int modified = 0;
            int prevFlags = 0;

            for (int map = 0; map <= 0xFFFFFF; map++)
            {
                var i = ConvertSNEStoPC(map);

                if (i == -1 || i >= size)
                {
                    // branch predictor may optimize this
                    continue;
                }

                var flags = (Data.BsnesPlusUsage)usageMap[map];

                if (flags == 0)
                {
                    // no information available
                    continue;
                }

                if (GetFlag(i) != Data.FlagType.Unreached)
                {
                    // skip if there is something already set..
                    continue;
                }

                // opcode: 0x30, operand: 0x20
                if (flags.HasFlag(Data.BsnesPlusUsage.UsageExec))
                {
                    SetFlag(i, Data.FlagType.Operand);

                    if (flags.HasFlag(Data.BsnesPlusUsage.UsageOpcode))
                    {
                        prevFlags = ((int)flags & 3) << 4;
                        SetFlag(i, Data.FlagType.Opcode);
                    }

                    SetMXFlags(i, prevFlags);
                    modified++;
                }
                else if (flags.HasFlag(Data.BsnesPlusUsage.UsageRead))
                {
                    SetFlag(i, Data.FlagType.Data8Bit);
                    modified++;
                }
            }

            return modified;
        }


        public int GetIntermediateAddress(int offset, bool resolve = false)
        {
            // FIX ME: log and generation of dp opcodes. search references
            switch (GetArchitechture(offset))
            {
                case Data.Architechture.CPU65C816: return CPU65C816.GetIntermediateAddress(offset, resolve);
                case Data.Architechture.APUSPC700: return -1;
                case Data.Architechture.GPUSuperFX: return -1;
            }
            return -1;
        }

        public string GetInstruction(int offset)
        {
            switch (GetArchitechture(offset))
            {
                case Data.Architechture.CPU65C816: return CPU65C816.GetInstruction(offset);
                case Data.Architechture.APUSPC700: return "";
                case Data.Architechture.GPUSuperFX: return "";
            }
            return "";
        }

        // this class exists for performance optimization ONLY.
        // class representing offsets into a trace log
        // we calculate it once from sample data and hang onto it
        private class CachedTraceLineIndex
        {
            // NOTE: newer versions of BSNES use different text for flags. check for completeness.
            private string sample =
                "028cde rep #$30               A:0004 X:0000 Y:0004 S:1fdd D:0000 DB:02 nvmxdiZC V:133 H: 654 F:36";

            // index of the start of the info
            public readonly int
                addr,
                D, DB,
                flags,
                f_N, f_V, f_M, f_X, f_D, f_I, f_Z, f_C;

            public CachedTraceLineIndex()
            {
                int SkipToken(string token)
                {
                    return sample.IndexOf(token) + token.Length;
                }

                addr = 0;
                D = SkipToken("D:");
                DB = SkipToken("DB:");
                flags = DB + 3;

                // flags: nvmxdizc
                f_N = flags + 0;
                f_V = flags + 1;
                f_M = flags + 2;
                f_X = flags + 3;
                f_D = flags + 4;
                f_I = flags + 5;
                f_Z = flags + 6;
                f_C = flags + 7;
            }
        }

        private static readonly CachedTraceLineIndex CachedIdx = new CachedTraceLineIndex();

        public int ImportTraceLogLine(string line)
        {
            // caution: very performance-sensitive function, please take care when making modifications
            // string.IndexOf() is super-slow too.
            // Input lines must follow this strict format and be this exact formatting and column indices.
            // 028cde rep #$30               A:0004 X:0000 Y:0004 S:1fdd D:0000 DB:02 nvmxdiZC V:133 H: 654 F:36

            int GetHexValueAt(int startIndex, int length)
            {
                return Convert.ToInt32(line.Substring(startIndex, length), 16);
            }

            if (line.Length < 80)
                return 0;

            int snesAddress = GetHexValueAt(0, 6);
            int pc = ConvertSNEStoPC(snesAddress);
            if (pc == -1)
                return 0;

            // TODO: error treatment

            int directPage = GetHexValueAt(CachedIdx.D, 4);
            int dataBank = GetHexValueAt(CachedIdx.DB, 2);

            // 'X' = unchecked in bsnesplus debugger UI = (8bit), 'x' or '.' = checked (16bit)
            bool xflag_set = line[CachedIdx.f_X] == 'X';

            // 'M' = unchecked in bsnesplus debugger UI = (8bit), 'm' or '.' = checked (16bit)
            bool mflag_set = line[CachedIdx.f_M] == 'M';

            SetFlag(pc, Data.FlagType.Opcode);

            int modified = 0;
            int size = GetROMSize();
            do
            {
                SetDataBank(pc, dataBank);
                SetDirectPage(pc, directPage);
                SetXFlag(pc, xflag_set);
                SetMFlag(pc, mflag_set);

                pc++;
                modified++;
            } while (pc < size && GetFlag(pc) == Data.FlagType.Operand);

            return modified;
        }

        public void ImportBizHawkCDL(BizHawkCdl cdl)
        {
            if (!cdl.TryGetValue("CARTROM", out var cdlRomFlags))
            {
                throw new InvalidDataException("The CDL file does not contain CARTROM block.");
            }

            var size = Math.Min(cdlRomFlags.Count, GetROMSize());
            bool m = false;
            bool x = false;
            for (var offset = 0; offset < size; offset++)
            {
                var cdlFlag = cdlRomFlags[offset];
                if (cdlFlag == BizHawkCdl.Flag.None)
                    continue;

                var type = Data.FlagType.Unreached;
                if ((cdlFlag & BizHawkCdl.Flag.ExecFirst) != 0)
                {
                    type = Data.FlagType.Opcode;
                    m = (cdlFlag & BizHawkCdl.Flag.CPUMFlag) != 0;
                    x = (cdlFlag & BizHawkCdl.Flag.CPUXFlag) != 0;
                }
                else if ((cdlFlag & BizHawkCdl.Flag.ExecOperand) != 0)
                    type = Data.FlagType.Operand;
                else if ((cdlFlag & BizHawkCdl.Flag.CPUData) != 0)
                    type = Data.FlagType.Data8Bit;
                else if ((cdlFlag & BizHawkCdl.Flag.DMAData) != 0)
                    type = Data.FlagType.Data8Bit;
                Mark(offset, type, 1);

                if (type == Data.FlagType.Opcode || type == Data.FlagType.Operand)
                {
                    // Operand reuses the last M and X flag values used in Opcode,
                    // since BizHawk CDL records M and X flags only in Opcode.
                    MarkMFlag(offset, m, 1);
                    MarkXFlag(offset, x, 1);
                }
            }
        }
        #region Equality
        protected bool Equals(Data other)
        {
            return Labels.SequenceEqual(other.Labels) && RomMapMode == other.RomMapMode && RomSpeed == other.RomSpeed && Comments.SequenceEqual(other.Comments) && RomBytes.Equals(other.RomBytes);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Data)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Labels.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)RomMapMode;
                hashCode = (hashCode * 397) ^ (int)RomSpeed;
                hashCode = (hashCode * 397) ^ Comments.GetHashCode();
                hashCode = (hashCode * 397) ^ RomBytes.GetHashCode();
                return hashCode;
            }
        }
        #endregion
    }
}

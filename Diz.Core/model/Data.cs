using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Diz.Core.arch;
using Diz.Core.util;

namespace Diz.Core.model
{
    public class Data
    {
        public enum FlagType : byte
        {
            Unreached = 0x00,
            Opcode = 0x10,
            Operand = 0x11,
            [Description("Data (8-bit)")] Data8Bit = 0x20,
            Graphics = 0x21,
            Music = 0x22,
            Empty = 0x23,
            [Description("Data (16-bit)")] Data16Bit = 0x30,
            [Description("Pointer (16-bit)")] Pointer16Bit = 0x31,
            [Description("Data (24-bit)")] Data24Bit = 0x40,
            [Description("Pointer (24-bit)")] Pointer24Bit = 0x41,
            [Description("Data (32-bit)")] Data32Bit = 0x50,
            [Description("Pointer (32-bit)")] Pointer32Bit = 0x51,
            Text = 0x60
        }

        public enum Architecture : byte
        {
            [Description("65C816")]
            CPU65C816 = 0x00,
            [Description("SPC700")]
            APUSPC700 = 0x01,
            [Description("SuperFX")]
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
            LoROM,

            HiROM, 

            ExHiROM, 

            [Description("SA - 1 ROM")]
            SA1ROM, 

            [Description("SA-1 ROM (FuSoYa's 8MB mapper)")]
            ExSA1ROM, 

            SuperFX,

            [Description("Super MMC")]
            SuperMMC, 

            ExLoROM
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

        // Note: order of these properties matters for the load/save process. Keep 'RomBytes' LAST
        // TODO: should be a way in the XML serializer to control the order, remove this comment
        // when we figure it out.
        public ROMMapMode RomMapMode { get; set; }
        public ROMSpeed RomSpeed { get; set; }

        // dictionaries store in SNES address format (since memory labels can't be represented as a PC address)
        public OdWrapper<int,string> Comments { get; set; } = new OdWrapper<int, string>();
        public OdWrapper<int, Label> Labels { get; set; } = new OdWrapper<int, Label>();

        // RomBytes stored as PC file offset addresses (since ROM will always be mapped to disk)
        public RomBytes RomBytes { get; set; } = new RomBytes();

        private CPU65C816 CPU65C816 { get; set; }

        public Data()
        {
            CPU65C816 = new CPU65C816(this);
        }

        public void CreateRomBytesFromRom(IEnumerable<byte> actualRomBytes)
        {
            Debug.Assert(RomBytes.Count == 0);
            RomBytes.Clear();
            foreach (var fileByte in actualRomBytes)
            {
                RomBytes.Add(new ROMByte
                {
                    Rom = fileByte,
                });
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
            return Encoding.UTF8.GetString(GetRomBytes(0xFFC0, 21));
        }

        public int GetRomCheckSumsFromRomBytes()
        {
            return ByteUtil.ByteArrayToInteger(GetRomBytes(0xFFDC, 4));
        }

        public void CopyRomDataIn(byte[] data)
        {
            var size = data.Length;
            Debug.Assert(RomBytes.Count == size);
            for (var i = 0; i < size; i++)
            {
                RomBytes[i].Rom = data[i];
            }
        }

        public int GetROMSize() => RomBytes?.Count ?? 0;
        public ROMMapMode GetROMMapMode() => RomMapMode;
        public ROMSpeed GetROMSpeed() => RomSpeed;
        public FlagType GetFlag(int i) => RomBytes[i].TypeFlag;
        public void SetFlag(int i, FlagType flag) => RomBytes[i].TypeFlag = flag;
        public Architecture GetArchitecture(int i) => RomBytes[i].Arch;
        public void SetArchitechture(int i, Architecture arch) => RomBytes[i].Arch = arch;
        public InOutPoint GetInOutPoint(int i) => RomBytes[i].Point;
        public void SetInOutPoint(int i, InOutPoint point) => RomBytes[i].Point |= point;
        public void ClearInOutPoint(int i) => RomBytes[i].Point = 0;
        public int GetDataBank(int i) => RomBytes[i].DataBank;
        public void SetDataBank(int i, int dbank) => RomBytes[i].DataBank = (byte)dbank;
        public int GetDirectPage(int i) => RomBytes[i].DirectPage;
        public void SetDirectPage(int i, int dpage) => RomBytes[i].DirectPage = 0xFFFF & dpage;
        public bool GetXFlag(int i) => RomBytes[i].XFlag;
        public void SetXFlag(int i, bool x) => RomBytes[i].XFlag = x;
        public bool GetMFlag(int i) => RomBytes[i].MFlag;
        public void SetMFlag(int i, bool m) => RomBytes[i].MFlag = m;
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
            if (Labels.Dict.TryGetValue(i, out var val)) 
                return val?.name ?? "";

            return "";
        }
        public string GetLabelComment(int i)
        {
            if (Labels.Dict.TryGetValue(i, out var val)) 
                return val?.comment ?? "";

            return "";
        }

        public void DeleteAllLabels()
        {
            Labels.Dict.Clear();
        }

        public void AddLabel(int offset, Label label, bool overwrite)
        {
            // adding null label removes it
            if (label == null)
            {
                if (Labels.Dict.ContainsKey(offset))
                    Labels.Dict.Remove(offset);

                return;
            }

            if (overwrite)
            {
                if (Labels.Dict.ContainsKey(offset))
                    Labels.Dict.Remove(offset);
            }

            if (!Labels.Dict.ContainsKey(offset))
            {
                label.CleanUp();
                Labels.Dict.Add(offset, label);
            }
        }

        public string GetComment(int i) => Comments.Dict.TryGetValue(i, out var val) ? val : "";
        public void AddComment(int i, string v, bool overwrite)
        {
            if (v == null)
            {
                if (Comments.Dict.ContainsKey(i)) Comments.Dict.Remove(i);
            } else
            {
                if (Comments.Dict.ContainsKey(i) && overwrite) Comments.Dict.Remove(i);
                if (!Comments.Dict.ContainsKey(i)) Comments.Dict.Add(i, v);
            }
        }

        public int ConvertPCtoSNES(int offset)
        {
            return RomUtil.ConvertPCtoSNES(offset, RomMapMode, GetROMSpeed());
        }
        public int GetROMByte(int i) => RomBytes[i].Rom;
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
            return GetArchitecture(offset) switch
            {
                Data.Architecture.CPU65C816 => CPU65C816.GetInstructionLength(offset),
                Data.Architecture.APUSPC700 => 1,
                Data.Architecture.GPUSuperFX => 1,
                _ => 1
            };
        }

        private int UnmirroredOffset(int offset)
        {
            return RomUtil.UnmirroredOffset(offset, GetROMSize());
        }

        public string GetFormattedBytes(int offset, int step, int bytes)
        {
            var res = step switch
            {
                1 => "db ",
                2 => "dw ",
                3 => "dl ",
                4 => "dd ",
                _ => ""
            };

            for (var i = 0; i < bytes; i += step)
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
            return RomUtil.ConvertSNESToPC(address, RomMapMode, GetROMSize());
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
            var prefix = RomUtil.TypeToLabel(GetFlag(offset));
            var labelAddress = Util.NumberToBaseString(snes, Util.NumberBase.Hexadecimal, 6);
            return $"{prefix}_{labelAddress}";
        }

        public int Step(int offset, bool branch, bool force, int prevOffset)
        {
            return GetArchitecture(offset) switch
            {
                Data.Architecture.CPU65C816 => CPU65C816.Step(offset, branch, force, prevOffset),
                Data.Architecture.APUSPC700 => offset,
                Data.Architecture.GPUSuperFX => offset,
                _ => offset
            };
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
                var stack = new Stack<int>();
                var seenBranches = new List<int>();
                var keepGoing = true;

                while (keepGoing)
                {
                    switch (GetArchitecture(newOffset))
                    {
                        case Data.Architecture.CPU65C816:
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
                        case Data.Architecture.APUSPC700:
                        case Data.Architecture.GPUSuperFX:
                            nextOffset = Step(newOffset, false, true, prevOffset);
                            prevOffset = newOffset;
                            newOffset = nextOffset;
                            break;
                    }

                    var flag = GetFlag(newOffset);
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

        public int MarkArchitechture(int offset, Data.Architecture arch, int count)
        {
            int i, size = GetROMSize();
            for (i = 0; i < count && offset + i < size; i++) SetArchitechture(offset + i, arch);
            return offset + i < size ? offset + i : size - 1;
        }

        public int GetInstructionLength(int offset)
        {
            return GetArchitecture(offset) switch
            {
                Data.Architecture.CPU65C816 => CPU65C816.GetInstructionLength(offset),
                Data.Architecture.APUSPC700 => 1,
                Data.Architecture.GPUSuperFX => 1,
                _ => 1
            };
        }

        public int FixMisalignedFlags()
        {
            int count = 0, size = GetROMSize();

            for (var i = 0; i < size; i++)
            {
                var flag = GetFlag(i);

                switch (flag)
                {
                    case FlagType.Opcode:
                    {
                        int len = GetInstructionLength(i);
                        for (var j = 1; j < len && i + j < size; j++)
                        {
                            if (GetFlag(i + j) != Data.FlagType.Operand)
                            {
                                SetFlag(i + j, Data.FlagType.Operand);
                                count++;
                            }
                        }
                        i += len - 1;
                        break;
                    }
                    case Data.FlagType.Operand:
                        SetFlag(i, Data.FlagType.Opcode);
                        count++;
                        i--;
                        break;
                    default:
                    {
                        if (RomUtil.GetByteLengthForFlag(flag) > 1)
                        {
                            int step = RomUtil.GetByteLengthForFlag(flag);
                            for (int j = 1; j < step; j++)
                            {
                                if (GetFlag(i + j) == flag) 
                                    continue;
                                SetFlag(i + j, flag);
                                count++;
                            }
                            i += step - 1;
                        }

                        break;
                    }
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
                    switch (GetArchitecture(i))
                    {
                        case Data.Architecture.CPU65C816: CPU65C816.MarkInOutPoints(i); break;
                        case Data.Architecture.APUSPC700: break;
                        case Data.Architecture.GPUSuperFX: break;
                    }
                }
            }
        }

        public int GetIntermediateAddress(int offset, bool resolve = false)
        {
            // FIX ME: log and generation of dp opcodes. search references
            return GetArchitecture(offset) switch
            {
                Data.Architecture.CPU65C816 => CPU65C816.GetIntermediateAddress(offset, resolve),
                Data.Architecture.APUSPC700 => -1,
                Data.Architecture.GPUSuperFX => -1,
                _ => -1
            };
        }

        public string GetInstruction(int offset)
        {
            return GetArchitecture(offset) switch
            {
                Data.Architecture.CPU65C816 => CPU65C816.GetInstruction(offset),
                Data.Architecture.APUSPC700 => "",
                Data.Architecture.GPUSuperFX => "",
                _ => ""
            };
        }

        #region Equality
        protected bool Equals(Data other)
        {
            return Labels.Dict.SequenceEqual(other.Labels.Dict) && RomMapMode == other.RomMapMode && RomSpeed == other.RomSpeed && Comments.Dict.SequenceEqual(other.Comments.Dict) && RomBytes.Equals(other.RomBytes);
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

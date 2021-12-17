using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using Diz.Core.arch;
using Diz.Core.export;
using Diz.Core.util;
using IX.Observable;
using JetBrains.Annotations;

namespace Diz.Core.model.snes
{
    public class Data : ILogCreatorDataSource, ICpuOperableByteSource, INotifyPropertyChanged
    {
        // TODO: this really shouldn't be in Data, move to an outside 'SNESSystem' class or something that operates on Data
        private readonly Cpu65C816 cpu65C816;
        // don't modify these directly, always go through the public properties so
        // other objects can subscribe to modification notifications
        private RomMapMode romMapMode;
        private RomSpeed romSpeed = RomSpeed.Unknown;
        private ObservableDictionary<int, string> comments;
        private RomBytes romBytes;

        // Note: order of these public properties matters for the load/save process. Keep 'RomBytes' LAST
        // TODO: should be a way in the XML serializer to control the order, remove this comment
        // when we figure it out.
        public RomMapMode RomMapMode
        {
            get => romMapMode;
            set => this.SetField(PropertyChanged, ref romMapMode, value);
        }

        public RomSpeed RomSpeed
        {
            get => romSpeed;
            set => this.SetField(PropertyChanged, ref romSpeed, value);
        }

        // next 2 dictionaries store in SNES address format (since memory labels can't be represented as a PC address)
        public ObservableDictionary<int, string> Comments
        {
            get => comments;
            set => this.SetField(PropertyChanged, ref comments, value);
        }
        
        // for deserialization/loading in Diz2.0
        // this is kind of a hack needs rework. would be better to ditch this and write some kind of custom
        // deserializer that handles this instead
        public Dictionary<int, Label> LabelsSerialization
        {
            get => new(Labels.Labels);
            set => Labels.SetAll(value);
        }

        // RomBytes stored as PC file offset addresses (since ROM will always be mapped to disk)
        public RomBytes RomBytes
        {
            get => romBytes;
            set => this.SetField(PropertyChanged, ref romBytes, value);
        }

        public Data()
        {
            comments = new ObservableDictionary<int, string>();
            Labels = new LabelsServiceWithTemp(this);
            romBytes = new RomBytes();
            cpu65C816 = new Cpu65C816(this);
        }

        public void CreateRomBytesFromRom(IEnumerable<byte> actualRomBytes)
        {
            Debug.Assert(RomBytes.Count == 0);
            
            var previousNotificationState = RomBytes.SendNotificationChangedEvents;
            RomBytes.SendNotificationChangedEvents = false;

            RomBytes.Clear();
            foreach (var fileByte in actualRomBytes)
            {
                RomBytes.Add(new RomByte
                {
                    Rom = fileByte,
                });
            }

            RomBytes.SendNotificationChangedEvents = previousNotificationState;
        }

        private byte[] GetRomBytes(int pcOffset, int count)
        {
            var output = new byte[count];
            for (var i = 0; i < output.Length; i++)
                output[i] = (byte)GetRomByte(pcOffset + i);

            return output;
        }

        public int RomSettingsOffset => RomUtil.GetRomSettingOffset(RomMapMode);
        
        public int RomComplementOffset => RomSettingsOffset + 0x07; // 2 bytes - complement
        public int RomChecksumOffset => RomComplementOffset + 2; // 2 bytes - checksum
        
        public int CartridgeTitleStartingOffset => 
            RomUtil.GetCartridgeTitleStartingRomOffset(RomSettingsOffset);

        public string CartridgeTitleName =>
            RomUtil.GetCartridgeTitleFromBuffer(
                GetRomBytes(CartridgeTitleStartingOffset, RomUtil.LengthOfTitleName)
            );

        public uint RomComplement => (uint) GetRomWord(RomComplementOffset);
        public uint RomChecksum => (uint) GetRomWord(RomChecksumOffset);
        public uint RomCheckSumsFromRomBytes => (RomChecksum << 16) | RomComplement;

        public void CopyRomDataIn(IEnumerable<byte> trueRomBytes)
        {
            var previousNotificationState = RomBytes.SendNotificationChangedEvents;
            RomBytes.SendNotificationChangedEvents = false;
            
            var i = 0;
            foreach (var b in trueRomBytes)
            {
                RomBytes[i].Rom = b;
                ++i;
            }
            Debug.Assert(RomBytes.Count == i);

            RomBytes.SendNotificationChangedEvents = previousNotificationState;
        }
        
        // recalculates the checksum and then modifies the internal bytes in the ROM so it contains
        // the valid checksum in the ROM header.
        //
        // NOTE: this new checksum is [currently] never saved with the project file / serialized (since we don't
        // store the potentially copyrighted ROM bytes in the project file). it should just be used for
        // testing/verification purposes. (that is why this is protected, it's not part of the normal API)
        public void FixChecksum()
        {
            var rawRomBytesCopy = CreateListRawRomBytes();
            ChecksumUtil.UpdateRomChecksum(rawRomBytesCopy, RomMapMode, GetRomSize());
            RomBytes.SetBytesFrom(rawRomBytesCopy, 0);
        }
        
        // expensive and ineffecient
        protected virtual List<byte> CreateListRawRomBytes() => RomBytes.Select(rb => rb.Rom).ToList();

        // looks at the actual bytes present in the ROM and calculates their checksum
        // this is unrelated to any stored/cached checksums in the Project file. 
        public ushort ComputeChecksum() => (ushort) ChecksumUtil.ComputeChecksumFromRom(CreateListRawRomBytes());
        public bool ComputeIsChecksumValid() =>
            ChecksumUtil.IsRomChecksumValid(CreateListRawRomBytes(), RomMapMode, GetRomSize());

        public int GetRomSize() => RomBytes?.Count ?? 0;
        public FlagType GetFlag(int i) => RomBytes[i].TypeFlag;
        public void SetFlag(int i, FlagType flag) => RomBytes[i].TypeFlag = flag;
        public Architecture GetArchitecture(int i) => RomBytes[i].Arch;
        public void SetArchitecture(int i, Architecture arch) => RomBytes[i].Arch = arch;
        public InOutPoint GetInOutPoint(int i) => RomBytes[i].Point;
        public void SetInOutPoint(int i, InOutPoint point) => RomBytes[i].Point |= point;
        public void ClearInOutPoint(int i) => RomBytes[i].Point = 0;
        public int GetDataBank(int i) => RomBytes[i].DataBank;
        public void SetDataBank(int i, int dBank) => RomBytes[i].DataBank = (byte)dBank;
        public int GetDirectPage(int i) => RomBytes[i].DirectPage;
        public void SetDirectPage(int i, int dPage) => RomBytes[i].DirectPage = 0xFFFF & dPage;
        public bool GetXFlag(int i) => RomBytes[i].XFlag;
        public void SetXFlag(int i, bool x) => RomBytes[i].XFlag = x;
        public bool GetMFlag(int i) => RomBytes[i].MFlag;
        public void SetMFlag(int i, bool m) => RomBytes[i].MFlag = m;
        public int GetMxFlags(int i)
        {
            return (RomBytes[i].MFlag ? 0x20 : 0) | (RomBytes[i].XFlag ? 0x10 : 0);
        }
        public void SetMxFlags(int i, int mx)
        {
            RomBytes[i].MFlag = ((mx & 0x20) != 0);
            RomBytes[i].XFlag = ((mx & 0x10) != 0);
        }
        
        public string GetComment(int i)
        {
            if (Comments.TryGetValue(i, out var val))
                return val;

            return GetCommentText(i) ?? "";
        }
        
        public string GetCommentText(int snesAddress)
        {
            // option 1: use the comment text first
            var comment = GetComment(snesAddress);
            if (!string.IsNullOrEmpty(comment))
                return comment;

            // option 2: if a real comment doesn't exist, try see if our label itself has a comment attached, display that.
            // TODO: this is convenient for display but might mess up setting. we probably should do this
            // only in views, remove from here.
            return Labels.GetLabelComment(snesAddress) ?? "";
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

        public int ConvertPCtoSnes(int offset)
        {
            return RomUtil.ConvertPCtoSnes(offset, RomMapMode, RomSpeed);
        }
        public int GetRomByte(int i) => RomBytes[i].Rom;
        int? IReadOnlyByteSource.GetRomWord(int offset)
        {
            return GetRomWord(offset);
        }

        int? IReadOnlyByteSource.GetRomLong(int offset)
        {
            return GetRomLong(offset);
        }

        int? IReadOnlyByteSource.GetRomDoubleWord(int offset)
        {
            return GetRomDoubleWord(offset);
        }

        byte? IReadOnlyByteSource.GetRomByte(int offset)
        {
            throw new NotImplementedException();
        }

        public int GetRomWord(int offset)
        {
            if (offset + 1 < GetRomSize())
                return GetRomByte(offset) + (GetRomByte(offset + 1) << 8);
            return -1;
        }
        public int GetRomLong(int offset)
        {
            if (offset + 2 < GetRomSize())
                return GetRomByte(offset) + (GetRomByte(offset + 1) << 8) + (GetRomByte(offset + 2) << 16);
            return -1;
        }
        public int GetRomDoubleWord(int offset)
        {
            if (offset + 3 < GetRomSize())
                return GetRomByte(offset) + (GetRomByte(offset + 1) << 8) + (GetRomByte(offset + 2) << 16) + (GetRomByte(offset + 3) << 24);
            return -1;
        }
        public int GetIntermediateAddressOrPointer(int offset)
        {
            switch (GetFlag(offset))
            {
                case FlagType.Unreached:
                case FlagType.Opcode:
                    return GetIntermediateAddress(offset, true);
                case FlagType.Pointer16Bit:
                    int bank = GetDataBank(offset);
                    return (bank << 16) | GetRomWord(offset);
                case FlagType.Pointer24Bit:
                case FlagType.Pointer32Bit:
                    return GetRomLong(offset);
            }
            return -1;
        }

        public int GetBankSize()
        {
            return RomUtil.GetBankSize(RomMapMode);
        }

        public int OpcodeByteLength(int offset)
        {
            return GetArchitecture(offset) switch
            {
                Architecture.Cpu65C816 => cpu65C816.GetInstructionLength(offset),
                Architecture.Apuspc700 => 1,
                Architecture.GpuSuperFx => 1,
                _ => 1
            };
        }

        private int UnmirroredOffset(int offset)
        {
            return RomUtil.UnmirroredOffset(offset, GetRomSize());
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
                    case 1: res += Util.NumberToBaseString(GetRomByte(offset + i), Util.NumberBase.Hexadecimal, 2, true); break;
                    case 2: res += Util.NumberToBaseString(GetRomWord(offset + i), Util.NumberBase.Hexadecimal, 4, true); break;
                    case 3: res += Util.NumberToBaseString(GetRomLong(offset + i), Util.NumberBase.Hexadecimal, 6, true); break;
                    case 4: res += Util.NumberToBaseString(GetRomDoubleWord(offset + i), Util.NumberBase.Hexadecimal, 8, true); break;
                }
            }

            return res;
        }

        public int ConvertSnesToPc(int address)
        {
            return RomUtil.ConvertSnesToPc(address, RomMapMode, GetRomSize());
        }

        public int Step(int offset, bool branch, bool force, int prevOffset)
        {
            return GetArchitecture(offset) switch
            {
                Architecture.Cpu65C816 => cpu65C816.Step(offset, branch, force, prevOffset),
                Architecture.Apuspc700 => offset,
                Architecture.GpuSuperFx => offset,
                _ => offset
            };
        }

        public int AutoStep(int offset, bool harsh, int amount)
        {
            int newOffset = offset, prevOffset = offset - 1, nextOffset;
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
                        case Architecture.Cpu65C816:
                            if (seenBranches.Contains(newOffset))
                            {
                                keepGoing = false;
                                break;
                            }

                            var opcode = GetRomByte(newOffset);

                            nextOffset = Step(newOffset, false, false, prevOffset);
                            var jumpOffset = Step(newOffset, true, false, prevOffset);

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
                                stack.Push(GetMxFlags(newOffset));
                            }
                            else if (opcode == 0x28) // PLP
                            {
                                if (stack.Count == 0)
                                {
                                    keepGoing = false; break;
                                }
                                else
                                {
                                    SetMxFlags(newOffset, stack.Pop());
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
                        case Architecture.Apuspc700:
                        case Architecture.GpuSuperFx:
                            nextOffset = Step(newOffset, false, true, prevOffset);
                            prevOffset = newOffset;
                            newOffset = nextOffset;
                            break;
                    }

                    var flag = GetFlag(newOffset);
                    if (!(flag == FlagType.Unreached || flag == FlagType.Opcode || flag == FlagType.Operand)) keepGoing = false;
                }
            }
            return newOffset;
        }

        public int Mark(Action<int> MarkAction, int offset, int count)
        {
            int i, size = GetRomSize();
            for (i = 0; i < count && offset + i < size; i++) 
                MarkAction(offset + i);
            
            return offset + i < size ? offset + i : size - 1;
        }

        public int MarkTypeFlag(int offset, FlagType type, int count) => Mark(i => SetFlag(i, type), offset, count);
        public int MarkDataBank(int offset, int db, int count) => Mark(i => SetDataBank(i, db), offset, count);
        public int MarkDirectPage(int offset, int dp, int count) => Mark(i => SetDirectPage(i, dp), offset, count);
        public int MarkXFlag(int offset, bool x, int count) => Mark(i => SetXFlag(i, x), offset, count);
        public int MarkMFlag(int offset, bool m, int count) => Mark(i => SetMFlag(i, m), offset, count);
        public int MarkArchitecture(int offset, Architecture arch, int count) => Mark(i => SetArchitecture(i, arch), offset, count);

        public int GetInstructionLength(int offset)
        {
            return GetArchitecture(offset) switch
            {
                Architecture.Cpu65C816 => cpu65C816.GetInstructionLength(offset),
                Architecture.Apuspc700 => 1,
                Architecture.GpuSuperFx => 1,
                _ => 1
            };
        }

        public int FixMisalignedFlags()
        {
            int count = 0, size = GetRomSize();

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
                            if (GetFlag(i + j) != FlagType.Operand)
                            {
                                SetFlag(i + j, FlagType.Operand);
                                count++;
                            }
                        }
                        i += len - 1;
                        break;
                    }
                    case FlagType.Operand:
                        SetFlag(i, FlagType.Opcode);
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
            for (var i = 0; i < GetRomSize(); i++) ClearInOutPoint(i);

            for (var i = 0; i < GetRomSize(); i++)
            {
                if (GetFlag(i) == FlagType.Opcode)
                {
                    switch (GetArchitecture(i))
                    {
                        case Architecture.Cpu65C816: cpu65C816.MarkInOutPoints(i); break;
                        case Architecture.Apuspc700: break;
                        case Architecture.GpuSuperFx: break;
                    }
                }
            }
        }

        public int GetIntermediateAddress(int offset, bool resolve = false)
        {
            // FIX ME: log and generation of dp opcodes. search references
            return GetArchitecture(offset) switch
            {
                Architecture.Cpu65C816 => cpu65C816.GetIntermediateAddress(offset, resolve),
                Architecture.Apuspc700 => -1,
                Architecture.GpuSuperFx => -1,
                _ => -1
            };
        }

        public string GetInstruction(int offset)
        {
            return GetArchitecture(offset) switch
            {
                Architecture.Cpu65C816 => cpu65C816.GetInstruction(offset),
                Architecture.Apuspc700 => "",
                Architecture.GpuSuperFx => "",
                _ => ""
            };
        }

        public int GetNumberOfBanks()
        {
            return RomBytes.Count / GetBankSize();
        }

        public string GetBankName(int bankIndex)
        {
            var bankSnesByte = GetSnesBankByte(bankIndex);
            return Util.NumberToBaseString(bankSnesByte, Util.NumberBase.Hexadecimal, 2);
        }

        private int GetSnesBankByte(int bankIndex)
        {
            var bankStartingPcOffset = bankIndex << 16;
            var bankSnesNumber = ConvertPCtoSnes(bankStartingPcOffset) >> 16;
            return bankSnesNumber;
        }

        // get the actual ROM file bytes (i.e. the contents of the SMC file on the disk)
        // note: don't save these anywhere permanent because ROM data is usually copyrighted.
        public IEnumerable<byte> GetFileBytes()
        {
            return RomBytes.Select(b => b.Rom);
        }
        
        public virtual byte[] GetOverriddenRomBytes()
        {
            return null; // NOP
        }
        
        #region Equality
        protected bool Equals(Data other)
        {
            return Labels.Equals(other.Labels) && RomMapMode == other.RomMapMode && RomSpeed == other.RomSpeed && Comments.SequenceEqual(other.Comments) && RomBytes.Equals(other.RomBytes);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == this.GetType() && Equals((Data)obj);
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

        
        [XmlIgnore] public LabelsServiceWithTemp Labels { get; protected init; }
        [XmlIgnore] IReadOnlyLabelProvider IReadOnlyLabels.Labels => Labels;
        [XmlIgnore] ITemporaryLabelProvider ILogCreatorDataSource.TemporaryLabelProvider => Labels;

        public event PropertyChangedEventHandler PropertyChanged;
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;
using Diz.Core.arch;
using Diz.Core.export;
using Diz.Core.util;
using IX.Observable;

namespace Diz.Core.model.snes
{
    public class Data : ILogCreatorDataSource, ICpuOperableByteSource, INotifyPropertyChanged
    {
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
            return Comments.TryGetValue(i, out var val) ? val : null;
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

        public byte? GetRomByte(int pcOffset)
        {
            return RomBytes[pcOffset].Rom;
        }
        
        public byte? GetSnesByte(int snesAddress)
        {
            return GetRomByte(ConvertSnesToPc(snesAddress));
        }

        public int? GetRomWord(int offset)
        {
            if (offset + 1 >= GetRomSize())
                return null;

            var rb1Null = GetRomByte(offset);
            var rb2Null = GetRomByte(offset + 1);
            if (!rb1Null.HasValue || !rb2Null.HasValue)
                return null;

            return rb1Null + (rb2Null << 8);
        }
        public int? GetRomLong(int offset)
        {
            if (offset + 2 >= GetRomSize()) 
                return null;

            var romWord = GetRomWord(offset);
            var rb3Null = GetRomByte(offset + 2);
            if (!romWord.HasValue || !rb3Null.HasValue)
                return null;
            
            return romWord + (rb3Null << 16);
        }
        
        public int? GetRomDoubleWord(int offset)
        {
            if (offset + 3 >= GetRomSize())
                return null;

            var romLong = GetRomLong(offset);
            var rb4Null = GetRomByte(offset + 3);
            if (!romLong.HasValue || !rb4Null.HasValue)
                return null;
            
            return romLong + (rb4Null << 24);
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
                    var romWord = GetRomWord(offset);
                    if (!romWord.HasValue)
                        return -1;
                    
                    return (bank << 16) | (int)romWord;
                case FlagType.Pointer24Bit:
                case FlagType.Pointer32Bit:
                    var romLong = GetRomLong(offset);
                    if (!romLong.HasValue)
                        return -1;
                    
                    return (int)romLong;
            }
            return -1;
        }

        public int GetBankSize()
        {
            return RomUtil.GetBankSize(RomMapMode);
        }

        public int ConvertSnesToPc(int address)
        {
            return RomUtil.ConvertSnesToPc(address, RomMapMode, GetRomSize());
        }

        private Cpu CpuAt(int offset) => new CpuDispatcher().Cpu(this, offset); 

        public int Step(int offset, bool branch, bool force, int prevOffset)
        {
            return CpuAt(offset).Step(this, offset, branch, force, prevOffset);
        }

        public int AutoStepSafe(int offset)
        {
            return CpuAt(offset).AutoStepSafe(this, offset);
        }
        
        public int AutoStepHarsh(int offset, int count)
        {
            return CpuAt(offset).AutoStepHarsh(this, offset, count);
        }

        public int Mark(Action<int> markAction, int offset, int count)
        {
            int i, size = GetRomSize();
            for (i = 0; i < count && offset + i < size; i++) 
                markAction(offset + i);
            
            return offset + i < size ? offset + i : size - 1;
        }

        public int MarkTypeFlag(int offset, FlagType type, int count) => Mark(i => SetFlag(i, type), offset, count);
        public int MarkDataBank(int offset, int db, int count) => Mark(i => SetDataBank(i, db), offset, count);
        public int MarkDirectPage(int offset, int dp, int count) => Mark(i => SetDirectPage(i, dp), offset, count);
        public int MarkXFlag(int offset, bool x, int count) => Mark(i => SetXFlag(i, x), offset, count);
        public int MarkMFlag(int offset, bool m, int count) => Mark(i => SetMFlag(i, m), offset, count);
        public int MarkArchitecture(int offset, Architecture arch, int count) => Mark(i => SetArchitecture(i, arch), offset, count);

        public int GetInstructionLength(int offset) => 
            CpuAt(offset).GetInstructionLength(this, offset);
        
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
            for (var i = 0; i < GetRomSize(); i++) 
                ClearInOutPoint(i);

            for (var i = 0; i < GetRomSize(); i++)
            {
                if (GetFlag(i) == FlagType.Opcode)
                {
                    CpuAt(i).MarkInOutPoints(this, i);
                }
            }
        }

        public int GetIntermediateAddress(int offset, bool resolve = false)
        {
            // FIX ME: log and generation of dp opcodes. search references
            return CpuAt(offset).GetIntermediateAddress(this, offset, resolve);
        }

        public string GetInstruction(int offset)
        {
            return CpuAt(offset).GetInstruction(this, offset);
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

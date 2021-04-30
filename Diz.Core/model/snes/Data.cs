using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Diz.Core.arch;
using Diz.Core.export;
using Diz.Core.model.byteSources;
using Diz.Core.util;
using JetBrains.Annotations;

namespace Diz.Core.model.snes
{
    // old-style data container to phase out eventually.
    // right now this supports just 1 ByteSource that is a SNES address space with an attached Rom
    //
    // the underlying new ByteSource stuff it uses supports more, but right now, the rest of the app doesn't.
    // Data is the bridge between the old and new.
    public class Data : ILogCreatorDataSource, ICpuOperableByteSource
    {
        // the parent of all our data, the SNES address space
        public ByteSource SnesAddressSpace { get; }
        
        // TODO: gotta carefully think about the serialization here. we need to not output bytes from the ROM itself.
        // everything else is fine.
        
        #region Helper Access (Don't serialize)
        
        [XmlIgnore] public ILabelProvider Labels { get; protected init; }
        [XmlIgnore] IReadOnlyLabelProvider IReadOnlySnesRom.Labels => Labels;
        [XmlIgnore] ITemporaryLabelProvider ILogCreatorDataSource.TemporaryLabelProvider => Labels;

        // cached access to stuff that livers in SnesAddressSpace. convenience only.
        [XmlIgnore] public ByteSource RomByteSource => 
            RomByteSourceMapping?.ByteSource;
        [XmlIgnore] public RegionMappingSnesRom RomMapping => 
            (RegionMappingSnesRom) RomByteSourceMapping?.RegionMapping;
        [XmlIgnore] public ByteSourceMapping RomByteSourceMapping =>
            SnesAddressSpace?.ChildSources
                ?.SingleOrDefault(map => 
                    map?.RegionMapping?.GetType() == typeof(RegionMappingSnesRom));
        
        [XmlIgnore] public RomMapMode RomMapMode => RomMapping?.RomMapMode ?? default;
        [XmlIgnore] public RomSpeed RomSpeed => RomMapping?.RomSpeed ?? default;
        
        // [XmlIgnore] private bool SendNotificationChangedEvents { get; set; } = true;
        
        #endregion

        public Data()
        {
            SnesAddressSpace = RomUtil.CreateSnesAddressSpace();
            Labels = new LabelProvider(this);
        }

        // NOTE: specially named parameterized constructor for XML serializer
        [UsedImplicitly] 
        public Data(ByteSource snesAddressSpace)
        {
            SnesAddressSpace = snesAddressSpace;
        }
        
        private byte[] GetRomBytes(int snesOffset, int count)
        {
            var output = new byte[count];
            for (var i = 0; i < output.Length; i++)
            {
                var snesAddress = snesOffset + i;
                output[i] = GetSnesByte(snesAddress) 
                            ?? throw new InvalidDataException($"No data at SNES offset {snesAddress:X}");
            }

            return output;
        }

        // TODO: offset isn't snes it's rom? how is this still able to work? figure it out and fix variable naming
        public string GetRomNameFromRomBytes() => GetFixedLengthStr(0xFFC0, 21);

        private string GetFixedLengthStr(int snesOffset, int count)
        {
            return Encoding.UTF8.GetString(GetRomBytes(snesOffset, count));
        }

        // TODO: offset isn't snes it's rom? how is this still able to work? figure it out and fix variable naming
        // TODO: replace with GetRomDoubleWord()
        public int GetRomCheckSumsFromRomBytes() => ByteUtil.ByteArrayToInt32(GetRomBytes(0xFFDC, 4));

        public int GetRomSize() => RomByteSource?.Bytes?.Count ?? 0;
        
        // -------------
        // probably can move some of this annotation stuff into ByteSource
        // -------------
        
        public T GetOneAnnotationAtPc<T>(int pcOffset) where T : Annotation, new()
        {
            var snesAddress = ConvertPCtoSnes(pcOffset);
            return SnesAddressSpace.GetOneAnnotation<T>(snesAddress);
        }

        public T GetOrCreateAnnotationAtPc<T>(int pcOffset) where T : Annotation, new()
        {
            var snesOffset = ConvertPCtoSnes(pcOffset);
            return _GetOrCreateAnnotation<T>(snesOffset).annotation;
        }
        
        private (T annotation, bool wasExisting) _GetOrCreateAnnotation<T>(int snesOffset) where T : Annotation, new()
        {
            var existing = SnesAddressSpace.GetOneAnnotation<T>(snesOffset);
            if (existing != null)
                return (existing, true);

            var newAnnotation = new T();

            // NOTE: for now, we add all annotations on the Snes Address space itself.
            // in the future, we might want to push them further down into nested ByteSource's like the 
            // ROM itself, or, the WRAM.  Especially useful because pushing something into a mirror WRAM region
            // will make the mirroring automagically work out nicely.
            SnesAddressSpace.AddAnnotation(snesOffset, newAnnotation);
            
            return (newAnnotation, false);
        }

        public FlagType GetFlag(int i) => GetOneAnnotationAtPc<MarkAnnotation>(i)?.TypeFlag ?? default;
        public void SetFlag(int i, FlagType flag) => GetOrCreateAnnotationAtPc<MarkAnnotation>(i).TypeFlag = flag;
        public Architecture GetArchitecture(int i) => GetOneAnnotationAtPc<OpcodeAnnotation>(i)?.Arch ?? default;
        public void SetArchitecture(int i, Architecture arch) => 
            GetOrCreateAnnotationAtPc<OpcodeAnnotation>(i).Arch = arch;
        
        public InOutPoint GetInOutPoint(int i) => GetOneAnnotationAtPc<BranchAnnotation>(i)?.Point ?? default;
        public void SetInOutPoint(int i, InOutPoint point) => GetOrCreateAnnotationAtPc<BranchAnnotation>(i).Point |= point;
        public void ClearInOutPoint(int i) => GetOneAnnotationAtPc<BranchAnnotation>(i).Point = 0;
        public int GetDataBank(int i) => GetOneAnnotationAtPc<OpcodeAnnotation>(i)?.DataBank ?? default;
        public void SetDataBank(int i, int dBank) => GetOrCreateAnnotationAtPc<OpcodeAnnotation>(i).DataBank = (byte)dBank;
        public int GetDirectPage(int i) => GetOneAnnotationAtPc<OpcodeAnnotation>(i)?.DirectPage ?? default;
        public void SetDirectPage(int i, int dPage) => GetOrCreateAnnotationAtPc<OpcodeAnnotation>(i).DirectPage = 0xFFFF & dPage;
        public bool GetXFlag(int i) => GetOneAnnotationAtPc<OpcodeAnnotation>(i)?.XFlag ?? default;
        public void SetXFlag(int i, bool x) => GetOrCreateAnnotationAtPc<OpcodeAnnotation>(i).XFlag = x;
        public bool GetMFlag(int i) => GetOneAnnotationAtPc<OpcodeAnnotation>(i)?.MFlag ?? default;

        public void SetMFlag(int i, bool m) => GetOrCreateAnnotationAtPc<OpcodeAnnotation>(i).MFlag = m;
        public int GetMxFlags(int i)
        {
            var opcodeAnnotation = GetOneAnnotationAtPc<OpcodeAnnotation>(i);
            if (opcodeAnnotation == null)
                return 0;
            
            return (opcodeAnnotation.MFlag ? 0x20 : 0) | (opcodeAnnotation.XFlag ? 0x10 : 0);
        }
        public void SetMxFlags(int i, int mx)
        {
            var opcodeAnnotation = GetOrCreateAnnotationAtPc<OpcodeAnnotation>(i);
            opcodeAnnotation.MFlag = (mx & 0x20) != 0;
            opcodeAnnotation.XFlag = (mx & 0x10) != 0;
        }
        
        private static bool IsComment(Annotation annotation) => annotation.GetType() == typeof(Comment);
        
        public string GetCommentText(int i)
        {
            // option 1: use the comment text first
            var comment = GetOneAnnotationAtPc<Comment>(i);
            if (comment != null)
                return comment.Text;

            // if that doesn't exist, try see if our label itself has a comment attached, display that.
            return Labels.GetLabelComment(ConvertPCtoSnes(i)) ?? "";
        }
        
        public Comment GetComment(int i) => GetOneAnnotationAtPc<Comment>(i);

        // setting text to null will remove the comment instead of adding anything
        public void AddComment(int snesAddress, string commentTextToAdd, bool overwrite = false)
        {
            if (commentTextToAdd == null || overwrite)
            {
                SnesAddressSpace.RemoveAllAnnotationsAt(snesAddress, IsComment);
                if (commentTextToAdd == null)
                    return;
            }
            
            var existing = SnesAddressSpace.GetOneAnnotation<Comment>(snesAddress);
            if (existing != null) 
                return;
            
            SnesAddressSpace.AddAnnotation(snesAddress, new Comment {Text = commentTextToAdd});
        }

        // get the value of the byte at ROM index i
        // throws an exception if no byte present at that address
        public ByteEntry BuildFlatByteEntryForRom(int romOffset)
        {
            var snesAddress = ConvertPCtoSnes(romOffset);
            return SnesAddressSpace.BuildFlatByteEntryFor(snesAddress);
        }

        public byte? GetRomByte(int pcOffset)
        {
            // weird thing: even though we're asking for a byte from the ROM,
            // we should always access it via the top-level ByteSource which is the SNES address space.
            // so, convert to that, and access via that.
            return GetSnesByte(ConvertPCtoSnes(pcOffset));
        }
        
        public byte? GetSnesByte(int snesAddress)
        {
            return SnesAddressSpace.BuildFlatByteEntryFor(snesAddress)?.Byte;
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
        
        // NOTE: technically not always correct. banks wrap around so, theoretically we should check what operation
        // we're doing and wrap to the beginning of the bank. for now.... just glossing over it, bigger fish to fry.
        // "past me" apologizes to 'future you' for this if you got hung up here.
        //
        // returns null if out of bounds
        public byte? GetNextRomByte(int pcOffset)
        {
            return pcOffset + 1 >= 0 && pcOffset + 1 < RomByteSource.Bytes.Count
                ? RomByteSource.Bytes[pcOffset + 1].Byte
                : null;
        }

        public int GetBankSize()
        {
            return RomUtil.GetBankSize(RomMapMode);
        }

        public ByteEntry BuildFlatByteEntryForSnes(int snesAddress)
        {
            return SnesAddressSpace?.BuildFlatByteEntryFor(snesAddress);
        }

        public int ConvertSnesToPc(int snesAddress)
        {
            return RomUtil.ConvertSnesToPc(snesAddress, RomMapMode, GetRomSize());
        }
        
        public int ConvertPCtoSnes(int pcOffset)
        {
            return RomUtil.ConvertPCtoSnes(pcOffset, RomMapMode, RomSpeed);
        }

        private Cpu CpuAt(int offset) => new CpuDispatcher().Cpu(this, offset); 

        public int Step(int offset, bool branch, bool force, int prevOffset)
        {
            return CpuAt(offset).Step(this, offset, branch, force, prevOffset);
        }

        public int PerformActionOnRange(Action<int> markAction, int offset, int count)
        {
            int i, size = GetRomSize();
            for (i = 0; i < count && offset + i < size; i++) 
                markAction(offset + i);
            
            return offset + i < size ? offset + i : size - 1;
        }

        public int MarkTypeFlag(int offset, FlagType type, int count) => 
            PerformActionOnRange(i => SetFlag(i, type), offset, count);
        public int MarkDataBank(int offset, int db, int count) => 
            PerformActionOnRange(i => SetDataBank(i, db), offset, count);
        public int MarkDirectPage(int offset, int dp, int count) => 
            PerformActionOnRange(i => SetDirectPage(i, dp), offset, count);
        public int MarkXFlag(int offset, bool x, int count) => 
            PerformActionOnRange(i => SetXFlag(i, x), offset, count);
        public int MarkMFlag(int offset, bool m, int count) => 
            PerformActionOnRange(i => SetMFlag(i, m), offset, count);
        public int MarkArchitecture(int offset, Architecture arch, int count) => 
            PerformActionOnRange(i => SetArchitecture(i, arch), offset, count);

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
            return RomByteSource.Bytes.Count / GetBankSize();
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

        public bool IsMatchingIntermediateAddress(int intermediateAddress, int addressToMatch)
        {
            var intermediateAddressOrPointer = GetIntermediateAddressOrPointer(intermediateAddress);
            var destinationOfIa = ConvertSnesToPc(intermediateAddressOrPointer);

            return destinationOfIa == addressToMatch;
        }

        // public event PropertyChangedEventHandler? PropertyChanged;

        #region Equality
        protected bool Equals(Data other)
        {
            return RomMapMode == other.RomMapMode && RomSpeed == other.RomSpeed && SnesAddressSpace.Equals(other.SnesAddressSpace);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((Data)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (397) ^ (int)RomMapMode;
                hashCode = (hashCode * 397) ^ (int)RomSpeed;
                
                // TODO: udpate this.?
                
                return hashCode;
            }
        }
        #endregion

        public int AutoStep(int offset, bool harsh, int count) => 
            CpuAt(offset).AutoStep(this, offset, harsh, count);
    }
}

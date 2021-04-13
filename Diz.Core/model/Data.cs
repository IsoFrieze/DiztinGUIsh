using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Diz.Core.arch;
using Diz.Core.export;
using Diz.Core.model.byteSources;
using Diz.Core.util;

namespace Diz.Core.model
{
    // old-style data container to phase out eventually.
    // right now this supports just 1 ByteSource that is a SNES address space with an attached Rom
    //
    // the underlying new ByteSource stuff it uses supports more, but right now, the rest of the app doesn't.
    // Data is the bridge between the old and new.
    public class Data : ILogCreatorDataSource, ICpuOperableByteSource
    {
        // TODO: gotta carefully think about the serialization here. we need to not output bytes from the ROM itself.
        // everything else is fine.
        
        public LabelProvider Labels { get; }
        IReadOnlyLabelProvider IReadOnlySnesRom.Labels => Labels;
        ITemporaryLabelProvider ILogCreatorDataSource.TemporaryLabelProvider => Labels;
        
        // the parent of all our data, the SNES address space
        public ByteSource SnesAddressSpace { get; private set; }
        
        // cached access to stuff that livers in SnesAddressSpace. convenience only.
        public ByteSource RomByteSource => 
            RomByteSourceMapping?.ByteSource;
        public RegionMappingSnesRom RomMapping => 
            (RegionMappingSnesRom) RomByteSourceMapping?.RegionMapping;
        public ByteSourceMapping RomByteSourceMapping =>
            SnesAddressSpace?.ChildSources
                ?.SingleOrDefault(map => 
                    map?.RegionMapping?.GetType() == typeof(RegionMappingSnesRom));

        // private bool SendNotificationChangedEvents { get; set; } = true;

        public RomMapMode RomMapMode => RomMapping?.RomMapMode ?? default;
        public RomSpeed RomSpeed => RomMapping?.RomSpeed ?? default;

        #region Initialization Helpers

        public void PopulateFrom(IReadOnlyCollection<byte> actualRomBytes, RomMapMode romMapMode, RomSpeed romSpeed)
        {
            var mapping = RomUtil.CreateRomMappingFromRomRawBytes(actualRomBytes, romMapMode, romSpeed);
            PopulateFrom(mapping);
        }

        public void PopulateFromRom(ByteSource romByteSource, RomMapMode romMapMode, RomSpeed romSpeed)
        {
            var mapping = RomUtil.CreateRomMappingFromRomByteSource(romByteSource, romMapMode, romSpeed);
            PopulateFrom(mapping);
        }

        public void PopulateFrom(ByteSourceMapping romByteSourceMapping)
        {
            // var previousNotificationState = SendNotificationChangedEvents;
            // SendNotificationChangedEvents = false;

            // setup a common SNES mapping, just the ROM and nothing else.
            // this is very configurable, for now, this class is sticking with the simple setup.
            // you can get as elaborate as you want, with RAM, patches, overrides, etc.
            SnesAddressSpace = RomUtil.CreateSnesAddressSpace();
            SnesAddressSpace.AttachChildByteSource(romByteSourceMapping);

            //SendNotificationChangedEvents = previousNotificationState;
        }

        // precondition, everything else has already been setup but adding in the actual bytes,
        // and is ready for actual rom byte data now
        public void PopulateFrom(IReadOnlyCollection<byte> actualRomBytes)
        {
            // this method is basically a shortcut which only works under some very specific constraints
            Debug.Assert(SnesAddressSpace != null);
            Debug.Assert(SnesAddressSpace.ChildSources.Count == 1);
            Debug.Assert(SnesAddressSpace.ChildSources[0].RegionMapping.GetType() == typeof(RegionMappingSnesRom));
            Debug.Assert(ReferenceEquals(RomByteSourceMapping, SnesAddressSpace.ChildSources[0]));
            Debug.Assert(RomMapping != null);
            Debug.Assert(RomByteSourceMapping?.ByteSource != null);
            Debug.Assert(actualRomBytes.Count == RomByteSource.Bytes.Count);

            var i = 0;
            foreach (var b in actualRomBytes)
            {
                RomByteSource.Bytes[i].Byte = b;
                ++i;
            }
        }
        
        public Data InitializeEmptyRomMapping(int size, RomMapMode mode, RomSpeed speed)
        {
            var romByteSource = new ByteSource
            {
                Bytes = new ByteList(size),
                Name = "Snes ROM"
            };
            PopulateFromRom(romByteSource, mode, speed);
            return this;
        }
        
        #endregion
        
        private byte[] GetRomBytes(int snesOffset, int count)
        {
            var output = new byte[count];
            for (var i = 0; i < output.Length; i++)
            {
                var pcOffset = ConvertSnesToPc(snesOffset + i);
                output[i] = GetRomByte(pcOffset);
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

        public Data()
        {
            Labels = new LabelProvider(this);
        }

        public Data(ByteSource romByteSource, RomMapMode romMapMode, RomSpeed romSpeed) : this()
        {
            PopulateFromRom(romByteSource, romMapMode, romSpeed);
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
        public byte GetRomByte(int pcOffset)
        {
            // weird thing: even though we're asking for a byte from the ROM,
            // we should always access it via the top-level ByteSource which is the SNES address space.
            // so, convert to that, and access via that.
            return GetSnesByte(ConvertPCtoSnes(pcOffset));
        }

        public byte GetSnesByte(int snesAddress)
        {
            return SnesAddressSpace.GetByte(snesAddress);
        }

        public int GetRomWord(int offset)
        {
            if (offset + 1 >= GetRomSize()) 
                return -1;
            
            return GetRomByte(offset) + (GetRomByte(offset + 1) << 8);
        }
        public int GetRomLong(int offset)
        {
            if (offset + 2 >= GetRomSize()) 
                return -1;
            
            return GetRomByte(offset) + (GetRomByte(offset + 1) << 8) + (GetRomByte(offset + 2) << 16);
        }
        public int GetRomDoubleWord(int offset)
        {
            if (offset + 3 >= GetRomSize()) 
                return -1;
            
            return GetRomByte(offset) + (GetRomByte(offset + 1) << 8) + (GetRomByte(offset + 2) << 16) + (GetRomByte(offset + 3) << 24);
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

        private int UnmirroredOffset(int offset)
        {
            return RomUtil.UnmirroredOffset(offset, GetRomSize());
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

        // get the actual ROM file bytes (i.e. the contents of the SMC file on the disk)
        // note: don't save these anywhere permanent because ROM data is usually copyrighted.
        public IEnumerable<byte> GetFileBytes()
        {
            return RomByteSource.Bytes.Select(b => ((ByteEntry) b).Byte.Value);
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
            return obj.GetType() == this.GetType() && Equals((Data)obj);
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

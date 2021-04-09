using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Diz.Core.arch;
using Diz.Core.export;
using Diz.Core.util;
using IX.Observable;

namespace Diz.Core.model
{
    // old-style data container to phase out eventually.
    // right now this supports just 1 ByteSource that is a SNES address space with an attached Rom
    //
    // the underlying new ByteSource stuff it uses supports more, but right now, the rest of the app doesn't.
    // Data is the bridge between the old and new.
    public class Data : ILogCreatorDataSource, ICpuOperableByteSource
    {
        public IEnumerable<KeyValuePair<int, Label>> Labels => SnesAddressSpace.GetAnnotationEnumerator<Label>();

        // TODO: gotta carefully think about the serialization here. we need to not output bytes from the ROM itself.
        // everything else is fine.
        public SnesAddressSpaceByteSource SnesAddressSpace { get; set; }
        public ByteSource RomByteSource { get; set; }
        
        // private bool SendNotificationChangedEvents { get; set; } = true;

        public RomMapMode RomMapMode { get; set; }
        public RomSpeed RomSpeed { get; set; }
        
        [Obsolete("Use RomByteSource instead")]
        public List<ByteOffsetData> RomBytes => RomByteSource?.Bytes;

        private static ByteSourceMapping CreateMappingFromRomRawBytes(
            IReadOnlyCollection<byte> actualRomBytes, RomSpeed romSpeed, RomMapMode romMapMode
            ) => new()
            {
                ByteSource = new ByteSource(actualRomBytes),
                RegionMapping = new RegionMappingSnesRom
                {
                    RomSpeed = romSpeed,
                    RomMapMode = romMapMode,
                }
            };

        public void PopulateFrom(IReadOnlyCollection<byte> actualRomBytes) => PopulateFrom(
                CreateMappingFromRomRawBytes(actualRomBytes, RomSpeed, RomMapMode) );

        public void PopulateFrom(ByteSourceMapping romByteSourceMapping)
        {
            // var previousNotificationState = SendNotificationChangedEvents;
            // SendNotificationChangedEvents = false;
            
            RomByteSource = romByteSourceMapping.ByteSource;
            SnesAddressSpace = new SnesAddressSpaceByteSource
            {
                ChildSources = new List<ByteSourceMapping>
                {
                    romByteSourceMapping
                }
            };
            
            //SendNotificationChangedEvents = previousNotificationState;
        }

        // TODO: something is messed up or just happens to work with the conversion of SNES->PC addresses here
        // fix conversion of snes->pc
        private byte[] GetRomBytes(int snesOffset, int count)
        {
            var output = new byte[count];
            for (var i = 0; i < output.Length; i++)
                output[i] = (byte)GetRomByte(ConvertSnesToPc(snesOffset + i));

            return output;
        }

        public string GetRomNameFromRomBytes()
        {
            // TODO: offset isn't snes it's rom? how is this still able to work? figure it out and fix variable naming
            return Encoding.UTF8.GetString(GetRomBytes(0xFFC0, 21));
        }

        public int GetRomCheckSumsFromRomBytes()
        {
            // TODO: offset isn't snes it's rom? how is this still able to work? figure it out and fix variable naming
            return ByteUtil.ByteArrayToInt32(GetRomBytes(0xFFDC, 4));
        }

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
        public string GetLabelName(int i)
        {
            var label = GetOneAnnotationAtPc<Label>(i);
            return label?.Name ?? "";
        }
        
        public string GetLabelComment(int i)
        {
            var label = GetOneAnnotationAtPc<Label>(i);
            return label?.Comment ?? "";
        }

        private static bool IsLabel(Annotation annotation) => annotation.GetType() == typeof(Label);
        private static bool IsComment(Annotation annotation) => annotation.GetType() == typeof(Comment);

        public void DeleteAllLabels()
        {
            SnesAddressSpace.RemoveAllAnnotations(IsLabel);
        }

        public void RemoveLabel(int snesAddress)
        {
            SnesAddressSpace.RemoveAllAnnotationsAt(snesAddress, IsLabel);
        }

        public void AddLabel(int snesAddress, Label labelToAdd, bool overwrite)
        {
            Debug.Assert(labelToAdd != null);
            
            if (overwrite)
                RemoveLabel(snesAddress);

            var existing = SnesAddressSpace.GetOneAnnotation<Label>(snesAddress);
            
            if (existing == null)
                SnesAddressSpace.AddAnnotation(snesAddress, labelToAdd);
        }

        public string GetCommentText(int i)
        {
            // option 1: use the comment text first
            var comment = GetOneAnnotationAtPc<Comment>(i);
            if (comment != null)
                return comment.Text;

            // if that doesn't exist, try see if our label itself has a comment attached, display that.
            return GetLabelComment(i) ?? "";
        }

        public Label GetLabel(int i) => GetOneAnnotationAtPc<Label>(i);
        public Comment GetComment(int i) => GetOneAnnotationAtPc<Comment>(i);

        // setting text to null will remove the comment instead of adding anything
        public void AddComment(int snesAddress, string commentTextToAdd, bool overwrite)
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
        public int GetRomByte(int pcOffset)
        {
            // TODO: we should put ALL stuff in terms of SNES address space. don't convert anymore.
            var snesOffset = ConvertSnesToPc(pcOffset);

            // TODO: why are we returning int and not byte?
            
            var dataAtOffset = GetRawDataAtSnesOffset(snesOffset);
            
            if (dataAtOffset?.Byte == null)
                throw new InvalidDataException("ERROR: GetRomByte() doesn't map to a real byte");

            return (int) dataAtOffset.Byte;
        }

        private ByteOffsetData GetRawDataAtSnesOffset(int snesOffset)
        {
            // PERF NOTE: this is now doing graph traversal and memory allocation, could get expensive
            // if called a lot. Keep an eye on it and do some caching if needed.
            return SnesAddressSpace.CompileAllChildDataAt(snesOffset);
        }

        // NOTE: technically not always correct. banks wrap around so, theoretically we should check what operation
        // we're doing and wrap to the beginning of the bank. for now.... just glossing over it, bigger fish to fry.
        // "past me" apologizes to 'future you' for this if you got hung up here.
        //
        // returns null if out of bounds
        public byte? GetNextRomByte(int offset)
        {
            return offset + 1 >= 0 && offset + 1 < RomByteSource.Bytes.Count
                ? RomByteSource.Bytes[offset + 1].Byte
                : null;
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
            return RomByteSource.Bytes.Select(b => ((ByteOffsetData) b).Byte.Value);
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
        
        
        public void AddTemporaryLabel(Label label)
        {
            throw new NotImplementedException();
        }

        public void ClearTemporaryLabels()
        {
            throw new NotImplementedException();
        }

        public int AutoStep(int offset, bool harsh, int count) => 
            CpuAt(offset).AutoStep(this, offset, harsh, count);
    }
}

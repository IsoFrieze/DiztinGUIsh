using System.Diagnostics;
using Diz.Core;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Cpu._65816;

public interface ISnesChecksum
{
    public int RomSettingsOffset { get; }

    public uint RomComplement { get; }
    public uint RomChecksum { get; }
    public uint RomCheckSumsFromRomBytes { get; }

    public int RomComplementOffset { get; }
    public int RomChecksumOffset { get; }
    
    public ushort ComputeChecksum();
    public bool ComputeIsChecksumValid();

    public void FixChecksum();
}

public interface ISnesCartName
{
    public int CartridgeTitleStartingOffset { get; }
    public string CartridgeTitleName { get; }
}

public interface IFixMisalignedFlags
{
    int FixMisalignedFlags();
}

public interface IMiscNavigable
{
    public (int unreachedOffsetFound, int iaSourceAddress) FindNextUnreachedInPointAfter(int startingOffset, bool searchForward = true);
}

public interface ISnesApi<out TData> :
    IRomByteFlagsGettable, 
    IRomByteFlagsSettable, 
    ISnesAddressConverter, 
    ISteppable,
    IMiscNavigable,
    IAutoSteppable,
    ISnesIntermediateAddress, 
    IInOutPointSettable, 
    IInOutPointGettable,
    IReadOnlyByteSource,
    IReadOnlyLabels,
    IRomMapProvider,
    IRomSize,
    ISnesBankInfo,
    ISnesChecksum,
    ISnesCartName,
    IInstructionGettable,
    IFixMisalignedFlags,
    IArchitectureApi,
    IMarkOperandAndOpcode

    where TData : IData
{
    TData Data { get; }

    public void CacheVerificationInfoFor(ISnesCachedVerificationInfo verificationCache);
}

public interface ISnesData : ISnesApi<IData>
{
    
}

public class SnesApi : ISnesData
{
    public IData Data { get; }
    
    public SnesApi(IData data)
    {
        Data = data;
    }

    public int RomSettingsOffset => RomUtil.GetRomSettingOffset(Data.RomMapMode);
    public int RomComplementOffset => RomSettingsOffset + 0x07; // 2 bytes - complement
    public int RomChecksumOffset => RomComplementOffset + 2; // 2 bytes - checksum
        
    public int CartridgeTitleStartingOffset => 
        RomUtil.GetCartridgeTitleStartingRomOffset(RomSettingsOffset);

    public string CartridgeTitleName =>
        RomUtil.GetCartridgeTitleFromBuffer(
            Data.GetRomBytes(CartridgeTitleStartingOffset, RomUtil.LengthOfTitleName)
        );
    
    // recalculates the checksum and then modifies the internal bytes in the ROM so it contains
    // the valid checksum in the ROM header.
    //
    // NOTE: this new checksum is [currently] never saved with the project file / serialized (since we don't
    // store the potentially copyrighted ROM bytes in the project file). it should just be used for
    // testing/verification purposes. (that is why this is protected, it's not part of the normal API)
    public void FixChecksum()
    {
        var rawRomBytesCopy = Data.RomBytes.CreateListRawRomBytes();
        ChecksumUtil.UpdateRomChecksum(rawRomBytesCopy, Data.RomMapMode, GetRomSize());
        Data.RomBytes.SetBytesFrom(rawRomBytesCopy, 0);
    }

    // looks at the actual bytes present in the ROM and calculates their checksum
    // this is unrelated to any stored/cached checksums in the Project file. 
    public ushort ComputeChecksum() => 
        (ushort) ChecksumUtil.ComputeChecksumFromRom(Data.RomBytes.CreateListRawRomBytes());
        
    public bool ComputeIsChecksumValid() =>
        ChecksumUtil.IsRomChecksumValid(Data.RomBytes.CreateListRawRomBytes(), Data.RomMapMode, GetRomSize());

    public uint RomComplement => (uint) Data.GetRomWord(RomComplementOffset)!;
    public uint RomChecksum => (uint) Data.GetRomWord(RomChecksumOffset)!;
    public uint RomCheckSumsFromRomBytes => (RomChecksum << 16) | RomComplement;

    public FlagType GetFlag(int i) => Data.RomBytes[i].TypeFlag;
    public void SetFlag(int i, FlagType flag) => Data.RomBytes[i].TypeFlag = flag;

    public InOutPoint GetInOutPoint(int i) => Data.RomBytes[i].Point;

    public void SetInOutPoint(int i, InOutPoint point) => Data.RomBytes[i].Point |= point;
    public void ClearInOutPoint(int i) => Data.RomBytes[i].Point = 0;
    public int GetDataBank(int i) => Data.RomBytes[i].DataBank;
    public void SetDataBank(int i, int dBank) => Data.RomBytes[i].DataBank = (byte)dBank;
    public int GetDirectPage(int i) => Data.RomBytes[i].DirectPage;
    public void SetDirectPage(int i, int dPage) => Data.RomBytes[i].DirectPage = 0xFFFF & dPage;
    public bool GetXFlag(int i) => Data.RomBytes[i].XFlag;
    public void SetXFlag(int i, bool x) => Data.RomBytes[i].XFlag = x;
    public bool GetMFlag(int i) => Data.RomBytes[i].MFlag;
    public void SetMFlag(int i, bool m) => Data.RomBytes[i].MFlag = m;
    public int GetMxFlags(int i)
    {
        return (Data.RomBytes[i].MFlag ? 0x20 : 0) | (Data.RomBytes[i].XFlag ? 0x10 : 0);
    }
    public void SetMxFlags(int i, int mx)
    {
        Data.RomBytes[i].MFlag = ((mx & 0x20) != 0);
        Data.RomBytes[i].XFlag = ((mx & 0x10) != 0);
    }

    public int ConvertPCtoSnes(int offset) => 
        RomUtil.ConvertPCtoSnes(offset, Data.RomMapMode, Data.RomSpeed);

    public int ConvertSnesToPc(int address) => 
        RomUtil.ConvertSnesToPc(address, Data.RomMapMode, GetRomSize());

    public int GetIntermediateAddressOrPointer(int offset)
    {
        switch (GetFlag(offset))
        {
            case FlagType.Unreached:
            case FlagType.Opcode:
                return GetIntermediateAddress(offset, true);
            case FlagType.Pointer16Bit:
                int bank = GetDataBank(offset);
                var romWord = Data.GetRomWord(offset);
                if (!romWord.HasValue)
                    return -1;
                    
                return (bank << 16) | (int)romWord;
            case FlagType.Pointer24Bit:
            case FlagType.Pointer32Bit:
                var romLong = Data.GetRomLong(offset);
                if (!romLong.HasValue)
                    return -1;
                    
                return (int)romLong;
        }
        return -1;
    }
    
    public bool IsMatchingIntermediateAddress(int intermediateAddress, int addressToMatch)
    {
        var intermediateAddressOrPointer = GetIntermediateAddressOrPointer(intermediateAddress);
        var destinationOfIa = ConvertSnesToPc(intermediateAddressOrPointer);

        return destinationOfIa == addressToMatch;
    }

    public int GetRomSize() => 
        Data.RomBytes?.Count ?? 0;

    public int GetBankSize() => 
        RomUtil.GetBankSize(Data.RomMapMode);

    public int GetNumberOfBanks()
    {
        var bankSize = GetBankSize();
        return bankSize == 0 
            ? 0 
            : GetRomSize() / bankSize;
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
    
    private Cpu<SnesApi> GetCpu(int offset) => 
        new CpuDispatcher().Cpu(this, offset);

    public int GetInstructionLength(int offset) => 
        GetCpu(offset).GetInstructionLength(this, offset);
    
    public int Step(int offset, bool branch, bool force=false, int prevOffset=-1) =>
        GetCpu(offset).Step(this, offset, branch, force, prevOffset);

    public int AutoStepSafe(int offset) =>
        GetCpu(offset).AutoStepSafe(this, offset);

    public int AutoStepHarsh(int offset, int count) =>
        GetCpu(offset).AutoStepHarsh(this, offset, count);

    public void MarkAsOpcodeAndOperandsStartingAt(int offset, int? dataBank = null, int? directPage = null, bool? xFlag = null, bool? mFlag = null)
    {
        if (GetCpu(offset) is not Cpu65C816<SnesApi> cpu65816)
            return;

        cpu65816.MarkAsOpcodeAndOperandsStartingAt(this, offset, dataBank, directPage, xFlag, mFlag);
    }
    
    public (int unreachedOffsetFound, int iaSourceAddress) FindNextUnreachedInPointAfter(int startingOffset, bool searchForward = true)
    {
        var direction = searchForward ? 1 : -1;
        for (
            var offsetToTry = startingOffset + direction; 
            offsetToTry >= 0 && offsetToTry < Data.RomBytes.Count; 
            offsetToTry += direction
        ) {
            var romByte = Data.RomBytes[offsetToTry];
            
            // must be unreached, or we don't care about it
            // (we're trying to quickly uncover new parts of the code we can step through that were missed before) 
            if (romByte.TypeFlag != FlagType.Unreached)
                continue;
            
            // something must jump to US or we don't care
            if ((romByte.Point & InOutPoint.InPoint) == 0)
                continue;

            // this is a legit in point, but, is the place it came FROM legit? return true if at least one legit
            // opcode from the origin of this point.
            // (warning: kinda expensive search, we have to scan EVERYTHING top to bottom)
            // consider some caching of references when we create in/out points to save this.
            //
            
            // search entire ROM for already marked opcodes whose IA jumps land on US.
            // TODO: this search could form the basis of a "find references to X address" calculator
            for (var i = 0; i < GetRomSize(); i++)
            {
                // we're looking for the OPCODE whose indirect address matches our candidate.
                if (GetFlag(i) != FlagType.Opcode)
                    continue;
                
                var cpu = GetCpu(i);
                var iaOffsetPc = cpu.CalculateInOutPointsFromOffset(this, i, out var newIaInOutPoint, out var newOffsetInOutPoint);
                
                // does the intermediate address of this other location match the offset we're searching for
                if (iaOffsetPc == -1 || iaOffsetPc != offsetToTry)
                    continue;

                var sourceIsOurInPoint = (newOffsetInOutPoint & InOutPoint.OutPoint) != 0 && (newIaInOutPoint & InOutPoint.InPoint) != 0;
                if (!sourceIsOurInPoint)
                    continue;
                
                // there may be other IAs that are ALSO this instructions inpoint, but, we can stop once we have found the first one.
                // we could do some other stuff here like set M/X flags on our offset based on where it jumped/branched FROM.
                // exercise for a later day.
                // var flag = Data.RomBytes[i].MFlag   ...or...   .XFLag
                // or could try:   Step(branch: true, offset: i);
                // or could try:   GetCpuStateFor(offset, i, etc) <-- this probably is best to copy MX flags/etc to here.

                return (offsetToTry, i);
            }
        }

        // didn't find any
        return (-1, -1);
    }

    // FIX ME: log and generation of dp opcodes. search references
    public int GetIntermediateAddress(int offset, bool resolve = false) => 
        GetCpu(offset).GetIntermediateAddress(this, offset, resolve);

    public string GetInstruction(int offset) => 
        GetCpu(offset).GetInstruction(this, offset);
    
    public void RescanInOutPoints()
    {
        for (var i = 0; i < GetRomSize(); i++)
            ClearInOutPoint(i);

        for (var i = 0; i < GetRomSize(); i++)
        {
            if (GetFlag(i) == FlagType.Opcode)
            {
                GetCpu(i).MarkInOutPoints(this, i);
            }
        }
    }
    
    public int FixMisalignedFlags()
    {
        int numChanged = 0, romSize = GetRomSize();

        for (var offset = 0; offset < romSize; offset++)
        {
            var flag = GetFlag(offset);

            switch (flag)
            {
                case FlagType.Opcode:
                {
                    var bytesChanged = 0;
                    var instructionLength = FixFlagsForOpcodeAndItsOperands(offset, romSize, ref bytesChanged);

                    numChanged += bytesChanged;
                    var newOffset = instructionLength - 1;
                    
                    offset += newOffset;
                    break;
                }
                case FlagType.Operand:
                    SetFlag(offset, FlagType.Opcode);
                    numChanged++;
                    offset--;
                    break;
                default:
                {
                    if (RomUtil.GetByteLengthForFlag(flag) > 1)
                    {
                        var step = RomUtil.GetByteLengthForFlag(flag);
                        for (var j = 1; j < step; j++)
                        {
                            if (GetFlag(offset + j) == flag) 
                                continue;
                            
                            SetFlag(offset + j, flag);
                            numChanged++;
                        }
                        offset += step - 1;
                    }

                    break;
                }
            }
        }

        return numChanged;
    }

    private int FixFlagsForOpcodeAndItsOperands(int offset, int romSize, ref int bytesChanged)
    {
        var instructionLength = GetInstructionLength(offset);
        for (var j = 1; j < instructionLength && offset + j < romSize; j++)
        {
            if (GetFlag(offset + j) == FlagType.Operand)
                continue;
                        
            SetFlag(offset + j, FlagType.Operand);
            bytesChanged++;
        }

        return instructionLength;
    }

    public byte? GetRomByte(int offset) => Data.GetRomByte(offset);
    public int? GetRomWord(int offset) => Data.GetRomWord(offset);
    public int? GetRomLong(int offset) => Data.GetRomLong(offset);
    public int? GetRomDoubleWord(int offset) => Data.GetRomDoubleWord(offset);
    public IReadOnlyLabelProvider Labels => Data.Labels;
    public RomMapMode RomMapMode
    {
        get => Data.RomMapMode;
        set => Data.RomMapMode = value;
    }

    public RomSpeed RomSpeed
    {
        get => Data.RomSpeed;
        set => Data.RomSpeed = value;
    }

    public void CacheVerificationInfoFor(ISnesCachedVerificationInfo verificationCache)
    {
        // Save a copy of these identifying ROM bytes with the project file itself, so they'll
        // be serialized to disk on project save. When we reload, we verify the recreated ROM data still matches both
        // of these. If either are wrong, then the ROM on disk could be different from the one associated with the 
        // project.

        // TODO: we need some way to abstract this SNES-specific stuff out of the project itself, while not breaking existing serialization
        // TODO: consider replacing this with the new IDataTag interface, it's perfect for this.
        verificationCache.InternalCheckSum = RomCheckSumsFromRomBytes;
        verificationCache.InternalRomGameName = CartridgeTitleName;
    }
}

public interface ISnesSampleProjectFactory : IProjectFactory
{
    
}

public class SnesSampleProjectFactory : ISnesSampleProjectFactory
{
    private readonly IProjectFactory createNewProject;
    public SnesSampleProjectFactory(IProjectFactory createNewProject)
    {
        this.createNewProject = createNewProject;
    }

    public IProject Create()
    {
        var project = createNewProject.Create() as Project; // TODO: don't cast, refactor to use IProject instead
        Debug.Assert(project?.Data != null);
        
        var snesData = project.Data.GetSnesApi();
        Debug.Assert(snesData != null);
        
        snesData.CacheVerificationInfoFor(project);
        
        return project;
    }
}

public static class SnesApiExtensions
{
    public static int MarkTypeFlag(this ISnesApi<IData> @this, int offset, FlagType type, int count) =>
        @this.Data.Mark(i => @this.SetFlag(i, type), offset, count);
    
    public static int MarkDataBank(this ISnesApi<IData> @this, int offset, int db, int count) =>
        @this.Data.Mark(i => @this.SetDataBank(i, db), offset, count);
    
    public static int MarkDirectPage(this ISnesApi<IData> @this, int offset, int dp, int count) => 
        @this.Data.Mark(i => @this.SetDirectPage(i, dp), offset, count);
    
    public static int MarkXFlag(this ISnesApi<IData> @this, int offset, bool x, int count) => 
        @this.Data.Mark(i => @this.SetXFlag(i, x), offset, count);
    
    public static int MarkMFlag(this ISnesApi<IData> @this, int offset, bool m, int count) => 
        @this.Data.Mark(i => @this.SetMFlag(i, m), offset, count);
    
    public static int MarkArchitecture(this ISnesApi<IData> @this, int offset, Architecture arch, int count) =>
        @this.Data.Mark(i => @this.Data.SetArchitecture(i, arch), offset, count);
    
    // input can be any length, and will be padded, using spaces, to the right size for SNES header
    public static void SetCartridgeTitle(this ISnesData @this, string utf8CartridgeTitle)
    {
        var rawShiftJisBytes = ByteUtil.GetRawShiftJisBytesFromStr(utf8CartridgeTitle);
        var paddedShiftJisBytes = ByteUtil.PadCartridgeTitleBytes(rawShiftJisBytes);

        // the BYTES need to be 21 in length. this is NOT the string length (which can be different because of multibyte chars)
        Debug.Assert(paddedShiftJisBytes.Length == RomUtil.LengthOfTitleName);

        @this.Data.RomBytes.SetBytesFrom(paddedShiftJisBytes, @this.CartridgeTitleStartingOffset);
    }
    
    public static (int directPage, int dataBank, bool xFlag, bool mFlag) GetCpuStateAt(this IRomByteFlagsGettable @this, int offset) {
        return (
            @this.GetDirectPage(offset), 
            @this.GetDataBank(offset), 
            @this.GetXFlag(offset), 
            @this.GetMFlag(offset)
        );
    }
    
    public static 
        (byte? opcode, int directPage, int dataBank, bool xFlag, bool mFlag) 
        GetCpuStateFor<TByteSource>(this TByteSource @this, int offset, int prevOffset) 
        where TByteSource : 
        IReadOnlyByteSource,
        IRomByteFlagsGettable
    {
        int directPage, dataBank;
        bool xFlag, mFlag;
        byte? opcode;

        void SetCpuStateFromCurrentOffset()
        {
            opcode = @this.GetRomByte(offset);
            (directPage, dataBank, xFlag, mFlag) = GetCpuStateAt(@this, offset);
        }

        void SetCpuStateFromPreviousOffset()
        {
            // go backwards from previous offset if it's valid but not an opcode
            while (prevOffset >= 0 && @this.GetFlag(prevOffset) == FlagType.Operand)
                prevOffset--;

            // if we didn't land on an opcode, forget it
            if (prevOffset < 0 || @this.GetFlag(prevOffset) != FlagType.Opcode) 
                return;
            
            // set these values to the PREVIOUS instruction
            (directPage, dataBank, xFlag, mFlag) = GetCpuStateAt(@this, prevOffset);
        }
        
        void SetMxFlagsFromRepSepAtOffset()
        {
            if (opcode != 0xC2 && opcode != 0xE2) // REP SEP 
                return;

            var operand = @this.GetRomByte(offset + 1);
            
            xFlag = (operand & 0x10) != 0 ? opcode == 0xE2 : xFlag;
            mFlag = (operand & 0x20) != 0 ? opcode == 0xE2 : mFlag;
        }
        
        SetCpuStateFromCurrentOffset();     // set from our current position first
        SetCpuStateFromPreviousOffset();    // if available, set from the previous offset instead.
        SetMxFlagsFromRepSepAtOffset();

        return (opcode, directPage, dataBank, xFlag, mFlag);
    }
    
    public static ISnesData? GetSnesApi(this IData @this) => 
        @this.GetApi<ISnesData>();
}

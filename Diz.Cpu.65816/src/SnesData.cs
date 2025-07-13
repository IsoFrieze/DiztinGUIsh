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

public interface IDataUtilities
{
    int FixMisalignedFlags();
    void NormalizeWramLabels();
}

public interface IMiscNavigable
{
    public (int unreachedOffsetFound, int iaSourceAddress) 
        FindNextUnreachedBranchPointAfter(
            int startingOffset, 
            bool searchForward = true, 
            bool includeUntakenBranchPoints = true, 
            bool requireUnreached = true
        );
    
    public int DetectNextPointerTableFromAddressingModeUsageAfter(
        int startingOffset, 
        bool searchForward = true
    );
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
    IDataUtilities,
    IArchitectureApi,
    IMarkOperandAndOpcode,
    ICommentTextProvider

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
                var romWord = Data.GetRomWord(offset);
                if (!romWord.HasValue)
                    return -1;
                
                // the original way: use what is written in the UI
                var bank = GetDataBank(offset); // allows overriding from the UI, but, you HAVE to set this correctly
                
                // but if it's zero (which can be a valid bank but usually won't be
                // then, autodetect the bank from the bank we're in.
                if (bank == 0)
                {
                    // new way, assumes the bank is the same as the location of the pointer
                    // more useful as long as it doesn't break anything
                    var snesAddrAtOffset = ConvertPCtoSnes(offset);
                    if (snesAddrAtOffset != -1)
                        bank = RomUtil.GetBankFromSnesAddress(snesAddrAtOffset);
                }

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

    // look for instructions using address modes that rely on pointer tables. identify any that may be incorrectly marked
    // in order to assist the user in manually marking them correctly.
    //
    // this is intended for instructions like:
    // JSR.W (#$81A224,X)       or JMP.W
    // where we want to mark the IA address [81A224] as a 16-bit pointer.
    // these are often really useful things to track down in disassembly, and otherwise a ton of manual work.
    // this is a nice shortcut to uncovering and marking these quickly.
    //
    // pointer tables we find will look like this:
    // PTR16_81A224:
    //  dw some_function_ptrtable_00
    //  dw some_function_ptrtable_02
    //  dw some_function_ptrtable_04
    //  ... etc ....
    //
    // return -1 if none found, or, ROM offset (PC) of possible pointer table located 
    public int DetectNextPointerTableFromAddressingModeUsageAfter(
        // where to start searching from
        int startingOffset, 
        
        // direction to search
        bool searchForward = true)
    {
        var snesData = Data.GetSnesApi();
        if (snesData == null)
            return -1;
        
        var direction = searchForward ? 1 : -1;
        
        for (
            var offsetToTry = startingOffset + direction;
            offsetToTry >= 0 && offsetToTry < Data.RomBytes.Count;
            offsetToTry += direction
        )
        {
            var opcodeRomByte = Data.RomBytes[offsetToTry];

            // we want to find only reached opcodes using certain addressing modes. skip anything not matching the criteria 
            if (opcodeRomByte.TypeFlag != FlagType.Opcode)
                continue;
            
            var addressMode = Cpu65C816<ISnesData>.GetAddressMode(snesData, offsetToTry);
            
            // note: feel free to add more address mode checks, if applicable.
            // this is the main one most pointer table stuff seems to want to use
            if (addressMode is not Cpu65C816Constants.AddressMode.AddressXIndexIndirect)
                continue;
            
            // note: we could also filter to certain instructions, if we want.  JSR, JMP are going to be the main ones we care about
            
            // OK, we are on an instruction that MIGHT be one that uses a pointer table.
            // let's keep narrowing it down:
            
            // does this instruction reference a valid IA? (Indirect address)
            var iaSnesAddress = snesData.GetIntermediateAddress(offsetToTry);
            if (iaSnesAddress == -1)
                continue;
            
            // note: this will rule out pointer tables that are in RAM
            // [which is OK because we're mostly marking ROM, but, could lead to false negatives if we're being thorough]
            var iaOffsetPc = snesData.ConvertSnesToPc(iaSnesAddress);
            if (iaOffsetPc == -1)
                continue;
            
            // let's have a look at the destination the IA refers to.
            // does the intermediate address match our criteria?
            var iaRomByte = Data.RomBytes[iaOffsetPc];
            
            // so ACTUALLY, this destination is already marked correctly, so we can skip it from our search
            // (remember: our goal is trying to locate INCORRECT or UNREACHED pointer tables)
            if (iaRomByte.TypeFlag == FlagType.Pointer16Bit)
                continue; // it's already good, SKIP IT
            
            // alright, we have a match! this is likely a pointer table but not marked as one.
            return iaOffsetPc;
        }

        // got to the end of the ROM and didn't find anything
        // this DOESN'T MEAN they don't exist, just, we couldn't detect anything
        return -1;
    }
    
    public (int unreachedOffsetFound, int iaSourceAddress) FindNextUnreachedBranchPointAfter(
        // where to start searching from
        int startingOffset, 
        
        // direction to search
        bool searchForward = true,
        
        // if true, return opcodes directly following a branch statement
        bool includeUntakenBranchPoints = true,
        
        // if true, require any result to be an unreached point. if false, it can be reached
        bool requireUnreached = true
    )
    {
        var direction = searchForward ? 1 : -1;
        for (
            var offsetToTry = startingOffset + direction; 
            offsetToTry >= 0 && offsetToTry < Data.RomBytes.Count; 
            offsetToTry += direction
        ) {
            var romByte = Data.RomBytes[offsetToTry];
            
            // usually: require it to be unreached, or most of the time we don't care about it.
            // (we're usually trying to quickly uncover new parts of the code we can step through that were missed before) 
            if (requireUnreached && romByte.TypeFlag != FlagType.Unreached)
                continue;
            
            // operation #1: is this an uncovered opcode directly after a branch or jump that returns to this point?
            if (includeUntakenBranchPoints)
            {
                // search backwards up to max of 4 bytes to find a previously marked opcode (if one exists and is already uncovered)
                for (var i = 1; i <= 4; i++)
                {
                    var searchOffset = offsetToTry - i;
                    
                    if (searchOffset < 0 && searchOffset >= Data.RomBytes.Count)
                        break; // out of bounds, bail
                    
                    var searchRomByte = Data.RomBytes[searchOffset];
                    
                    // if we fid something not fully uncovered yet, it's too risky, so bail
                    if (searchRomByte.TypeFlag == FlagType.Unreached) 
                        break;
                    
                    // we're looking for something already marked as an opcode
                    // if we hit operands, we want to keep searching backwards
                    if (searchRomByte.TypeFlag != FlagType.Opcode)
                        continue;
                    
                    // found our opcode. does it qualify as a conditional branch/subroutine call?
                    // this isn't going to be foolproof but it should catch 95% of the stuff we most care about
                    // we're ignoring: JMP JML BRA BRL (because they don't return to this point)
                    var opcode = searchRomByte.Rom;
                    var opcode_returns_after_jump = 
                        opcode == 0x10 || opcode == 0x30 || opcode == 0x50 || opcode == 0x70 ||     // BPL BMI BVC BVS
                        opcode == 0x90 || opcode == 0xB0 || opcode == 0xD0 || opcode == 0xF0 ||     // BCC BCS BNE BEQ
                        opcode == 0x20 || opcode == 0x22;   // JSR JSL

                    if (opcode_returns_after_jump)
                    {
                        // GOT IT! this is our answer, we're done.
                        return (offsetToTry, -1);
                    }
                    else
                    {
                        // we found an opcode searching backwards but, it's not a conditional branch / function call,
                        // which means we should stop this search right here and move on.
                        break;
                    }
                }
            }
            
            // operation #2: does jump to US?
            if ((romByte.Point & InOutPoint.InPoint) == 0)
                continue;

            // this is a legit in point, but, is the place it came FROM legit? return true if at least one legit
            // opcode from the origin of this point.
            // (warning: kinda expensive search, we have to scan EVERYTHING top to bottom)
            // consider some caching of references when we create in/out points to save this.
            
            // search entire ROM for already marked opcodes whose IA jumps land on US.
            // TODO: this search could later be extracted to form the basis of a "find references to X address" calculator
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

    public string GetInstructionStr(int offset) => 
        GetCpu(offset).GetInstructionStr(this, offset);

    public CpuInstructionDataFormatted GetInstructionData(int offset) =>
        GetCpu(offset).GetInstructionData(this, offset);
    
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

    public void NormalizeWramLabels()
    {
        var wramLabels = Labels.Labels
            .Where(x => RomUtil.GetWramAddressFromSnesAddress(x.Key) != -1)
            .ToList();
        
        foreach (var label in wramLabels)
        {
            var normalizedSnesAddress = RomUtil.GetSnesAddressFromWramAddress(RomUtil.GetWramAddressFromSnesAddress(label.Key));

            // already normalized? skip
            if (normalizedSnesAddress == label.Key)
                continue;

            // if there are duplicates or overlaps, we can't proceed, they must be manually cleaned up
            if (wramLabels.Any(x => x.Key == normalizedSnesAddress))
                continue;

            Data.Labels.RemoveLabel(label.Key);
            Data.Labels.AddLabel(normalizedSnesAddress, label.Value, true);
        }
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
    public string GetCommentText(int snesAddress) => Data.GetCommentText(snesAddress);
    public string? GetComment(int snesAddress) => Data.GetComment(snesAddress);
    
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
    public static int MarkTypeFlag(this ISnesApi<IData> @this, int offset, FlagType type, int count)
    {
        return @this.Data.Mark(MarkAction, offset, count);

        void MarkAction(int i)
        {
            // doing ONLY this is 100% fine, and is the original behavior
            @this.SetFlag(i, type);
            
            // but also.... 
            // if we're marking pointers, it also helps give the LogWriter more hints if
            // we reset the databank to the bank this byte is in.
            // this matters most for 16-bit but doesn't hurt for other types.
            // note: this is not always the best choice but, I think it's a good default.
            // feel free to modify to suit your preferences
            if (type is FlagType.Pointer16Bit or FlagType.Pointer24Bit or FlagType.Pointer32Bit)
            {
                var snesAddress = @this.ConvertPCtoSnes(i);
                if (snesAddress != -1)
                    @this.SetDataBank(i, RomUtil.GetBankFromSnesAddress(snesAddress));
            }
        }
    }

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

    public static (int found, string outputTextLog) GenerateMisalignmentReport(this ISnesApi<IData> @this)
    {
        // note: maybe this can be combined with FixMisalignedFlags() ? 
        
        var outputTextLog = "";
        int numMisalignedFound = 0, offset = 0;

        while (numMisalignedFound < 500 && offset < @this.GetRomSize())
        {
            FlagType flag = @this.GetFlag(offset), check = flag == FlagType.Opcode ? FlagType.Operand : flag;
            var step = flag == FlagType.Opcode
                ? @this.GetInstructionLength(offset)
                : RomUtil.GetByteLengthForFlag(flag);

            var snesAddress = @this.ConvertPCtoSnes(offset);
            
            if (flag == FlagType.Operand)
            {
                numMisalignedFound++;
                outputTextLog +=
                    $"{Util.NumberToBaseString(snesAddress, Util.NumberBase.Hexadecimal, 6, true)} " +
                    $"(0x{Util.NumberToBaseString(offset, Util.NumberBase.Hexadecimal, 0)}): Operand without Opcode\r\n";
            }
            else if (step > 1)
            {
                for (var i = 1; i < step; i++)
                {
                    if (@this.GetFlag(offset + i) == check) 
                        continue;
                    
                    numMisalignedFound++;
                    var expected = Util.GetEnumDescription(check);
                    var actual = Util.GetEnumDescription(@this.GetFlag(offset + i));
                    
                    outputTextLog += $"{Util.NumberToBaseString(snesAddress, Util.NumberBase.Hexadecimal, 6, true)} " +
                            $"(0x{Util.NumberToBaseString(offset + i, Util.NumberBase.Hexadecimal, 0)}): " +
                            $"{actual} is not {expected}\r\n";
                }
            }

            offset += step;
        }

        if (numMisalignedFound == 0)
            outputTextLog = "No misaligned flags found!";

        return (numMisalignedFound, outputTextLog);
    }
}

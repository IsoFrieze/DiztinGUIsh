using System.Diagnostics;
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

public interface ISnesApi<out TData> :
    IRomByteFlagsGettable, 
    IRomByteFlagsSettable, 
    ISnesAddressConverter, 
    ISteppable,
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
    IArchitectureApi

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

    public uint RomComplement => (uint) Data.GetRomWord(RomComplementOffset);
    public uint RomChecksum => (uint) Data.GetRomWord(RomChecksumOffset);
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
    
    public int Step(int offset, bool branch, bool force, int prevOffset) =>
        GetCpu(offset).Step(this, offset, branch, force, prevOffset);

    public int AutoStepSafe(int offset) =>
        GetCpu(offset).AutoStepSafe(this, offset);

    public int AutoStepHarsh(int offset, int count) =>
        GetCpu(offset).AutoStepHarsh(this, offset, count);

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

        // we need some way to abstract this out of the project itself.
        verificationCache.InternalCheckSum = RomCheckSumsFromRomBytes;
        verificationCache.InternalRomGameName = CartridgeTitleName;
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
    
    public static ISnesData? GetSnesApi(this IData @this) => 
        @this.GetApi<ISnesData>();
}

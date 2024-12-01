using System.Diagnostics;
using Diz.Core.util;

namespace Diz.Import.bsnes.tracelog;

public partial class BsnesTraceLogImporter
{
    private CachedTraceLineTextIndex TextImportFormatCached { get; } = new();
    
    public ModificationData AllocateModificationData()
    {
        var modData = modificationDataPool.Get();
            
        modData.changed = false;
            
        return modData;
    }

    private void FreeModificationData(ref ModificationData? modData)
    {
        if (modData == null)
            return;

        modificationDataPool.Return(ref modData);
    }
        
    public void ImportTraceLogLine(string line)
    {
        var modData = AllocateModificationData();
        if (!ParseTextLine(line, modData))
            return;

        UpdatePCAddress(modData);
        ConsumeAndFreeTraceData(ref modData); // note: frees modData, sets it to null. can't use after this call
    }
        
    // PERFORMANCE CRITICAL FUNCTION
    // WARNING: THREAD SAFETY: this function will be called from multiple threads concurrently and MUST REMAIN thread-safe.
    // and, for performance-reasons, we're handling our own locking.
    public void ImportTraceLogLineBinary(byte[] bytes, bool abridgedFormat, BsnesTraceLogCaptureController.TraceLogCaptureSettings settings)
    {
        var modData = AllocateModificationData();
        
        // first, make a copy of all settings.
        modData.CaptureSettings = settings;
        
        // then parse the BSNES data for this opcode+operands, and process it
        ParseBinary(bytes, abridgedFormat, out var opcodeLen, modData);
        UpdatePCAddress(modData);
        
        // note: frees modData and returns to the pool. don't use modData after this call.
        ConsumeAndFreeTraceData(ref modData, opcodeLen);
    }

    public bool ParseTextLine(string line, ModificationData modData)
    {
        // caution: very performance-sensitive function, please take care when making modifications
        // string.IndexOf() is super-slow too.
        // Input lines must follow this strict format and be this exact formatting and column indices.
        // 028cde rep #$30               A:0004 X:0000 Y:0004 S:1fdd D:0000 DB:02 nvmxdiZC V:133 H: 654 F:36
        if (line.Length < 80)
            return false;
            
        // performance: we could just parse the whitespace, but, 
        // tracelogs have a huge amount of lines. so we parse the first line,
        // then save the offsets for all other lines in the same file.
        if (TextImportFormatCached.LastLineLength != line.Length)
            TextImportFormatCached.RecomputeCachedIndicesBasedOn(line);

        // TODO: add error treatment / validation here.

        modData.SnesAddress = (int)ByteUtil.ByteParseHex(line, 0, 6);
        modData.DirectPage = (int)ByteUtil.ByteParseHex(line, TextImportFormatCached.D, 4);
        modData.DataBank = (int)ByteUtil.ByteParseHex(line, TextImportFormatCached.Db, 2);
            
        // 'X' (if emulation mode) or 'B' (if native mode) = unchecked in bsnesplus debugger UI = (8bit)
        // 'x' or '.' = checked (16bit)
        modData.XFlagSet = line[TextImportFormatCached.FX] == 'X' || line[TextImportFormatCached.FX] == 'B';

        // 'M' (if emulation mode) or '1' (if native mode) = unchecked in bsnesplus debugger UI = (8bit)
        // 'm' or '.' = checked (16bit)
        modData.MFlagSet = line[TextImportFormatCached.FM] == 'M' || line[TextImportFormatCached.FM] == '1';
            
        // TODO: we could capture native vs emulation mode here and mark that.

        return true;
    }

    private static void ParseBinary(byte[] bytes, bool abridgedFormat, out byte opcodeLen, ModificationData modData)
    {
        // file format info from the BSNES side:
        // https://github.com/binary1230/bsnes-plus/blob/e30dfc784f3c40c0db0a09124db4ec83189c575c/bsnes/snes/cpu/core/disassembler/disassembler.cpp#L224
            
        // extremely performance-intensive function. be really careful when adding stuff
        if (abridgedFormat)
        {
            if (bytes.Length != 8)
                throw new InvalidDataException("Non-abridged trace data length must be 8 bytes");
        }
        else
        {
            if (bytes.Length != 21)
                throw new InvalidDataException("Non-abridged trace data length must be 21 bytes");
        }

        var currentIndex = 0;

        // -----------------------------
        modData.SnesAddress = ByteUtil.ByteArrayToInt24(bytes, currentIndex);
        currentIndex += 3;

        opcodeLen = bytes[currentIndex++];

        modData.DirectPage = ByteUtil.ByteArrayToInt16(bytes, currentIndex);
        currentIndex += 2;

        modData.DataBank = bytes[currentIndex++];

        // the flags register
        var flags = bytes[currentIndex++];
        // n = flags & 0x80;
        // v = flags & 0x40;
        // m = flags & 0x20;
        // d = flags & 0x08;
        // i = flags & 0x04;
        // z = flags & 0x02;
        // c = flags & 0x01;

        // we only care about X and M flags
        modData.XFlagSet = (flags & 0x10) != 0;
        modData.MFlagSet = (flags & 0x20) != 0;

        if (!abridgedFormat)
        {
            // skip opcodes. NOTE: must read all 5 bytes but only use up to 'opcode_len' bytes 
            //var op  = bytes[currentIndex++];
            //var op0 = bytes[currentIndex++];
            //var op1 = bytes[currentIndex++];
            //var op2 = bytes[currentIndex++];
            currentIndex += 4;

            // skip A register
            currentIndex += 2;

            // skip X register
            currentIndex += 2;

            // skip Y register
            currentIndex += 2;

            // skip S register
            currentIndex += 2;

            // skip, flag 'e' for emulation mode or not
            // skip E(emu) flag <-- NOTE: we might... want this someday.
            currentIndex += 1;
        }

        Debug.Assert(currentIndex == bytes.Length);
    }
}
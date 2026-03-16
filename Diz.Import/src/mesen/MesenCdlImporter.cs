using Diz.Core.Interfaces;
using Diz.Cpu._65816;

namespace Diz.Import.mesen;

public static class MesenCdlImporter
{
    // based on https://github.com/SourMesen/Mesen2/blob/master/Core/Debugger/CodeDataLogger.cpp

    [Flags]
    private enum Flag : byte
    {
        None = 0x00,
        Code = 0x01,
        Data = 0x02,
        JumpTarget = 0x04,
        SubEntryPoint = 0x08,
    }

    public static void Import(string filename, ISnesData data)
    {
        var cdlData = LoadFromFile(filename, data.GetRomSize());
        CopyInto(cdlData, data);
    }

    private static byte[] LoadFromFile(string path, int expectedRomSize)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        return LoadFromStream(fs, expectedRomSize);
    }

    private static byte[] LoadFromStream(Stream input, int expectedRomSize)
    {
        var memSize = (uint)expectedRomSize;
        var cdlData = new byte[memSize];

        var br = new BinaryReader(input);
        var fileSize = input.Length;

        if (fileSize < 5) {
            throw new InvalidDataException("File is too small to be a Mesen2 CDL file.");
        }

        var header = br.ReadBytes(5);
        var hasHeader = header.SequenceEqual("CDLv2"u8.ToArray());

        if (hasHeader)
        {
            if (fileSize < 9 + memSize)
                throw new InvalidDataException($"Mesen2 CDL file is too small for expected ROM size. Expected at least {9 + memSize} bytes, got {fileSize}.");

            // Skip CRC32 for now as we don't have the ROM CRC32 here and the user said "parsing is most important"
            // and we don't care about auto-reset logic.
            br.BaseStream.Seek(9, SeekOrigin.Begin);
            var read = br.Read(cdlData, 0, (int)memSize);
            if (read < memSize)
                throw new InvalidDataException("Could not read full CDL data from file.");
        }
        else
        {
            // Older CRC-less CDL file or just raw CDL
            if (fileSize < memSize)
                throw new InvalidDataException($"CDL file is too small for expected ROM size. Expected {memSize} bytes, got {fileSize}.");

            br.BaseStream.Seek(0, SeekOrigin.Begin);
            var read = br.Read(cdlData, 0, (int)memSize);
            if (read < memSize)
            {
                throw new InvalidDataException("Could not read full CDL data from file.");
            }
        }

        return cdlData;
    }

    private static void CopyInto(byte[] cdlData, ISnesData snesData)
    {
        var size = Math.Min(cdlData.Length, snesData.GetRomSize());

        for (var cdlOffset = 0; cdlOffset < size; cdlOffset++)
        {
            var cdlFlag = (Flag)cdlData[cdlOffset];
            ProcessCdlFlagsAtCdlOffset(snesData, cdlFlag, cdlOffset);
        }
    }

    private static void ProcessCdlFlagsAtCdlOffset(ISnesData snesData, Flag cdlFlag, int offset)
    {
        if (cdlFlag == Flag.None)
            return;
        
        // skip anything already marked as operand:
        if (snesData.GetFlag(offset) == FlagType.Operand)
            return;
        
        // NES doesn't use these, they're always 8-bit
        snesData.MarkMFlag(offset, true, 1);
        snesData.MarkXFlag(offset, true, 1);
        // maybe in the future: snesData.MarkArchitecture(NES_6502)

        // NOTE: Mesen marks "Code" as both Code|Data
        // process code FIRST, and only mark it as data if NOT marked as code.
        if ((cdlFlag & Flag.Code) != 0)
        {
            // NOTE: this isn't quite the full picture:
            //  Mesen2 marks every byte executed as "CODE" but doesn't give us info on what's an opcode vs operand (which Diz needs or things look weird)
            //  we are going to need to divine this information.
            //  first: we're going to mark the first byte here as opcode

            // NOTE: this will usually mark the next couple bytes as operands.
            // when the CDL advances, it'll see the data already marked and SKIP making changes til we get to the next unreached code.
            // There could be edge cases where this doesn't work, but, it's good enough for now.
            
            snesData.MarkTypeFlag(offset, FlagType.Opcode, 1);

            // then, we're going to mark the rest of the bytes as operands by doing a step:
            snesData.Step(offset, false, false, offset - 1);
        } 
        else if ((cdlFlag & Flag.Data) != 0)
        {
            // 1. set "read point" meaning this data was read by something live
            // do this for anything that's not code which was actually visited in a CDL:
            if (snesData.GetFlag(offset) is not FlagType.Opcode and not FlagType.Operand) {
                // set this byte as "visited"
                snesData.SetInOutPoint(offset, InOutPoint.ReadPoint);
            }
            
            // 2. only going to let things be marked as data if not already marked as something else.
            // especially NEVER let this mark previously marked code as data (or any more specialized stuff like pointer tables, text, etc)
            if (snesData.GetFlag(offset) != FlagType.Unreached)
                return;
            
            snesData.MarkTypeFlag(offset, FlagType.Data8Bit, 1);
        }
    }
}
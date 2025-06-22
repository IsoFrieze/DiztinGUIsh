using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Cpu._65816;

public class Cpu65C816<TByteSource> : Cpu<TByteSource> 
    where TByteSource : 
    IRomByteFlagsGettable, 
    IRomByteFlagsSettable, 
    ISnesAddressConverter, 
    ISteppable, 
    IReadOnlyByteSource, 
    ISnesIntermediateAddress,
    IInOutPointSettable,
    IInOutPointGettable,
    IReadOnlyLabels
{
    public override int Step(TByteSource data, int offset, bool branch, bool force, int prevOffset = -1)
    {
        var (opcode, directPage, dataBank, xFlag, mFlag) = data.GetCpuStateFor(offset, prevOffset);
        var length = MarkAsOpcodeAndOperandsStartingAt(data, offset, dataBank, directPage, xFlag, mFlag);
        MarkInOutPoints(data, offset);

        var nextOffset = offset + length;

        var useIndirectAddress = 
            opcode is 0x4C or 0x5C or 0x80 or 0x82 || 
            branch && opcode is 0x10 or 0x30 or 0x50 or 0x70 or 0x90 or 0xB0 or 0xD0 or 0xF0 or 0x20 or 0x22;

        if (force || !useIndirectAddress) 
            return nextOffset;
            
        var iaNextOffsetPc = data.ConvertSnesToPc(GetIntermediateAddress(data, offset, true));
        if (iaNextOffsetPc >= 0)
            return iaNextOffsetPc;

        return nextOffset;
    }
    
    public override int CalculateInOutPointsFromOffset(
        TByteSource data,
        int offset,
        out InOutPoint newIaInOutPoint,
        out InOutPoint newOffsetInOutPoint
    )
    {
        // calculate these from scratch (don't rely on existing in-out data)
        newIaInOutPoint = InOutPoint.None;
        newOffsetInOutPoint = InOutPoint.None;

        var opcode = data.GetRomByte(offset);
        if (opcode == null)
            return -1;

        var intermediateAddress = data.GetIntermediateAddress(offset, true);
        var iaOffsetPc = data.ConvertSnesToPc(intermediateAddress);

        // set read point on EA
        if (iaOffsetPc >= 0 && ( // these are all read/write/math instructions
                ((opcode & 0x04) != 0) || ((opcode & 0x0F) == 0x01) || ((opcode & 0x0F) == 0x03) ||
                ((opcode & 0x1F) == 0x12) || ((opcode & 0x1F) == 0x19)) &&
            (opcode != 0x45) && (opcode != 0x55) && (opcode != 0xF5) && (opcode != 0x4C) &&
            (opcode != 0x5C) && (opcode != 0x6C) && (opcode != 0x7C) && (opcode != 0xDC) && (opcode != 0xFC)
           )
        {
            newIaInOutPoint |= InOutPoint.ReadPoint;
        }

        // set end point on offset
        if (opcode == 0x40 || opcode == 0x4C || opcode == 0x5C || opcode == 0x60 // RTI JMP JML RTS
            || opcode == 0x6B || opcode == 0x6C || opcode == 0x7C || opcode == 0x80 // RTL JMP JMP BRA
            || opcode == 0x82 || opcode == 0xDB || opcode == 0xDC // BRL STP JML
           )
        {
            newOffsetInOutPoint |= InOutPoint.EndPoint;
        }

        // set out point on offset
        // set in point on EA
        if (iaOffsetPc >= 0 && (
                opcode == 0x4C || opcode == 0x5C || opcode == 0x80 || opcode == 0x82 // JMP JML BRA BRL
                || opcode == 0x10 || opcode == 0x30 || opcode == 0x50 || opcode == 0x70 // BPL BMI BVC BVS
                || opcode == 0x90 || opcode == 0xB0 || opcode == 0xD0 || opcode == 0xF0 // BCC BCS BNE BEQ
                || opcode == 0x20 || opcode == 0x22)) // JSR JSL
        {
            newOffsetInOutPoint |= InOutPoint.OutPoint;
            newIaInOutPoint |= InOutPoint.InPoint;
        }

        return iaOffsetPc;
    }

    public int MarkAsOpcodeAndOperandsStartingAt(
        TByteSource data, int offsetToMarkAsOpcode, 
        int? dataBank = null, int? directPage = null, 
        bool? xFlag = null, bool? mFlag = null              // you pretty much always want to set the MX flags or this function is worthless
        )
    {
        var numBytesToChangeForOpcodesAndOperands = 1;
        var i = 0;
        var markedAs = FlagType.Opcode;
        do
        {
            var currentOffset = offsetToMarkAsOpcode + i;

            if (dataBank != null)
                data.SetDataBank(currentOffset, dataBank.Value);
            
            if (directPage != null)
                data.SetDirectPage(currentOffset, directPage.Value);
            
            if (xFlag != null)
                data.SetXFlag(currentOffset, xFlag.Value);
            
            if (mFlag != null)
                data.SetMFlag(currentOffset, mFlag.Value);
            
            data.SetFlag(currentOffset, markedAs);
            
            if (markedAs == FlagType.Opcode)
            {
                // call GetInstructionLength() only AFTER setting all the other flags above,
                // because the data for the instruction will CHANGE based on those flags.
                // we want to get that, and apply it to the next couple operands
                numBytesToChangeForOpcodesAndOperands = GetInstructionLength(data, offsetToMarkAsOpcode);
            }

            markedAs = FlagType.Operand;
            ++i;
        } while (i < numBytesToChangeForOpcodesAndOperands);

        return numBytesToChangeForOpcodesAndOperands;
    }

    // input: ROM offset
    // return: a SNES address
    public override int GetIntermediateAddress(TByteSource data, int offset, bool resolve)
    {
        int bank;
        int programCounter;
            
        #if !DIZ_3_BRANCH
        // old way
        var opcode = data.GetRomByte(offset);
        #else
            // new way
            var byteEntry = GetByteEntryRom(data, offset);
            var opcode = byteEntry?.Byte;
        #endif
            
        if (opcode == null)
            return -1;

        var mode = GetAddressMode(data, offset);
        switch (mode)
        {
            case Cpu65C816Constants.AddressMode.DirectPage:
            case Cpu65C816Constants.AddressMode.DirectPageXIndex:
            case Cpu65C816Constants.AddressMode.DirectPageYIndex:
            case Cpu65C816Constants.AddressMode.DirectPageIndirect:
            case Cpu65C816Constants.AddressMode.DirectPageXIndexIndirect:
            case Cpu65C816Constants.AddressMode.DirectPageIndirectYIndex:
            case Cpu65C816Constants.AddressMode.DirectPageLongIndirect:
            case Cpu65C816Constants.AddressMode.DirectPageLongIndirectYIndex:
                if (resolve)
                {
                    var directPage = data.GetDirectPage(offset);
                    var operand = data.GetRomByte(offset + 1);
                    if (!operand.HasValue)
                        return -1;
                    return (directPage + (int)operand) & 0xFFFF;
                }
                else
                {
                    goto case Cpu65C816Constants.AddressMode.DirectPageSIndex;
                }
            case Cpu65C816Constants.AddressMode.DirectPageSIndex:
            case Cpu65C816Constants.AddressMode.DirectPageSIndexIndirectYIndex:
                return data.GetRomByte(offset + 1) ?? -1;
            case Cpu65C816Constants.AddressMode.Address:
            case Cpu65C816Constants.AddressMode.AddressXIndex:
            case Cpu65C816Constants.AddressMode.AddressYIndex:
            case Cpu65C816Constants.AddressMode.AddressXIndexIndirect:
            {
                bank = opcode is 0x20 or 0x4C or 0x7C or 0xFC
                    ? data.ConvertPCtoSnes(offset) >> 16
                    : data.GetDataBank(offset);
                var operand = data.GetRomWord(offset + 1);
                if (!operand.HasValue)
                    return -1;
                    
                return (bank << 16) | (int)operand;
            }
            case Cpu65C816Constants.AddressMode.AddressIndirect:
            case Cpu65C816Constants.AddressMode.AddressLongIndirect:
            {
                var operand = data.GetRomWord(offset + 1) ?? -1;
                return operand;
            }
            case Cpu65C816Constants.AddressMode.Long:
            case Cpu65C816Constants.AddressMode.LongXIndex:
            {
                var operand = data.GetRomLong(offset + 1) ?? -1;
                return operand;
            }
            case Cpu65C816Constants.AddressMode.Relative8:
            {
                programCounter = data.ConvertPCtoSnes(offset + 2);
                bank = programCounter >> 16;
                var romByte = data.GetRomByte(offset + 1);
                if (!romByte.HasValue)
                    return -1;
                    
                return (bank << 16) | ((programCounter + (sbyte)romByte) & 0xFFFF);
            }
            case Cpu65C816Constants.AddressMode.Relative16:
            {
                // something may be wrong here with the "PER" instruction (opcode 0x62).
                // description in https://github.com/IsoFrieze/DiztinGUIsh/issues/102
                // must fix
                
                programCounter = data.ConvertPCtoSnes(offset + 3);
                bank = programCounter >> 16;
                var romByte = data.GetRomWord(offset + 1);
                if (!romByte.HasValue)
                    return -1;
                    
                return (bank << 16) | ((programCounter + (short)romByte) & 0xFFFF);
            }
        }
        return -1;
    }

    public override string GetInstruction(TByteSource data, int offset)
    {
        var mode = GetAddressMode(data, offset);
        if (mode == null)
            throw new InvalidDataException("Expected non-null mode");
            
        var format = GetInstructionFormatString(data, offset);
        var mnemonic = GetMnemonic(data, offset);
            
        int numDigits1 = 0, numDigits2 = 0;
        int? value1 = null, value2 = null;
        var identified = false;
            
        switch (mode)
        {
            case Cpu65C816Constants.AddressMode.BlockMove:
                identified = true;
                numDigits1 = numDigits2 = 2;
                value1 = data.GetRomByte(offset + 1);
                value2 = data.GetRomByte(offset + 2);
                break;
            case Cpu65C816Constants.AddressMode.Constant8:
            case Cpu65C816Constants.AddressMode.Immediate8:
                identified = true;
                numDigits1 = 2;
                value1 = data.GetRomByte(offset + 1);
                break;
            case Cpu65C816Constants.AddressMode.Immediate16:
                identified = true;
                numDigits1 = 4;
                value1 = data.GetRomWord(offset + 1);
                break;
        }

        string op1, op2 = "";
        if (identified)
        {
            op1 = CreateHexStr(value1, numDigits1);
            op2 = CreateHexStr(value2, numDigits2);
        }
        else
        {
            // dom note: this is where we could inject expressions if needed. it gives stuff like "$F001".
            // we could substitute our expression of "$#F000 + $#01" or "some_struct.member" like "player.hp"
            // the expression must be verified to always match the bytes in the file [unless we allow overriding]
            op1 = FormatOperandAddress(data, offset, mode.Value);
        }
            
        return string.Format(format, mnemonic, op1, op2);
    }

    public override int AutoStepSafe(TByteSource byteSource, int offset)
    {
        var cmd = new AutoStepper65816<TByteSource>(byteSource);
        cmd.Run(offset);
        return cmd.Offset;
    }

    private static string CreateHexStr(int? v, int numDigits)
    {
        if (numDigits == 0)
            return "";

        if (v == null)
            throw new InvalidDataException("Expected non-null input value, got null");
            
        return Util.NumberToBaseString((int) v, Util.NumberBase.Hexadecimal, numDigits, true);
    }

    public override int GetInstructionLength(TByteSource data, int offset)
    {
        var mode = GetAddressMode(data, offset);
            
        // not sure if this is the right thing. probably fine. if we hit this, we're in a weird mess anyway.
        return mode == null ? 1 : GetInstructionLength(mode.Value);
    }

    // Find, and append, in/out points to any that current exist at this offset and its IA address
    public override void MarkInOutPoints(TByteSource data, int offset)
    {
        var iaOffsetPc = CalculateInOutPointsFromOffset(data, offset, out var newIaInOutPoint, out var newOffsetInOutPoint);

        // these will append the in/out points to existing data that's already there
        data.SetInOutPoint(offset, newOffsetInOutPoint);
        if (iaOffsetPc >= 0)
            data.SetInOutPoint(iaOffsetPc, newIaInOutPoint);
    }

    private static int GetInstructionLength(Cpu65C816Constants.AddressMode mode)
    {
        switch (mode)
        {
            case Cpu65C816Constants.AddressMode.Implied:
            case Cpu65C816Constants.AddressMode.Accumulator:
                return 1;
            case Cpu65C816Constants.AddressMode.Constant8:
            case Cpu65C816Constants.AddressMode.Immediate8:
            case Cpu65C816Constants.AddressMode.DirectPage:
            case Cpu65C816Constants.AddressMode.DirectPageXIndex:
            case Cpu65C816Constants.AddressMode.DirectPageYIndex:
            case Cpu65C816Constants.AddressMode.DirectPageSIndex:
            case Cpu65C816Constants.AddressMode.DirectPageIndirect:
            case Cpu65C816Constants.AddressMode.DirectPageXIndexIndirect:
            case Cpu65C816Constants.AddressMode.DirectPageIndirectYIndex:
            case Cpu65C816Constants.AddressMode.DirectPageSIndexIndirectYIndex:
            case Cpu65C816Constants.AddressMode.DirectPageLongIndirect:
            case Cpu65C816Constants.AddressMode.DirectPageLongIndirectYIndex:
            case Cpu65C816Constants.AddressMode.Relative8:
                return 2;
            case Cpu65C816Constants.AddressMode.Immediate16:
            case Cpu65C816Constants.AddressMode.Address:
            case Cpu65C816Constants.AddressMode.AddressXIndex:
            case Cpu65C816Constants.AddressMode.AddressYIndex:
            case Cpu65C816Constants.AddressMode.AddressIndirect:
            case Cpu65C816Constants.AddressMode.AddressXIndexIndirect:
            case Cpu65C816Constants.AddressMode.AddressLongIndirect:
            case Cpu65C816Constants.AddressMode.BlockMove:
            case Cpu65C816Constants.AddressMode.Relative16:
                return 3;
            case Cpu65C816Constants.AddressMode.Long:
            case Cpu65C816Constants.AddressMode.LongXIndex:
                return 4;
            default:
                return 1;
        }
    }

    private const bool AttemptTouseDirectPageArithmeticInFinalOutput = true;

    // this can print bytes OR labels. it can also deal with SOME mirroring and Direct Page addressing etc/etc
    private string FormatOperandAddress(TByteSource data, int offset, Cpu65C816Constants.AddressMode mode)
    {
        var intermediateAddress = data.GetIntermediateAddress(offset);
        if (intermediateAddress < 0)
            return "";

        if (data is IReadOnlyLabels labelProvider)
        {
            // is there a label for this absolute address? if so, lets use that
            var labelName = labelProvider.Labels.GetLabelName(intermediateAddress);
            if (labelName != "")
            {
                // couple of special cases.
                // we won't return +/- temp labels if this isn't an opcode we want to use with branching.
                if (!RomUtil.IsValidPlusMinusLabel(labelName)) // "+". "-", "++", "--", etc
                    return labelName; // not a local label, OK to use 
                
                // this is a +/- local label, so, do some extra checks...
                var opcode = data.GetRomByte(offset); // advance the offset to the next byte
                var opcodeIsBranch = opcode == 0x80 || // BRA
                                     opcode == 0x10 || opcode == 0x30 || opcode == 0x50 ||
                                     opcode == 0x70 || // BPL BMI BVC BVS
                                     opcode == 0x90 || opcode == 0xB0 || opcode == 0xD0 ||
                                     opcode == 0xF0; // BCC BCS BNE BEQ
                // NOT going to do this for any JUMPs like JMP, JML, and also not BRL

                // only allow us to use the local label if we're a branch
                if (opcodeIsBranch)
                    return labelName;
            }

            // otherwise...
            
            // TODO: extract some of this label mirroring logic into its own function so other stuff can call it
            
            // OPTIONAL:
            // super-specific variable substitution.  this needs to be heavily generalized.
            // this MAY NOT COVER EVERY EDGE CASE. feel free to modify or disable it.
            // this is to try and get more labels in the output by creating a mathematical expression
            // for ASAR to use. only works if you have accurate 'D' register (direct page) set.
            // usually only useful after you've done a lot of tracelog capture.
            if (AttemptTouseDirectPageArithmeticInFinalOutput && mode
                    is Cpu65C816Constants.AddressMode.DirectPage
                    or Cpu65C816Constants.AddressMode.DirectPageXIndex
                    or Cpu65C816Constants.AddressMode.DirectPageYIndex
                    or Cpu65C816Constants.AddressMode.DirectPageIndirect
                    or Cpu65C816Constants.AddressMode.DirectPageXIndexIndirect
                    or Cpu65C816Constants.AddressMode.DirectPageIndirectYIndex
                    or Cpu65C816Constants.AddressMode.DirectPageLongIndirect
                    or Cpu65C816Constants.AddressMode.DirectPageLongIndirectYIndex)
            {
                var dp = data.GetDirectPage(offset);
                if (dp != 0)
                {
                    labelName = labelProvider.Labels.GetLabelName(dp + intermediateAddress);
                    if (labelName != "")
                    {
                        // direct page addressing. we can use an expression to use a variable name for this
                        // TODO: we can also use asar directive .dbase to set the assumed DB register.
                        return $"{labelName}-${dp:X}"; // IMPORTANT: no spaces on that minus sign.
                    }
                }
            }
            
            // OPTIONAL 2: try some crazy hackery to deal with mirroring on RAM labels.
            // (this is super-hacky, we need to do this better)
            // also, can the DP address above ALSO interact with this? (probably, right?) if so, we need to keep that in mind
            var (labelAddressFound, labelEntryFound) = SearchForMirroredLabel(data, intermediateAddress);
            if (labelEntryFound != null)
                return $"{labelEntryFound.Name}";
        }

        var count = BytesToShow(mode);
        if (mode is Cpu65C816Constants.AddressMode.Relative8 or Cpu65C816Constants.AddressMode.Relative16)
        {
            var romWord = data.GetRomWord(offset + 1);
            if (!romWord.HasValue)
                return "";
                
            intermediateAddress = (int)romWord;
        }
            
        intermediateAddress &= ~(-1 << (8 * count));
        return Util.NumberToBaseString(intermediateAddress, Util.NumberBase.Hexadecimal, 2 * count, true);
    }

    private (int labelAddress, IAnnotationLabel? labelEntry) SearchForMirroredLabel(TByteSource data, int snesAddress)
    {
        // WARNING: this is an EXTREMELY wasteful and very inefficient search. cache wram addresses in labels if needed for perf
        foreach (var (labelAddress, labelEntry) in data.Labels.Labels)
        {
            if (!AreLabelsSameMirror(snesAddress, labelAddress)) 
                continue;

            // we found a label that's in WRAM and matches the same WRAM address as our IA.
            // that means, we found a mirrored address we could match here. let's do so now.
            //
            // NOTE: this may not cover every case correctly. Whatever we put here needs to assemble
            // down to the original bytes in the ROM.
            // we CAN use expressions/etc to make this work if we want.
            // the point is so humans can see the labels, but the output assembly can be the original bytes
            // TODO: might need to limit this to cases where there are 2 bytes in the IA only.
            return (labelAddress, labelEntry);
        }
        
        return (-1, null);
    }

    private static bool AreLabelsSameMirror(int snesAddress, int labelAddress)
    {
        if (snesAddress == -1 || labelAddress == -1)
            return false;

        // early out shortcut 
        if ((snesAddress & 0xFFFF) != (labelAddress & 0xFFFF))
            return false;
        
        // this function is a crappy and probably error-prone way to do this. gotta start somewhere.
        // it would be better to do this by mapping out the memory regions than trying to go backwards
        // from any arbitrary SNES address back to the mapped region. still, we'll give it a shot.
        // we MOST care about things affecting labels that humans care about: WRAM mirrors and SNES registers
        
        // check WRAM mirroring
        if (AreSnesAddressesSameMirroredWramAddresses(snesAddress, labelAddress))
            return true;

        // check other IO mirroring (overlaps with above for LowRAM too, but that's OK)
        if (AreSnesAddressesSameMirroredIoRegion(snesAddress, labelAddress)) 
            return true;

        return false;
    }

    private static bool AreSnesAddressesSameMirroredIoRegion(int snesAddress1, int snesAddress2)
    {
        var reducedSnesAddr1 = GetUnmirroredIoRegionFromBank(snesAddress1);
        var reducedSnesAddr2 = GetUnmirroredIoRegionFromBank(snesAddress2);
        
        return reducedSnesAddr1 != -1 && reducedSnesAddr2 == reducedSnesAddr1;
    }

    private static int GetUnmirroredIoRegionFromBank(int snesAddress)
    {
        if (snesAddress == -1)
            return -1;
        
        // Mirrored WRAM range in banks $00-$3F and $80-$BF
        var bank = (snesAddress >> 16) & 0xFF; // Extract the high byte (bank number)

        // Check if the bank is within WRAM-mirroring ranges: $00-$3F or $80-$BF
        var bankContainsMirror = bank is (>= 0x00 and <= 0x3F) or (>= 0x80 and <= 0xBF);
        if (!bankContainsMirror) 
            return -1;
        
        // are we in the mirrored region?
        var low16Addr = snesAddress & 0xFFFF;
        if (low16Addr is < 0x0000 or > 0x7FFF) 
            return -1;

        return low16Addr;
    }

    private static bool AreSnesAddressesSameMirroredWramAddresses(int snesAddress1, int snesAddress2)
    {
        var wramAddress1 = RomUtil.GetWramAddress(snesAddress1);
        var wramAddress2 = RomUtil.GetWramAddress(snesAddress2);
        
        return wramAddress1 != -1 && wramAddress2 == wramAddress1;
    }

    private string GetMnemonic(TByteSource data, int offset, bool showHint = true)
    {
        var mn = Cpu65C816Constants.Mnemonics[data.GetRomByteUnsafe(offset)];
        if (!showHint) 
            return mn;

        var mode = GetAddressMode(data, offset);
        if (mode == null)
            return mn;
                
        var count = BytesToShow(mode.Value);

        if (mode is Cpu65C816Constants.AddressMode.Constant8 or Cpu65C816Constants.AddressMode.Relative16 or Cpu65C816Constants.AddressMode.Relative8) 
            return mn;

        return count switch
        {
            1 => mn + ".B",
            2 => mn + ".W",
            3 => mn + ".L",
            _ => mn
        };
    }

    private static int BytesToShow(Cpu65C816Constants.AddressMode mode)
    {
        switch (mode)
        {
            case Cpu65C816Constants.AddressMode.Constant8:
            case Cpu65C816Constants.AddressMode.Immediate8:
            case Cpu65C816Constants.AddressMode.DirectPage:
            case Cpu65C816Constants.AddressMode.DirectPageXIndex:
            case Cpu65C816Constants.AddressMode.DirectPageYIndex:
            case Cpu65C816Constants.AddressMode.DirectPageSIndex:
            case Cpu65C816Constants.AddressMode.DirectPageIndirect:
            case Cpu65C816Constants.AddressMode.DirectPageXIndexIndirect:
            case Cpu65C816Constants.AddressMode.DirectPageIndirectYIndex:
            case Cpu65C816Constants.AddressMode.DirectPageSIndexIndirectYIndex:
            case Cpu65C816Constants.AddressMode.DirectPageLongIndirect:
            case Cpu65C816Constants.AddressMode.DirectPageLongIndirectYIndex:
            case Cpu65C816Constants.AddressMode.Relative8:
                return 1;
            case Cpu65C816Constants.AddressMode.Immediate16:
            case Cpu65C816Constants.AddressMode.Address:
            case Cpu65C816Constants.AddressMode.AddressXIndex:
            case Cpu65C816Constants.AddressMode.AddressYIndex:
            case Cpu65C816Constants.AddressMode.AddressIndirect:
            case Cpu65C816Constants.AddressMode.AddressXIndexIndirect:
            case Cpu65C816Constants.AddressMode.AddressLongIndirect:
            case Cpu65C816Constants.AddressMode.Relative16:
                return 2;
            case Cpu65C816Constants.AddressMode.Long:
            case Cpu65C816Constants.AddressMode.LongXIndex:
                return 3;
        }
        return 0;
    }

    // {0} = mnemonic
    // {1} = intermediate address / label OR operand 1 for block move
    // {2} = operand 2 for block move
    private string GetInstructionFormatString(TByteSource data, int offset)
    {
        var mode = GetAddressMode(data, offset);
        switch (mode)
        {
            case Cpu65C816Constants.AddressMode.Implied:
                return "{0}";
            case Cpu65C816Constants.AddressMode.Accumulator:
                return "{0} A";
            case Cpu65C816Constants.AddressMode.Constant8:
            case Cpu65C816Constants.AddressMode.Immediate8:
            case Cpu65C816Constants.AddressMode.Immediate16:
                return "{0} #{1}";
            case Cpu65C816Constants.AddressMode.DirectPage:
            case Cpu65C816Constants.AddressMode.Address:
            case Cpu65C816Constants.AddressMode.Long:
            case Cpu65C816Constants.AddressMode.Relative8:
            case Cpu65C816Constants.AddressMode.Relative16:
                return "{0} {1}";
            case Cpu65C816Constants.AddressMode.DirectPageXIndex:
            case Cpu65C816Constants.AddressMode.AddressXIndex:
            case Cpu65C816Constants.AddressMode.LongXIndex:
                return "{0} {1},X";
            case Cpu65C816Constants.AddressMode.DirectPageYIndex:
            case Cpu65C816Constants.AddressMode.AddressYIndex:
                return "{0} {1},Y";
            case Cpu65C816Constants.AddressMode.DirectPageSIndex:
                return "{0} {1},S";
            case Cpu65C816Constants.AddressMode.DirectPageIndirect:
            case Cpu65C816Constants.AddressMode.AddressIndirect:
                return "{0} ({1})";
            case Cpu65C816Constants.AddressMode.DirectPageXIndexIndirect:
            case Cpu65C816Constants.AddressMode.AddressXIndexIndirect:
                return "{0} ({1},X)";
            case Cpu65C816Constants.AddressMode.DirectPageIndirectYIndex:
                return "{0} ({1}),Y";
            case Cpu65C816Constants.AddressMode.DirectPageSIndexIndirectYIndex:
                return "{0} ({1},S),Y";
            case Cpu65C816Constants.AddressMode.DirectPageLongIndirect:
            case Cpu65C816Constants.AddressMode.AddressLongIndirect:
                return "{0} [{1}]";
            case Cpu65C816Constants.AddressMode.DirectPageLongIndirectYIndex:
                return "{0} [{1}],Y";
            case Cpu65C816Constants.AddressMode.BlockMove:
                return "{0} {1},{2}";
        }
        return "";
    }
        
    public static Cpu65C816Constants.AddressMode? GetAddressMode(TByteSource data, int offset)
    {
        var opcode = data.GetRomByte(offset);
        if (!opcode.HasValue)
            return null;
            
        var mFlag = data.GetMFlag(offset);
        var xFlag = data.GetXFlag(offset);
            
        return GetAddressMode(opcode.Value, mFlag, xFlag);
    }

    public static Cpu65C816Constants.AddressMode GetAddressMode(int opcode, bool mFlag, bool xFlag)
    {
        var mode = Cpu65C816Constants.AddressingModes[opcode];
        return mode switch
        {
            Cpu65C816Constants.AddressMode.ImmediateMFlagDependent => mFlag
                ? Cpu65C816Constants.AddressMode.Immediate8
                : Cpu65C816Constants.AddressMode.Immediate16,
            Cpu65C816Constants.AddressMode.ImmediateXFlagDependent => xFlag
                ? Cpu65C816Constants.AddressMode.Immediate8
                : Cpu65C816Constants.AddressMode.Immediate16,
            _ => mode
        };
    }
}

public static class Cpu65C816Constants
{
    public enum AddressMode : byte
    {
        Implied, Accumulator, Constant8, Immediate8, Immediate16,
        ImmediateXFlagDependent, ImmediateMFlagDependent,
        DirectPage, DirectPageXIndex, DirectPageYIndex,
        DirectPageSIndex, DirectPageIndirect, DirectPageXIndexIndirect,
        DirectPageIndirectYIndex, DirectPageSIndexIndirectYIndex,
        DirectPageLongIndirect, DirectPageLongIndirectYIndex,
        Address, AddressXIndex, AddressYIndex, AddressIndirect,
        AddressXIndexIndirect, AddressLongIndirect,
        Long, LongXIndex, BlockMove, Relative8, Relative16
    }

    public static readonly string[] Mnemonics =
    {
        "BRK", "ORA", "COP", "ORA", "TSB", "ORA", "ASL", "ORA", "PHP", "ORA", "ASL", "PHD", "TSB", "ORA", "ASL", "ORA",
        "BPL", "ORA", "ORA", "ORA", "TRB", "ORA", "ASL", "ORA", "CLC", "ORA", "INC", "TCS", "TRB", "ORA", "ASL", "ORA",
        "JSR", "AND", "JSL", "AND", "BIT", "AND", "ROL", "AND", "PLP", "AND", "ROL", "PLD", "BIT", "AND", "ROL", "AND",
        "BMI", "AND", "AND", "AND", "BIT", "AND", "ROL", "AND", "SEC", "AND", "DEC", "TSC", "BIT", "AND", "ROL", "AND",
        "RTI", "EOR", "WDM", "EOR", "MVP", "EOR", "LSR", "EOR", "PHA", "EOR", "LSR", "PHK", "JMP", "EOR", "LSR", "EOR",
        "BVC", "EOR", "EOR", "EOR", "MVN", "EOR", "LSR", "EOR", "CLI", "EOR", "PHY", "TCD", "JML", "EOR", "LSR", "EOR",
        "RTS", "ADC", "PER", "ADC", "STZ", "ADC", "ROR", "ADC", "PLA", "ADC", "ROR", "RTL", "JMP", "ADC", "ROR", "ADC",
        "BVS", "ADC", "ADC", "ADC", "STZ", "ADC", "ROR", "ADC", "SEI", "ADC", "PLY", "TDC", "JMP", "ADC", "ROR", "ADC",
        "BRA", "STA", "BRL", "STA", "STY", "STA", "STX", "STA", "DEY", "BIT", "TXA", "PHB", "STY", "STA", "STX", "STA",
        "BCC", "STA", "STA", "STA", "STY", "STA", "STX", "STA", "TYA", "STA", "TXS", "TXY", "STZ", "STA", "STZ", "STA",
        "LDY", "LDA", "LDX", "LDA", "LDY", "LDA", "LDX", "LDA", "TAY", "LDA", "TAX", "PLB", "LDY", "LDA", "LDX", "LDA",
        "BCS", "LDA", "LDA", "LDA", "LDY", "LDA", "LDX", "LDA", "CLV", "LDA", "TSX", "TYX", "LDY", "LDA", "LDX", "LDA",
        "CPY", "CMP", "REP", "CMP", "CPY", "CMP", "DEC", "CMP", "INY", "CMP", "DEX", "WAI", "CPY", "CMP", "DEC", "CMP",
        "BNE", "CMP", "CMP", "CMP", "PEI", "CMP", "DEC", "CMP", "CLD", "CMP", "PHX", "STP", "JML", "CMP", "DEC", "CMP",
        "CPX", "SBC", "SEP", "SBC", "CPX", "SBC", "INC", "SBC", "INX", "SBC", "NOP", "XBA", "CPX", "SBC", "INC", "SBC",
        "BEQ", "SBC", "SBC", "SBC", "PEA", "SBC", "INC", "SBC", "SED", "SBC", "PLX", "XCE", "JSR", "SBC", "INC", "SBC"
    };

    public static readonly AddressMode[] AddressingModes =
    {
        AddressMode.Constant8, AddressMode.DirectPageXIndexIndirect, AddressMode.Constant8, AddressMode.DirectPageSIndex,
        AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPageLongIndirect,
        AddressMode.Implied, AddressMode.ImmediateMFlagDependent, AddressMode.Accumulator, AddressMode.Implied,
        AddressMode.Address, AddressMode.Address, AddressMode.Address, AddressMode.Long,
        AddressMode.Relative8, AddressMode.DirectPageIndirectYIndex, AddressMode.DirectPageIndirect, AddressMode.DirectPageSIndexIndirectYIndex,
        AddressMode.DirectPage, AddressMode.DirectPageXIndex, AddressMode.DirectPageXIndex, AddressMode.DirectPageLongIndirectYIndex,
        AddressMode.Implied, AddressMode.AddressYIndex, AddressMode.Accumulator, AddressMode.Implied,
        AddressMode.Address, AddressMode.AddressXIndex, AddressMode.AddressXIndex, AddressMode.LongXIndex,

        AddressMode.Address, AddressMode.DirectPageXIndexIndirect, AddressMode.Long, AddressMode.DirectPageSIndex,
        AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPageLongIndirect,
        AddressMode.Implied, AddressMode.ImmediateMFlagDependent, AddressMode.Accumulator, AddressMode.Implied,
        AddressMode.Address, AddressMode.Address, AddressMode.Address, AddressMode.Long,
        AddressMode.Relative8, AddressMode.DirectPageIndirectYIndex, AddressMode.DirectPageIndirect, AddressMode.DirectPageSIndexIndirectYIndex,
        AddressMode.DirectPageXIndex, AddressMode.DirectPageXIndex, AddressMode.DirectPageXIndex, AddressMode.DirectPageLongIndirectYIndex,
        AddressMode.Implied, AddressMode.AddressYIndex, AddressMode.Accumulator, AddressMode.Implied,
        AddressMode.AddressXIndex, AddressMode.AddressXIndex, AddressMode.AddressXIndex, AddressMode.LongXIndex,

        AddressMode.Implied, AddressMode.DirectPageXIndexIndirect, AddressMode.Constant8, AddressMode.DirectPageSIndex,
        AddressMode.BlockMove, AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPageLongIndirect,
        AddressMode.Implied, AddressMode.ImmediateMFlagDependent, AddressMode.Accumulator, AddressMode.Implied,
        AddressMode.Address, AddressMode.Address, AddressMode.Address, AddressMode.Long,
        AddressMode.Relative8, AddressMode.DirectPageIndirectYIndex, AddressMode.DirectPageIndirect, AddressMode.DirectPageSIndexIndirectYIndex,
        AddressMode.BlockMove, AddressMode.DirectPageXIndex, AddressMode.DirectPageXIndex, AddressMode.DirectPageLongIndirectYIndex,
        AddressMode.Implied, AddressMode.AddressYIndex, AddressMode.Implied, AddressMode.Implied,
        AddressMode.Long, AddressMode.AddressXIndex, AddressMode.AddressXIndex, AddressMode.LongXIndex,

        AddressMode.Implied, AddressMode.DirectPageXIndexIndirect, AddressMode.Relative16, AddressMode.DirectPageSIndex,
        AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPageLongIndirect,
        AddressMode.Implied, AddressMode.ImmediateMFlagDependent, AddressMode.Accumulator, AddressMode.Implied,
        AddressMode.AddressIndirect, AddressMode.Address, AddressMode.Address, AddressMode.Long,
        AddressMode.Relative8, AddressMode.DirectPageIndirectYIndex, AddressMode.DirectPageIndirect, AddressMode.DirectPageSIndexIndirectYIndex,
        AddressMode.DirectPageXIndex, AddressMode.DirectPageXIndex, AddressMode.DirectPageXIndex, AddressMode.DirectPageLongIndirectYIndex,
        AddressMode.Implied, AddressMode.AddressYIndex, AddressMode.Implied, AddressMode.Implied,
        AddressMode.AddressXIndexIndirect, AddressMode.AddressXIndex, AddressMode.AddressXIndex, AddressMode.LongXIndex,

        AddressMode.Relative8, AddressMode.DirectPageXIndexIndirect, AddressMode.Relative16, AddressMode.DirectPageSIndex,
        AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPageLongIndirect,
        AddressMode.Implied, AddressMode.ImmediateMFlagDependent, AddressMode.Implied, AddressMode.Implied,
        AddressMode.Address, AddressMode.Address, AddressMode.Address, AddressMode.Long,
        AddressMode.Relative8, AddressMode.DirectPageIndirectYIndex, AddressMode.DirectPageIndirect, AddressMode.DirectPageSIndexIndirectYIndex,
        AddressMode.DirectPageXIndex, AddressMode.DirectPageXIndex, AddressMode.DirectPageYIndex, AddressMode.DirectPageLongIndirectYIndex,
        AddressMode.Implied, AddressMode.AddressYIndex, AddressMode.Implied, AddressMode.Implied,
        AddressMode.Address, AddressMode.AddressXIndex, AddressMode.AddressXIndex, AddressMode.LongXIndex,

        AddressMode.ImmediateXFlagDependent, AddressMode.DirectPageXIndexIndirect, AddressMode.ImmediateXFlagDependent, AddressMode.DirectPageSIndex,
        AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPageLongIndirect,
        AddressMode.Implied, AddressMode.ImmediateMFlagDependent, AddressMode.Implied, AddressMode.Implied,
        AddressMode.Address, AddressMode.Address, AddressMode.Address, AddressMode.Long,
        AddressMode.Relative8, AddressMode.DirectPageIndirectYIndex, AddressMode.DirectPageIndirect, AddressMode.DirectPageSIndexIndirectYIndex,
        AddressMode.DirectPageXIndex, AddressMode.DirectPageXIndex, AddressMode.DirectPageYIndex, AddressMode.DirectPageLongIndirectYIndex,
        AddressMode.Implied, AddressMode.AddressYIndex, AddressMode.Implied, AddressMode.Implied,
        AddressMode.AddressXIndex, AddressMode.AddressXIndex, AddressMode.AddressYIndex, AddressMode.LongXIndex,

        AddressMode.ImmediateXFlagDependent, AddressMode.DirectPageXIndexIndirect, AddressMode.Constant8, AddressMode.DirectPageSIndex,
        AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPageLongIndirect,
        AddressMode.Implied, AddressMode.ImmediateMFlagDependent, AddressMode.Implied, AddressMode.Implied,
        AddressMode.Address, AddressMode.Address, AddressMode.Address, AddressMode.Long,
        AddressMode.Relative8, AddressMode.DirectPageIndirectYIndex, AddressMode.DirectPageIndirect, AddressMode.DirectPageSIndexIndirectYIndex,
        AddressMode.DirectPageIndirect, AddressMode.DirectPageXIndex, AddressMode.DirectPageXIndex, AddressMode.DirectPageLongIndirectYIndex,
        AddressMode.Implied, AddressMode.AddressYIndex, AddressMode.Implied, AddressMode.Implied,
        AddressMode.AddressLongIndirect, AddressMode.AddressXIndex, AddressMode.AddressXIndex, AddressMode.LongXIndex,

        AddressMode.ImmediateXFlagDependent, AddressMode.DirectPageXIndexIndirect, AddressMode.Constant8, AddressMode.DirectPageSIndex,
        AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPage, AddressMode.DirectPageLongIndirect,
        AddressMode.Implied, AddressMode.ImmediateMFlagDependent, AddressMode.Implied, AddressMode.Implied,
        AddressMode.Address, AddressMode.Address, AddressMode.Address, AddressMode.Long,
        AddressMode.Relative8, AddressMode.DirectPageIndirectYIndex, AddressMode.DirectPageIndirect, AddressMode.DirectPageSIndexIndirectYIndex,
        AddressMode.Address, AddressMode.DirectPageXIndex, AddressMode.DirectPageXIndex, AddressMode.DirectPageLongIndirectYIndex,
        AddressMode.Implied, AddressMode.AddressYIndex, AddressMode.Implied, AddressMode.Implied,
        AddressMode.AddressXIndexIndirect, AddressMode.AddressXIndex, AddressMode.AddressXIndex, AddressMode.LongXIndex,
    };
}
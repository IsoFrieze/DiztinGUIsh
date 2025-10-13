using System.Diagnostics;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Cpu._65816;

public class Cpu65C816<TByteSource> : Cpu<TByteSource> 
    where TByteSource : 
    IRomByteFlagsGettable, 
    IRomSize,
    IRomByteFlagsSettable, 
    ISnesAddressConverter, 
    ISteppable, 
    IReadOnlyByteSource, 
    ISnesIntermediateAddress,
    IInOutPointSettable,
    IInOutPointGettable,
    IReadOnlyLabels,
    ICommentTextProvider,
    IRegionProvider
{
    // TODO: expose these somehow to the project settings
    public bool AttemptTouseDirectPageArithmeticInFinalOutput { get; set; } = true;
    public bool AttemptToUnmirrorLabels { get; set; } = true;
    
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
    
    public override string GetInstructionStr(TByteSource data, int offset) => 
        GetInstructionData(data, offset).FullGeneratedText; // shortcut

    public override CpuInstructionDataFormatted GetInstructionData(TByteSource data, int offset)
    {
        var mode = GetAddressMode(data, offset);
        if (mode == null)
            throw new InvalidDataException("Expected non-null addressing mode");
            
        var format = GetInstructionFormatString(data, offset);
        var mnemonic = GetMnemonic(data, offset);
            
        int numDigitsForOperand1 = 0, numDigitsForOperand2 = 0;
        int? operandValue1 = null, operandValue2 = null;
        var identified = false;
        var overridesAllowed = false;
            
        switch (mode)
        {
            case Cpu65C816Constants.AddressMode.BlockMove:
                identified = true;
                numDigitsForOperand1 = numDigitsForOperand2 = 2;
                operandValue1 = data.GetRomByte(offset + 1);
                operandValue2 = data.GetRomByte(offset + 2);
                break;
            case Cpu65C816Constants.AddressMode.Constant8:
            case Cpu65C816Constants.AddressMode.Immediate8:
            case Cpu65C816Constants.AddressMode.Immediate16:
                identified = true;
                overridesAllowed = true;
                numDigitsForOperand1 = mode == Cpu65C816Constants.AddressMode.Immediate16 
                    ? 4 
                    : 2;
                operandValue1 = mode == Cpu65C816Constants.AddressMode.Immediate16 
                    ? data.GetRomWord(offset + 1) 
                    : data.GetRomByte(offset + 1);
                break;
        }
        
        var operandOriginalStr1 = "";
        var operandOriginalStr2 = "";
        
        if (!identified)
        {
            // note: lots of complexity with labels, mirroring, overrides, etc inside here:
            operandOriginalStr1 = FormatOperandAddress(data, offset);
            operandOriginalStr2 = "";
        }
        else
        {
            operandOriginalStr1 = operandValue1!=null ? CreateHexStr(operandValue1, numDigitsForOperand1) : "";
            operandOriginalStr2 = operandValue2!=null ? CreateHexStr(operandValue2, numDigitsForOperand2) : "";
        }

        var operandFinalStr1 = operandOriginalStr1;
        var operandFinalStr2 = operandOriginalStr2;
        
        // try a substitution, if any exist. only for opcodes with ONE operand (not going to handle the ones with two)
        if (overridesAllowed)
        {
            var specialDirective = GetSpecialDirectiveOverrideFromComments(data, offset);
            if (specialDirective != null)
            {
                if (!string.IsNullOrEmpty(specialDirective.TextToOverride))
                {
                    operandFinalStr1 = specialDirective.TextToOverride; // allow overriding here
                }
                else if (specialDirective.ConstantFormatOverride == CpuUtils.OperandOverride.FormatOverride.AsDecimal && operandValue1!=null)
                {
                    operandFinalStr1 = operandValue1.ToString()!;
                }
            }
        }
        
        var finalStr = string.Format(format, mnemonic, operandFinalStr1, operandFinalStr2);
        
        var pointerStr = GetPointerStr(data, offset);
        if (pointerStr != null)
            finalStr = pointerStr;
        
        var outputInstructionData = new CpuInstructionDataFormatted  {
            // generate a string like: "LDA.W $01,X" or "JSR.W fn_do_stuff"
            FullGeneratedText = finalStr,
            
            // save these in case useful later
            OriginalNonOverridenOperand1 = operandOriginalStr1,
            OriginalNonOverridenOperand2 = operandOriginalStr2,
            OverriddenOperand1 = operandFinalStr1,
            OverriddenOperand2 = operandFinalStr2,
            
            // save other stuff if you want 
        };
        
        return outputInstructionData;
    }

    private static int SearchForRomOffsetBoundsOfPointerTableFrom(TByteSource data, int offset, bool searchBackwards = true)
    {
        // what type of pointer table are we in the middle of?
        var pointerTableType = data.GetFlag(offset);
        if (pointerTableType is not (FlagType.Pointer16Bit or FlagType.Pointer24Bit or FlagType.Pointer32Bit))
            return -1;  // not a pointer table

        var currentBound = offset;
        while (true)
        {
            var candidateOffset = searchBackwards ? currentBound - 1 : currentBound + 1;
            if (candidateOffset > data.GetRomSize() || candidateOffset < 0)
                break;
            
            // must be marked as a pointer OF THE SAME TYPE
            if (data.GetFlag(candidateOffset) !=  pointerTableType)
                break;

            currentBound = candidateOffset;
        }
        
        return currentBound;
    }

    private static string? GetPointerStr(TByteSource data, int offset)
    {
        var pointerType = data.GetFlag(offset);
        if (pointerType is not (FlagType.Pointer16Bit or FlagType.Pointer24Bit or FlagType.Pointer32Bit))
            return null;

        var pointerTableStartOffset = SearchForRomOffsetBoundsOfPointerTableFrom(data, offset, searchBackwards: true);
        if (pointerTableStartOffset == -1)
            return null;
        
        // ok, we're inside a pointer table.
        // we don't want to display a string for every entry, but only the first of the N bytes of the pointer table
        var stride = pointerType switch
        {
            FlagType.Pointer16Bit => 2,
            FlagType.Pointer24Bit => 3,
            FlagType.Pointer32Bit => 4,
            // ReSharper disable once UnreachableSwitchArmDueToIntegerAnalysis
            _ => -1,
        };
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (stride == -1)
            return null; // shouldn't happen but
        
        // special: return empty string (NOT null) since we're in the upper bytes of a pointer table. we don't
        // want to show any text here.
        var distanceToStartOfTable = offset - pointerTableStartOffset;
        if (distanceToStartOfTable % stride != 0)
            return ""; 
        
        var ia = data.GetIntermediateAddressOrPointer(offset);
        if (ia == -1)
            return null;

        // we're in the middle of a pointer table AND in the right position.
        // show some useful text, if available.
        var labelAtIa = data.Labels.GetLabel(ia);
        if (labelAtIa != null)
            return labelAtIa.Name;
        
        var iaClipped = stride switch
        {
            2 => ia & 0xFFFF,
            3 => ia & 0xFFFFFF,
            _ => ia // same as 4
        };

        return RomUtil.ConvertNumToHexStr(iaClipped, stride);
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

    // this can print bytes OR labels. it can also deal with SOME mirroring and Direct Page addressing etc/etc
    private string FormatOperandAddress(TByteSource data, int offset)
    {
        var mode = GetAddressMode(data, offset);
        if (mode == null)
            throw new InvalidDataException("Expected non-null addressing mode");
        
        var allowLabelUsageHere = true;
        
        // -------------------------------------------------------------------
        // OPTION 0: SPECIAL DIRECTIVES
        // we'll let the user do weird special-case things with "!!" directives
        // -------------------------------------------------------------------
        var specialDirective = GetSpecialDirectiveOverrideFromComments(data, offset);
        if (specialDirective != null)
        {
            // ZERO VALIDATION OF THIS TEXT. it's up to the user to get it right. have fun
            if (!string.IsNullOrEmpty(specialDirective.TextToOverride))
                return specialDirective.TextToOverride;

            if (specialDirective.ForceOnlyShowRawHex)
                allowLabelUsageHere = false;
        }

        // ---------------------------------------------------------------
        // OPTION 1: Trying to find a label that is appropriate for
        //           this location, and if so, use it
        // ---------------------------------------------------------------
        
        if (allowLabelUsageHere)
        {
            var finalLabelExpressionToUse = GetFinalLabelExpressionToUse(data, offset);
            if (!string.IsNullOrEmpty(finalLabelExpressionToUse))
                return finalLabelExpressionToUse;
        }

        // ---------------------------------------------------------------
        // OPTION 2: Couldn't find a decent label to use
        //           We'll just print the raw hex number as a constant instead
        // ---------------------------------------------------------------
        return GetFormattedRawHexIa(data, offset);
    }

    private static string GetFormattedRawHexIa(TByteSource data, int offset)
    {
        // don't bake the directpage offset into this
        var intermediateAddress = data.GetIntermediateAddress(offset);
        
        var mode = GetAddressMode(data, offset);
        if (mode == null)
            throw new InvalidDataException("Expected non-null addressing mode");
        
        var numByteDigitsToDisplay = GetNumBytesToShow(mode.Value);
        if (mode is Cpu65C816Constants.AddressMode.Relative8 or Cpu65C816Constants.AddressMode.Relative16)
        {
            var romWord = data.GetRomWord(offset + 1);
            if (!romWord.HasValue)
                return "";

            intermediateAddress = (int)romWord;
        }

        return RomUtil.ConvertNumToHexStr(intermediateAddress, numByteDigitsToDisplay);
    }

    private string GetFinalLabelExpressionToUse(TByteSource data, int offset)
    {
        // important: setting "resolve: true" here bakes the DP offset into the IA.
        // this is usually what we want for labels BUT we have to build an expression that bakes this back out if so. 
        var intermediateAddress = data.GetIntermediateAddress(offset, resolve: true);
        if (intermediateAddress < 0)
            return "";
        
        var mode = GetAddressMode(data, offset);
        if (mode == null)
            throw new InvalidDataException("Expected non-null addressing mode");
        
        // first and easiest: is there a label for this absolute address AND are we allowed to use it? if so, we'll use that.
        // this label will include the DirectPage offset built into the IA.
        var candidateLabel = GetValidatedLabelNameForOffset(data, offset);

        // secondly: if, we didn't find a label that matches our IA 1:1. BUT, is there a mirrored label that works here?
        // example: you have a label defined for 7E0004, and our IA is 000004. those are a mirror of the same data so,
        // it's OK to use the 7E label.
        var unmirrorCorrectedDisplacement = 0;
        if (AttemptToUnmirrorLabels && candidateLabel == null) 
            (unmirrorCorrectedDisplacement, candidateLabel) = GetUnmirroredLabelNameAndDisplacement(data, mode.Value, intermediateAddress);
        
        // got a good label entry for this SNES address.
        // now, does this label have multiple entries and we have to pick one based on the surrounding context?
        // if so, do pick one now.
        var labelName = ResolveLabelNameWithContext(data, candidateLabel, offset);
        if (labelName == "")
            return "";

        // this is to try and get more labels in the output by creating a mathematical expression
        // for ASAR to use. only works if you have accurate 'D' register (direct page) set.
        // usually only useful after you've done a lot of tracelog capture.
        //
        // if your Directpage register is set wrong, you'll get wrong/weird label names, or miss a label name.
        // it will still COMPILE byte-identical, it'll just look weird to humans.
        var directPageDisplacement = 0;
        if (AttemptTouseDirectPageArithmeticInFinalOutput &&
            mode is Cpu65C816Constants.AddressMode.DirectPage
                 or Cpu65C816Constants.AddressMode.DirectPageXIndex
                 or Cpu65C816Constants.AddressMode.DirectPageYIndex
                 or Cpu65C816Constants.AddressMode.DirectPageIndirect
                 or Cpu65C816Constants.AddressMode.DirectPageXIndexIndirect
                 or Cpu65C816Constants.AddressMode.DirectPageIndirectYIndex
                 or Cpu65C816Constants.AddressMode.DirectPageLongIndirect
                 or Cpu65C816Constants.AddressMode.DirectPageLongIndirectYIndex)
        {
            // intermediateAddress already has the dp offset baked in at this point.
            // HOWEVER, we need to build an expression that backs it out now.

            // example:
            // say we have a line where D = $0100
            // the instruction says LDA.B $20
            // the true address the SNES is going to load is at $120 ($100 + $20 = $120)
            //
            // if we add a label for Diz like "player_health" for $120, then we want the output line to have the word "player_health" in it.
            // so what we'll do here is build an expression for asar that will look like:
            // LDA.B player_health-$100 ; since D=100, player_health-$100 = $20, and $20 is what actually gets assembled here, which is what we want.

            directPageDisplacement = data.GetDirectPage(offset);
        }
        
        // everything is figured out, now actually render the final label text.
        
        // sometimes we need to add expressions to subtract out extra numbers to make the right
        // final bytes so asar will generate byte-identical output. do that here.
        // these will return "" if no need for extra math (which is the typical case)
        var dpOffsetExprStr = GenerateDisplacementString(directPageDisplacement);
        var unMirrorCorrectedOffsetStr = GenerateDisplacementString(unmirrorCorrectedDisplacement);
        
        return $"{labelName}{unMirrorCorrectedOffsetStr}{dpOffsetExprStr}";
    }

    private static string ResolveLabelNameWithContext(TByteSource data, IAnnotationLabel? candidateLabel, int offset)
    {
        if (candidateLabel == null)
            return "";
        
        var snesAddress = data.ConvertPCtoSnes(offset);
        if (candidateLabel.ContextMappings.Count <= 0 || snesAddress == -1) 
            return candidateLabel.Name;
        
        // find any applicable regions in the surrounding context of where we are in the ROM offset:
        var applicableOrderedRegions = data.Regions
            .Where(x => snesAddress >= x.StartSnesAddress && snesAddress <= x.EndSnesAddress)
            .OrderBy(x => x.Priority)
            .ToList();

        foreach (var region in applicableOrderedRegions)
        {
            var matchingLabelContext = candidateLabel.ContextMappings
                .FirstOrDefault(labelContext => labelContext.Context == region.ContextToApply);

            if (matchingLabelContext == null) 
                continue;
            
            return matchingLabelContext.NameOverride;
        }

        // didn't find a valid override at this context. use the default name
        return candidateLabel.Name;
    }

    private (int unmirrorCorrectedDisplacement, IAnnotationLabel? unmirroredLabel) GetUnmirroredLabelNameAndDisplacement(
        TByteSource data, Cpu65C816Constants.AddressMode mode, int snesAddress)
    {
        // NOTE: BE REALLY CAREFUL: SearchForMirroredLabel() IS A PERFORMANCE-INTENSE and HEAVILY OPTIMIZED FUNCTION
        var (mirroredLabelSnesAddress, mirrorLabelEntry) = SearchForMirroredLabel(data, snesAddress);

        if (mirrorLabelEntry == null || mirroredLabelSnesAddress == -1)
            return (0, null);

        // we're good, use this label
        // BUT: in some situations, the output instructions needs to generate the un-mirrored address in order to match
        // the exact bytes in the original ROM.  so, we may need to offset back out the difference between the two mirrors.
        // see https://github.com/IsoFrieze/DiztinGUIsh/issues/117 for an example
        var numBytesToShow = GetNumBytesToShow(mode);
        var mask = numBytesToShow switch
        {
            1 => 0xFF,
            2 => 0xFFFF,
            3 => 0xFFFFFF,
            _ => -1
        };
        
        // valid for this to be positive or negative
        var unmirrorCorrectedDisplacement = 0;
        if (mask > 0 && (mirroredLabelSnesAddress & mask) != (snesAddress & mask)) 
            unmirrorCorrectedDisplacement = mirroredLabelSnesAddress - snesAddress;

        return (unmirrorCorrectedDisplacement, mirrorLabelEntry);
    }

    // generate a string suitable for use with Asar expression math.
    // i.e. "-$FF" or "+$0100", etc
    private static string GenerateDisplacementString(int amountToDisplace)
    {
        if (amountToDisplace == 0) 
            return "";
        
        // IMPORTANT: Asar doesn't allow any whitespace between any math expression terms. don't output spaces here,
        // or anywhere that we use this expression.  i.e. "A+B" is valid, but "A + B" will throw an error.
        var direction = amountToDisplace > 0 ? '-' : '+';
        
        // since we're handling the sign ourselves, we only want to print positive numbers
        var absAmountToDisplace = Math.Abs(amountToDisplace);
        
        return $"{direction}${absAmountToDisplace:X}";
    }

    private static CpuUtils.OperandOverride? GetSpecialDirectiveOverrideFromComments(TByteSource data, int offset)
    {
        // here be dragons.  we'll let the user override anything they ever wanted to in the comments.
        // there will be, for now, very little validation/etc.
        var snesAddress = data.ConvertPCtoSnes(offset);
        if (snesAddress == -1)
            return null;
        
        // searches both ROM comments and comments from the label list
        var comment = data.GetCommentText(snesAddress);
        return CpuUtils.ParseCommentSpecialDirective(comment);
    }

    private static IAnnotationLabel? GetValidatedLabelNameForOffset(TByteSource data, int srcOffset)
    {
        var destinationIa = data.GetIntermediateAddress(srcOffset, true);
        if (destinationIa < 0)
            return null;
        
        var candidateLabel = data.Labels.GetLabel(destinationIa);
        if (string.IsNullOrEmpty(candidateLabel?.Name))
            return null;
        
        // some special cases related to +/- local labels:
        
        // is this a local label?  like "+". "-", "++", "--", etc?
        if (!RomUtil.IsValidPlusMinusLabel(candidateLabel.Name)) 
            return candidateLabel;      // not local label, so we're good
        
        // this IS a local +/- label, so let's do some additional validation..
        
        var opcode = data.GetRomByte(srcOffset);
        var opcodeIsBranch = 
            opcode == 0x80 || // BRA
            opcode == 0x10 || opcode == 0x30 || opcode == 0x50 || opcode == 0x70 || // BPL BMI BVC BVS
            opcode == 0x90 || opcode == 0xB0 || opcode == 0xD0 || opcode == 0xF0;   // BCC BCS BNE BEQ
        // NOT going to do this for any JUMPs like JMP, JML, and also not BRL
        
        // don't allow local +/- labels unless the opcode is a branch
        if (!opcodeIsBranch)
            return null;
    
        // finally, if this IS a branch AND a +/- label,
        // make sure the branch is in the correct direction
        // (no other checks prevent this except right here).
        // DIZ doesn't treat local labels special, so it's up
        // to us to enforce this here:
        var srcSnesAddress = data.ConvertPCtoSnes(srcOffset);
        var branchDirectionIsForward = candidateLabel.Name[0] == '+';
        
        var validBranchDirection =
            srcSnesAddress != destinationIa &&                            // infinite loop (branch to self) 
            branchDirectionIsForward == (srcSnesAddress < destinationIa); // trying to branch the wrong way

        return validBranchDirection ? candidateLabel : null;
    }

    private (int labelAddress, IAnnotationLabel? labelEntry) SearchForMirroredLabel(TByteSource data, int snesAddress)
    {
        // WARNING: during assembly text export, this function is EXTREMELY performance intensive.
        // PLEASE PROFILE before making any serious changes 

        // optimization: during exporting, this function is EXTREMELY performance intensive.
        // let's try and use a smaller subset of labels to search.
        // this will only be available in certain contexts, like when exporting assembly text.
        var exporterCache = data.Labels.MirroredLabelCacheSearch;
        if (exporterCache != null)
            return exporterCache.SearchOptimizedForMirroredLabel(snesAddress);

        // less optimized fallback version (does same thing as above, but uses all labels)
        // this is used during normal operation (like scrolling around the grid)
        foreach (var (labelAddress, labelEntry) in data.Labels.Labels)
        {
            if (!RomUtil.AreLabelsSameMirror(snesAddress, labelAddress)) 
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

    private string GetMnemonic(TByteSource data, int offset, bool showHint = true)
    {
        var mn = Cpu65C816Constants.Mnemonics[data.GetRomByteUnsafe(offset)];
        if (!showHint) 
            return mn;

        var mode = GetAddressMode(data, offset);
        if (mode == null)
            return mn;
                
        var count = GetNumBytesToShow(mode.Value);

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

    private static int GetNumBytesToShow(Cpu65C816Constants.AddressMode mode)
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
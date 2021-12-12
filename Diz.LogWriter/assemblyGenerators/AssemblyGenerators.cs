﻿using Diz.Core.model;
using Diz.Core.util;

namespace Diz.LogWriter.assemblyGenerators
{
    public class AssemblyGeneratePercent : AssemblyPartialLineGenerator
    {
        public AssemblyGeneratePercent()
        {
            Token = "";
            DefaultLength = 1;
            RequiresToken = false;
            UsesOffset = false;
        }
        protected override string Generate(int length)
        {
            return "%";  // just a literal %
        }
    }

    public class AssemblyGenerateEmpty : AssemblyPartialLineGenerator
    {
        public AssemblyGenerateEmpty()
        {
            Token = "%empty";
            DefaultLength = 1;
            UsesOffset = false;
        }
        protected override string Generate(int length)
        {
            return string.Format($"{{0,{length}}}", "");
        }
    }

    public class AssemblyGenerateLabel : AssemblyPartialLineGenerator
    {
        public AssemblyGenerateLabel()
        {
            Token = "label";
            DefaultLength = -22;
        }
        protected override string Generate(int offset, int length)
        {
            // what we're given: a PC offset in ROM.
            // what we need to find: any labels (SNES addresses) that refer to it.
            //
            // i.e. given that we are at PC offset = 0,
            // we find valid SNES offsets mirrored of 0xC08000 and 0x808000 which both refer to the same place
            // 
            // TODO: we may still need to deal with that mirroring here
            // TODO: eventually, support multiple labels tagging the same address, it may not always be just one.
        
            var snesAddress = Data.ConvertPCtoSnes(offset); 
            var label = Data.Labels.GetLabelName(snesAddress);
            if (label == null)
                return "";
        
            LogCreator.OnLabelVisited(snesAddress);

            var noColon = label.Length == 0 || label[0] == '-' || label[0] == '+';

            var str = $"{label}{(noColon ? "" : ":")}";
            return Util.LeftAlign(length, str);
        }
    }

    public class AssemblyGenerateCode : AssemblyPartialLineGenerator
    {
        public AssemblyGenerateCode()
        {
            Token = "code";
            DefaultLength = 37;
        }
        protected override string Generate(int offset, int length)
        {
            var bytes = LogCreator.GetLineByteLength(offset);
            var code = "";

            switch (Data.GetFlag(offset))
            {
                case FlagType.Opcode:
                    code = Data.GetInstruction(offset);
                    break;
                case FlagType.Unreached:
                case FlagType.Operand:
                case FlagType.Data8Bit:
                case FlagType.Graphics:
                case FlagType.Music:
                case FlagType.Empty:
                    code = Data.GetFormattedBytes(offset, 1, bytes);
                    break;
                case FlagType.Data16Bit:
                    code = Data.GetFormattedBytes(offset, 2, bytes);
                    break;
                case FlagType.Data24Bit:
                    code = Data.GetFormattedBytes(offset, 3, bytes);
                    break;
                case FlagType.Data32Bit:
                    code = Data.GetFormattedBytes(offset, 4, bytes);
                    break;
                case FlagType.Pointer16Bit:
                    code = Data.GeneratePointerStr(offset, 2);
                    break;
                case FlagType.Pointer24Bit:
                    code = Data.GeneratePointerStr(offset, 3);
                    break;
                case FlagType.Pointer32Bit:
                    code = Data.GeneratePointerStr(offset, 4);
                    break;
                case FlagType.Text:
                    code = Data.GetFormattedText(offset, bytes);
                    break;
            }

            return Util.LeftAlign(length, code);
        }
    }

    public class AssemblyGenerateOrg : AssemblyPartialLineGenerator
    {
        public AssemblyGenerateOrg()
        {
            Token = "%org";
            DefaultLength = 37;
            UsesOffset = true;
        }
        protected override string Generate(int offset, int length)
        {
            var org =
                $"ORG {Util.NumberToBaseString(Data.ConvertPCtoSnes(offset), Util.NumberBase.Hexadecimal, 6, true)}";
            return Util.LeftAlign(length, org);
        }
    }

    public class AssemblyGenerateMap : AssemblyPartialLineGenerator
    {
        public AssemblyGenerateMap()
        {
            Token = "%map";
            DefaultLength = 37;
            UsesOffset = false;
        }
        protected override string Generate(int length)
        {
            var romMapType = Data.RomMapMode switch
            {
                RomMapMode.LoRom => "lorom",
                RomMapMode.HiRom => "hirom",
                RomMapMode.Sa1Rom => "sa1rom",
                RomMapMode.ExSa1Rom => "exsa1rom",
                RomMapMode.SuperFx => "sfxrom",
                RomMapMode.ExHiRom => "exhirom",
                RomMapMode.ExLoRom => "exlorom",
                _ => ""
            };
            return Util.LeftAlign(length, romMapType);
        }
    }
    
    // 0+ = bank_xx.asm, -1 = labels.asm
    public class AssemblyGenerateIncSrc : AssemblyPartialLineGenerator
    {
        public AssemblyGenerateIncSrc()
        {
            Token = "%incsrc";
            DefaultLength = 1;
        }
        protected override string Generate(int offset, int length)
        {
            return Util.LeftAlign(length,BuildIncsrc(offset));
        }

        private string BuildIncsrc(int offset)
        {
            return offset >= 0
                ? BuildOutputForOffset(offset)
                : BuildIncSrc("labels.asm");
        }

        private string BuildOutputForOffset(int offset)
        {
            var bank = Data.ConvertPCtoSnes(offset) >> 16;
            var name = Util.NumberToBaseString(bank, Util.NumberBase.Hexadecimal, 2);
            return BuildBankInclude(name);
        }

        private static string BuildBankInclude(string name)
        {
            var val = $"bank_{name}.asm";
            return BuildIncSrc(val);
        }

        private static string BuildIncSrc(string val)
        {
            return $"incsrc \"{val}\"";
        }
    }
    
    public class AssemblyGenerateBankCross : AssemblyPartialLineGenerator
    {
        public AssemblyGenerateBankCross()
        {
            Token = "%bankcross";
            DefaultLength = 1;
        }
        protected override string Generate(int length)
        {
            return Util.LeftAlign(length, "check bankcross off");
        }
    }
    
    public class AssemblyGenerateIndirectAddress : AssemblyPartialLineGenerator
    {
        public AssemblyGenerateIndirectAddress()
        {
            Token = "ia";
            DefaultLength = 6;
        }
        protected override string Generate(int offset, int length)
        {
            var ia = Data.GetIntermediateAddressOrPointer(offset);
            return ia >= 0 ? Util.ToHexString6(ia) : "      ";
        }
    }
    
    public class AssemblyGenerateProgramCounter : AssemblyPartialLineGenerator
    {
        public AssemblyGenerateProgramCounter()
        {
            Token = "pc";
            DefaultLength = 6;
        }
        protected override string Generate(int offset, int length)
        {
            return Util.ToHexString6(Data.ConvertPCtoSnes(offset));
        }
    }
    
    public class AssemblyGenerateOffset : AssemblyPartialLineGenerator
    {
        public AssemblyGenerateOffset()
        {
            Token = "offset";
            DefaultLength = -6; // trim to length
        }
        protected override string Generate(int offset, int length)
        {
            var hexStr = Util.NumberToBaseString(offset, Util.NumberBase.Hexadecimal, 0);
            return Util.LeftAlign(length, hexStr);
        }
    }
    
    public class AssemblyGenerateDataBytes : AssemblyPartialLineGenerator
    {
        public AssemblyGenerateDataBytes()
        {
            Token = "bytes";
            DefaultLength = 8;
        }
        protected override string Generate(int offset, int length)
        {
            var bytes = BuildByteString(offset);
            
            // TODO: FIXME: use 'length' here in this format string
            return $"{bytes,-8}";
        }

        private string BuildByteString(int offset)
        {
            if (Data.GetFlag(offset) != FlagType.Opcode) 
                return "";
            
            var bytes = "";
            for (var i = 0; i < Data.GetInstructionLength(offset); i++)
            {
                var romByte = Data.GetRomByteUnsafe(offset + i);
                bytes += Util.NumberToBaseString(romByte, Util.NumberBase.Hexadecimal);
            }

            return bytes;
        }
    }
    
    public class AssemblyGenerateComment : AssemblyPartialLineGenerator
    {
        public AssemblyGenerateComment()
        {
            Token = "comment";
            DefaultLength = 1;
        }
        protected override string Generate(int offset, int length)
        {
            var snesOffset = Data.ConvertPCtoSnes(offset);
            var str = Data.GetCommentText(snesOffset);
            return Util.LeftAlign(length, str);
        }
    }
    
    public class AssemblyGenerateDataBank : AssemblyPartialLineGenerator
    {
        public AssemblyGenerateDataBank()
        {
            Token = "b";
            DefaultLength = 2;
        }
        protected override string Generate(int offset, int length)
        {
            return Util.NumberToBaseString(Data.GetDataBank(offset), Util.NumberBase.Hexadecimal, 2);
        }
    }
    
    public class AssemblyGenerateDirectPage : AssemblyPartialLineGenerator
    {
        public AssemblyGenerateDirectPage()
        {
            Token = "d";
            DefaultLength = 4;
        }
        protected override string Generate(int offset, int length)
        {
            return Util.NumberToBaseString(Data.GetDirectPage(offset), Util.NumberBase.Hexadecimal, 4);
        }
    }
    
    public class AssemblyGenerateMFlag : AssemblyPartialLineGenerator
    {
        public AssemblyGenerateMFlag()
        {
            Token = "m";
            DefaultLength = 1;
        }
        protected override string Generate(int offset, int length)
        {
            var m = Data.GetMFlag(offset);
            if (length == 1) 
                return m ? "M" : "m";
        
            return m ? "08" : "16";
        }
    }
    
    public class AssemblyGenerateXFlag : AssemblyPartialLineGenerator
    {
        public AssemblyGenerateXFlag()
        {
            Token = "x";
            DefaultLength = 1;
        }
        protected override string Generate(int offset, int length)
        {
            var x = Data.GetXFlag(offset);
            if (length == 1) 
                return x ? "X" : "x";
        
            return x ? "08" : "16";
        }
    }
    
    // output label at snes offset, and its value
    // example output:  "FnMultiplyByTwo = $808012"
    public class AssemblyGenerateLabelAssign : AssemblyPartialLineGenerator
    {
        public AssemblyGenerateLabelAssign()
        {
            Token = "%labelassign";
            DefaultLength = 1;
        }
        protected override string Generate(int offset, int length)
        {
            // EXTREMELY IMPORTANT:
            // unlike all the other generators where offset is a ROM offset,
            // for us, offset will be a SNES address.
            var snesAddress = offset; // yes. this is correct.
            
            var labelName = Data.Labels.GetLabelName(snesAddress);
            var snesAddrFormatted = Util.NumberToBaseString(snesAddress, Util.NumberBase.Hexadecimal, 6, true);
            var labelComment = Data.Labels.GetLabelComment(snesAddress);

            if (string.IsNullOrEmpty(labelName))
                return "";

            labelComment ??= "";

            var finalCommentText = "";

            // TODO: probably not the best way to stuff this in here. -Dom
            // we should consider putting this in the %comment% section in the future.
            // for now, just hacking this in so it's included somewhere. this option defaults to OFF
            if (LogCreator.Settings.PrintLabelSpecificComments && labelComment != "")
                finalCommentText = $"; !^ {labelComment} ^!";

            var str = $"{labelName} = {snesAddrFormatted}{finalCommentText}";
            return Util.LeftAlign(length, str);
        }
    }
}
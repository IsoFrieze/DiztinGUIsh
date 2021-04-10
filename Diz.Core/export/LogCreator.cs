using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Diz.Core.model;
using Diz.Core.util;
using JetBrains.Annotations;

namespace Diz.Core.export
{
    public partial class LogCreator
    {
        public enum FormatUnlabeled
        {
            ShowAll = 0,
            ShowInPoints = 1, // TODO Add Show In Points with +/- labels
            ShowNone = 2
        }

        public enum FormatStructure
        {
            SingleFile = 0,
            OneBankPerFile = 1
        }
        
        protected internal class AssemblerHandler : Attribute
        {
            public string Token;
            public int Length;
        }

        public class OutputResult
        {
            public bool Success;
            public int ErrorCount = -1;
            public LogCreator LogCreator;
            public string OutputStr = ""; // only set if outputString=true
        }

        public LogWriterSettings Settings { get; init; }
        public ILogCreatorDataSource Data { get; init; }

        private LogCreatorOutput Output;
        private List<int> LabelsWeVisited; // snes addresses

        public virtual OutputResult CreateLog()
        {
            OutputResult result;

            try
            {
                Init();
                WriteLog();
                result = GetResult();
                CloseOutput(result);
            }
            finally
            {
                Cleanup(); // critical to ALWAYS do this so we restore labels
            }

            return result;
        }

        private LogCreatorTempLabelGenerator LogCreatorTempLabelGenerator { get; set; }
        private LogCreatorLineFormatter LogCreatorLineFormatter { get; set; }

        private Dictionary<string, AssemblyPartialLineGenerator> Generators { get; set; }

        protected virtual void Init()
        {
            Debug.Assert(Settings.RomSizeOverride == -1 || Settings.RomSizeOverride <= Data.GetRomSize());
            
            InitOutput();

            Generators = CreateAssemblyGenerators();
            LogCreatorLineFormatter = new LogCreatorLineFormatter(Settings.Format, Generators);
            errorCount = 0;
            LabelsWeVisited = new List<int>();

            // MODIFIES UNDERLYING DATA. MAKE SURE TO UNDO THIS.
            GenerateTemporaryLabelsIfNeeded();
        }

        private void GenerateTemporaryLabelsIfNeeded()
        {
            if (Settings.Unlabeled == FormatUnlabeled.ShowNone) 
                return;
            
            LogCreatorTempLabelGenerator = new LogCreatorTempLabelGenerator {
                LogCreator = this, GenerateAllUnlabeled = Settings.Unlabeled == FormatUnlabeled.ShowAll,
            };
            
            LogCreatorTempLabelGenerator.GenerateTemporaryLabels();
        }

        private void InitOutput()
        {
            Output = Settings.OutputToString ? new LogCreatorStringOutput() : Output = new LogCreatorStreamOutput();
            Output.Init(this);
        }

        protected virtual void Cleanup()
        {
            LogCreatorTempLabelGenerator?.ClearTemporaryLabels();
        }

        private void CloseOutput(OutputResult result)
        {
            Output?.Finish(result);
            Output = null;
        }

        private OutputResult GetResult()
        {
            var result = new OutputResult()
            {
                ErrorCount = errorCount,
                Success = true,
                LogCreator = this
            };

            if (Settings.OutputToString)
                result.OutputStr = ((LogCreatorStringOutput)Output)?.OutputString;

            return result;
        }

        private int errorCount;

        private void ReportError(int offset, string msg)
        {
            ++errorCount;
            var offsetMsg = offset >= 0 ? $" Offset 0x{offset:X}" : "";
            Output.WriteErrorLine($"({errorCount}){offsetMsg}: {msg}");
        }

        private DataErrorChecking dataErrorChecking;
        private DataErrorChecking DataErrorChecking
        {
            get
            {
                if (dataErrorChecking == null)
                {
                    dataErrorChecking = new DataErrorChecking
                    {
                        Data = Data,
                    };
                    dataErrorChecking.ErrorNotifier += (_, errorInfo) => 
                        ReportError(errorInfo.Offset, errorInfo.Msg);
                }

                return dataErrorChecking;
            }
        }

        private void CheckForErrorsAt(int offset)
        {
            DataErrorChecking.CheckForErrorsAt(offset);
        }

        public int GetRomSize()
        {
            return Settings.RomSizeOverride != -1 ? Settings.RomSizeOverride : Data.GetRomSize();
        }

        protected virtual void WriteLog()
        {
            var size = GetRomSize();

            WriteMainIncludes(size);
            var pointer = WriteMainAssembly(size);
            WriteLabels(pointer);
        }

        private int WriteMainAssembly(int size)
        {
            // perf: this is the meat of the export, takes a while
            var pointer = 0;
            var bank = -1;
            while (pointer < size)
            {
                WriteAddress(ref pointer, ref bank);
            }
            return pointer;
        }

        protected void WriteMainIncludes(int size)
        {
            const int ignored = 0; 
            Output.WriteLine(GenerateLine(ignored, "map"));
            Output.WriteLine(GenerateLine(ignored, "empty"));
            WriteMainBankIncludes(size);
        }

        private void WriteMainBankIncludes(int size)
        {
            if (Settings.Structure != FormatStructure.OneBankPerFile)
                return;

            for (var i = 0; i < size; i += Data.GetBankSize())
                Output.WriteLine(GenerateLine(i, "incsrc"));

            Output.WriteLine(GenerateLine(-1, "incsrc"));
        }

        // address is a "PC address" i.e. offset into the ROM.
        // not a SNES address.
        protected void WriteAddress(ref int pointer, ref int currentBank)
        {
            SwitchBanksIfNeeded(pointer, ref currentBank);

            WriteBlankLineIfStartingNewParagraph(pointer);
            WriteTheRealLine(pointer);
            WriteBlankLineIfEndPoint(pointer);

            pointer += GetLineByteLength(pointer);
        }

        private void WriteTheRealLine(int pointer) => 
            Output.WriteLine(GenerateLine(pointer, null));

        private void WriteBlankLineIfEndPoint(int pointer)
        {
            if (!IsLocationAnEndPoint(pointer)) 
                return;
            
            const int ignored = 0; Output.WriteLine(GenerateLine(ignored, "empty"));
        }

        private void WriteBlankLineIfStartingNewParagraph(int pointer)
        {
            if (!IsLocationAReadPoint(pointer) && !AnyLabelsPresent(pointer)) 
                return;
            
            const int ignored = 0; Output.WriteLine(GenerateLine(ignored, "empty"));
        }

        private bool IsLocationPoint(int pointer, InOutPoint mustHaveFlag) => (Data.GetInOutPoint(pointer) & mustHaveFlag) != 0;
        private bool IsLocationAnEndPoint(int pointer) => IsLocationPoint(pointer, InOutPoint.EndPoint);
        private bool IsLocationAReadPoint(int pointer) => IsLocationPoint(pointer, InOutPoint.ReadPoint);
        
        private bool AnyLabelsPresent(int pointer)
        {
            var snesAddress = Data.ConvertPCtoSnes(pointer);
            return Data.LabelProvider.GetLabel(snesAddress)?.Name.Length > 0;
        }

        private void SwitchBanksIfNeeded(int pointer, ref int currentBank)
        {
            var snesAddress = Data.ConvertPCtoSnes(pointer);

            var thisBank = snesAddress >> 16;

            if (thisBank == currentBank)
                return;

            OpenNewBank(pointer, thisBank);
            currentBank = thisBank;

            if (snesAddress % Data.GetBankSize() == 0) 
                return;

            ReportError(pointer, "An instruction crossed a bank boundary.");
        }

        private void OpenNewBank(int pointer, int thisBank)
        {
            Output.SwitchToBank(thisBank);

            const int ignored = 0; 
            Output.WriteLine(GenerateLine(ignored, "empty"));
            Output.WriteLine(GenerateLine(pointer, "org"));
            Output.WriteLine(GenerateLine(ignored, "empty"));
        }

        protected void WriteLabels(int pointer)
        {
            // TODO check for PC to snes stuff
            var unvisitedLabels = GetUnvisitedLabels();
            WriteAnyUnvisitedLabels(pointer, unvisitedLabels);
            PrintAllLabelsIfRequested(pointer, unvisitedLabels);
        }

        private Dictionary<int, IReadOnlyLabel> GetUnvisitedLabels()
        {
            var unvisitedLabels = new Dictionary<int, IReadOnlyLabel>();  // snes addresses

            // part 1: important: include all labels we aren't defining somewhere else. needed for disassembly
            foreach (var (snesAddress, label) in Data.LabelProvider.Labels)
            {
                if (LabelsWeVisited.Contains(snesAddress))
                    continue;

                // this label was not defined elsewhere in our disassembly, so we need to include it in labels.asm
                unvisitedLabels.Add(snesAddress, label);
            }

            return unvisitedLabels;
        }

        private void WriteAnyUnvisitedLabels(int pointer, Dictionary<int, IReadOnlyLabel> unvisitedLabels)
        {
            SwitchOutputStream(pointer, "labels");

            foreach (var pair in unvisitedLabels)
            {
                var snesAddress = pair.Key;
                var pcOffset = Data.ConvertSnesToPc(snesAddress);
                Output.WriteLine(GenerateLine(pcOffset, "labelassign"));
            }
        }
        
        private void PrintAllLabelsIfRequested(int pointer, IReadOnlyDictionary<int, IReadOnlyLabel> unvisitedLabels)
        {
            // part 2: optional: if requested, print all labels regardless of use.
            // Useful for debugging, documentation, or reverse engineering workflow.
            // this file shouldn't need to be included in the build, it's just reference documentation

            if (!Settings.IncludeUnusedLabels) 
                return;
            
            SwitchOutputStream(pointer, "all-labels.txt"); // TODO: csv in the future. escape commas
            
            foreach (var (snesAddress, _) in Data.LabelProvider.Labels)
            {
                // not the best place to add formatting, TODO: cleanup
                var category = unvisitedLabels.ContainsKey(snesAddress) ? "UNUSED" : "USED";
                var labelPcAddress = Data.ConvertPCtoSnes(pointer);
                // TODO: double check this is the right snes/pc conversion
                Output.WriteLine($";!^!-{category}-! " + GenerateLine(labelPcAddress, "labelassign"));
            }
        }

        protected void SwitchOutputStream(int pointer, string streamName)
        {
            Output.SwitchToStream(streamName);

            // write an extra blank line if we would normally switch files here
            if (Settings.Structure != FormatStructure.SingleFile) 
                return;

            const int ignored = 0; 
            Output.WriteLine(GenerateLine(ignored, "empty"));
        }

        // --------------------------
        #region WriteOperations

        private string GenerateLine(int offset, string specialStr)
        {
            var line = "";

            foreach (var (parameter, length) in LogCreatorLineFormatter.ParsedLineFormat)
            {
                line += GenerateLinePartial(offset, parameter, length, specialStr);
            }

            if (specialStr == null)
                CheckForErrorsAt(offset);
            
            return line;
        }

        private string GenerateLinePartial(int offset, string parameter, int length, string specialModifierStr)
        {
            // string literal version
            if (length == int.MaxValue)
                return parameter;

            // special case (replaces code & everything else = empty)
            if (specialModifierStr != null) 
                parameter = $"%{(parameter != "code" ? "empty" : specialModifierStr)}";

            if (!Generators.TryGetValue(parameter, out var generator))
                throw new InvalidOperationException($"Can't find generator for {parameter}");

            return generator.Emit(offset, length);
        }

        internal int GetLineByteLength(int offset)
        {
            int max = 1, step = 1;
            var size = GetRomSize();

            switch (Data.GetFlag(offset))
            {
                case FlagType.Opcode:
                    return Data.GetInstructionLength(offset);
                case FlagType.Unreached:
                case FlagType.Operand:
                case FlagType.Data8Bit:
                case FlagType.Graphics:
                case FlagType.Music:
                case FlagType.Empty:
                    max = Settings.DataPerLine;
                    break;
                case FlagType.Text:
                    max = 21;
                    break;
                case FlagType.Data16Bit:
                    step = 2;
                    max = Settings.DataPerLine;
                    break;
                case FlagType.Data24Bit:
                    step = 3;
                    max = Settings.DataPerLine;
                    break;
                case FlagType.Data32Bit:
                    step = 4;
                    max = Settings.DataPerLine;
                    break;
                case FlagType.Pointer16Bit:
                    step = 2;
                    max = 2;
                    break;
                case FlagType.Pointer24Bit:
                    step = 3;
                    max = 3;
                    break;
                case FlagType.Pointer32Bit:
                    step = 4;
                    max = 4;
                    break;
            }

            var bankSize = Data.GetBankSize();
            var myBank = offset / bankSize;
            
            var min = step; 
            while (
                min < max &&
                offset + min < size &&
                Data.GetFlag(offset + min) == Data.GetFlag(offset) &&
                Data.LabelProvider.GetLabelName(Data.ConvertPCtoSnes(offset + min)) == "" &&
                (offset + min) / bankSize == myBank
            ) min += step;
            return min;
        }
        
        public string GetFormattedBytes(int offset, int step, int bytes)
        {
            var res = step switch
            {
                1 => "db ",
                2 => "dw ",
                3 => "dl ",
                4 => "dd ",
                _ => ""
            };

            for (var i = 0; i < bytes; i += step)
            {
                if (i > 0) res += ",";

                switch (step)
                {
                    case 1: res += Util.NumberToBaseString(Data.GetRomByte(offset + i), Util.NumberBase.Hexadecimal, 2, true); break;
                    case 2: res += Util.NumberToBaseString(Data.GetRomWord(offset + i), Util.NumberBase.Hexadecimal, 4, true); break;
                    case 3: res += Util.NumberToBaseString(Data.GetRomLong(offset + i), Util.NumberBase.Hexadecimal, 6, true); break;
                    case 4: res += Util.NumberToBaseString(Data.GetRomDoubleWord(offset + i), Util.NumberBase.Hexadecimal, 8, true); break;
                }
            }

            return res;
        }
        
        public string GetPointer(int offset, int bytes)
        {
            var ia = -1;
            string format = "", param = "";
            switch (bytes)
            {
                case 2:
                    ia = (Data.GetDataBank(offset) << 16) | Data.GetRomWord(offset);
                    format = "dw {0}";
                    param = Util.NumberToBaseString(Data.GetRomWord(offset), Util.NumberBase.Hexadecimal, 4, true);
                    break;
                case 3:
                    ia = Data.GetRomLong(offset);
                    format = "dl {0}";
                    param = Util.NumberToBaseString(Data.GetRomLong(offset), Util.NumberBase.Hexadecimal, 6, true);
                    break;
                case 4:
                    ia = Data.GetRomLong(offset);
                    format = "dl {0}" +
                             $" : db {Util.NumberToBaseString(Data.GetRomByte(offset + 3), Util.NumberBase.Hexadecimal, 2, true)}";
                    param = Util.NumberToBaseString(Data.GetRomLong(offset), Util.NumberBase.Hexadecimal, 6, true);
                    break;
            }

            if (Data.ConvertSnesToPc(ia) >= 0)
            {
                var labelName = Data.LabelProvider.GetLabelName(ia);
                if (labelName != "") 
                    param = labelName;
            }
            
            return string.Format(format, param);
        }

        public string GetFormattedText(int offset, int bytes)
        {
            var text = "db \"";
            for (var i = 0; i < bytes; i++) text += (char)Data.GetRomByte(offset + i);
            return text + "\"";
        }

        public abstract class AssemblyPartialLineGenerator
        {
            public LogCreator LogCreator { get; protected internal set; }
            public ILogCreatorDataSource Data => LogCreator.Data;
            
            public string Token { get; protected set; } = "";
            public int DefaultLength { get; protected set; }
            
            public bool RequiresToken { get; protected set; } = true;
            public bool UsesOffset { get; protected set; } = true;

            public string Emit(int? offset, int length)
            {
                var finalLength = length;
                Prep(offset, ref finalLength);

                if (UsesOffset)
                {
                    Debug.Assert(offset != null);
                    return Generate(offset.Value, finalLength);
                }

                return Generate(finalLength);
            }

            protected virtual string Generate(int length)
            {
                throw new InvalidDataException("Invalid Generate() call: Can't call without an offset.");
            }
            
            protected virtual string Generate(int offset, int length)
            {
                // NOTE: if you get here (without an override in a derived class)
                // it means the client code should have instead been calling the other Generate(length) overload
                // directly. for now, we'll gracefully handle it, but client code should try and be better about it
                // eventually.
                
                return Generate(length);
                // throw new InvalidDataException("Invalid Generate() call: Can't call with offset.");
            }
            
            // call Prep() before doing anything in each Emit()
            // if length is non-zero, use that as our length, if not we use the default length
            protected virtual void Prep(int? offset, ref int length)
            {
                if (length == 0 && DefaultLength == 0)
                    throw new InvalidDataException("Assembly output component needed a length but received none.");
                
                // set the length
                length = length != 0 ? length : DefaultLength;
                
                if (RequiresToken && string.IsNullOrEmpty(Token))
                    throw new InvalidDataException("Assembly output component needed a token but received none.");

                // we should throw exceptions both ways, for now though we'll let it slide if we were passed in
                // an offset and we don't need it.
                var hasOffset = offset != null;
                if (UsesOffset && UsesOffset != hasOffset)
                    throw new InvalidDataException(UsesOffset 
                        ? "Assembly output component needed an offset but received none."
                        : "Assembly output component doesn't use an offset but we were provided one anyway.");
            }
        }

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
                // TODO: we need to deal with that mirroring here
                // TODO: eventually, support multiple labels tagging the same address, it may not always be just one.
            
                var snesOffset = Data.ConvertPCtoSnes(offset); 
                var label = Data.LabelProvider.GetLabelName(snesOffset);
                if (label == null)
                    return "";
            
                LogCreator.LabelsWeVisited.Add(snesOffset);

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
                        code = LogCreator.GetFormattedBytes(offset, 1, bytes);
                        break;
                    case FlagType.Data16Bit:
                        code = LogCreator.GetFormattedBytes(offset, 2, bytes);
                        break;
                    case FlagType.Data24Bit:
                        code = LogCreator.GetFormattedBytes(offset, 3, bytes);
                        break;
                    case FlagType.Data32Bit:
                        code = LogCreator.GetFormattedBytes(offset, 4, bytes);
                        break;
                    case FlagType.Pointer16Bit:
                        code = LogCreator.GetPointer(offset, 2);
                        break;
                    case FlagType.Pointer24Bit:
                        code = LogCreator.GetPointer(offset, 3);
                        break;
                    case FlagType.Pointer32Bit:
                        code = LogCreator.GetPointer(offset, 4);
                        break;
                    case FlagType.Text:
                        code = LogCreator.GetFormattedText(offset, bytes);
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
                var s = "incsrc \"labels.asm\"";
                if (offset >= 0)
                {
                    var bank = Data.ConvertPCtoSnes(offset) >> 16;
                    s = $"incsrc \"bank_{Util.NumberToBaseString(bank, Util.NumberBase.Hexadecimal, 2)}.asm\"";
                }
                return Util.LeftAlign(length, s);
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
                var bytes = "";
                if (Data.GetFlag(offset) == FlagType.Opcode)
                {
                    for (var i = 0; i < Data.GetInstructionLength(offset); i++)
                    {
                        bytes += Util.NumberToBaseString(Data.GetRomByte(offset + i), Util.NumberBase.Hexadecimal);
                    }
                }
                // TODO: FIXME: use 'length here'
                return $"{bytes,-8}";
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
        public class AssemblyGenerateLabelAssign : AssemblyPartialLineGenerator
        {
            public AssemblyGenerateLabelAssign()
            {
                Token = "%labelassign";
                DefaultLength = 1;
            }
            protected override string Generate(int offset, int length)
            {
                var snesAddress = Data.ConvertPCtoSnes(offset);
                var labelName = Data.LabelProvider.GetLabelName(snesAddress);
                var offsetStr = Util.NumberToBaseString(offset, Util.NumberBase.Hexadecimal, 6, true);
                var labelComment = Data.LabelProvider.GetLabelComment(snesAddress);

                if (string.IsNullOrEmpty(labelName))
                    return "";

                labelComment ??= "";

                var finalCommentText = "";

                // TODO: probably not the best way to stuff this in here. -Dom
                // we should consider putting this in the %comment% section in the future.
                // for now, just hacking this in so it's included somewhere. this option defaults to OFF
                if (LogCreator.Settings.PrintLabelSpecificComments && labelComment != "")
                    finalCommentText = $"; !^ {labelComment} ^!";

                var str = $"{labelName} = {offsetStr}{finalCommentText}";
                return Util.LeftAlign(length, str);
            }
        }

        public Dictionary<string, AssemblyPartialLineGenerator> CreateAssemblyGenerators()
        {
            var list = new List<AssemblyPartialLineGenerator>
            {
                new AssemblyGeneratePercent(),
                new AssemblyGenerateEmpty(),
                new AssemblyGenerateLabel(),
                new AssemblyGenerateCode(),
                new AssemblyGenerateOrg(),
                new AssemblyGenerateMap(),
                new AssemblyGenerateIncSrc(),
                new AssemblyGenerateBankCross(),
                new AssemblyGenerateIndirectAddress(),
                new AssemblyGenerateProgramCounter(),
                new AssemblyGenerateOffset(),
                new AssemblyGenerateDataBytes(),
                new AssemblyGenerateComment(),
                new AssemblyGenerateDataBank(),
                new AssemblyGenerateDirectPage(),
                new AssemblyGenerateMFlag(),
                new AssemblyGenerateXFlag(),
                new AssemblyGenerateLabelAssign(),
            };

            var dict = new Dictionary<string, AssemblyPartialLineGenerator>();
            foreach (var generator in list)
            {
                generator.LogCreator = this;
                dict.Add(generator.Token, generator);
            }

            return dict;
        }
    }

    #endregion
}

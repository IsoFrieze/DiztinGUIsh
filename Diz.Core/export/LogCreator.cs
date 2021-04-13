using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Core.export
{
    public class LogCreator
    {
        public LogWriterSettings Settings { get; init; }
        public ILogCreatorDataSource Data { get; init; }
        private LogCreatorOutput Output { get; set; }
        
        protected internal List<int> LabelsWeVisited { get; private set; } // snes addresses
        
        private LogCreatorTempLabelGenerator LogCreatorTempLabelGenerator { get; set; }
        private LogCreatorLineFormatter LogCreatorLineFormatter { get; set; }

        private Dictionary<string, AssemblyPartialLineGenerator> Generators { get; set; }
        
        private int errorCount;
        
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

        public virtual LogCreatorOutput.OutputResult CreateLog()
        {
            Init();
            
            GenerateAssemblyOutput();

            var result = GetResult();
            CloseOutput(result);

            return result;
        }

        private void GenerateAssemblyOutput()
        {
            try
            {
                LogCreatorTempLabelGenerator?.GenerateTemporaryLabels();
                
                WriteLog();
            }
            finally
            {
                // MODIFIES UNDERLYING DATA. WE MUST ALWAYS MAKE SURE TO UNDO THIS
                LogCreatorTempLabelGenerator?.ClearTemporaryLabels();
            }
        }

        protected virtual void Init()
        {
            Debug.Assert(Settings.RomSizeOverride == -1 || Settings.RomSizeOverride <= Data.GetRomSize());

            InitOutput();

            Generators = CreateAssemblyGenerators();
            
            LogCreatorLineFormatter = new LogCreatorLineFormatter(Settings.Format, Generators);
            
            errorCount = 0;
            LabelsWeVisited = new List<int>();
            
            InitTempLabelGenerator();
        }

        private void InitTempLabelGenerator()
        {
            if (Settings.Unlabeled == LogWriterSettings.FormatUnlabeled.ShowNone)
                return;

            LogCreatorTempLabelGenerator = new LogCreatorTempLabelGenerator
            {
                LogCreator = this, GenerateAllUnlabeled = Settings.Unlabeled == LogWriterSettings.FormatUnlabeled.ShowAll,
            };
        }

        private void InitOutput()
        {
            Output = Settings.OutputToString ? new LogCreatorStringOutput() : Output = new LogCreatorStreamOutput();
            Output.Init(this);
        }

        private void CloseOutput(LogCreatorOutput.OutputResult result)
        {
            Output?.Finish(result);
            Output = null;
        }

        private LogCreatorOutput.OutputResult GetResult()
        {
            var result = new LogCreatorOutput.OutputResult
            {
                ErrorCount = errorCount,
                Success = true,
                LogCreator = this
            };

            if (Settings.OutputToString)
                result.OutputStr = ((LogCreatorStringOutput) Output)?.OutputString;

            return result;
        }

        private void ReportError(int offset, string msg)
        {
            ++errorCount;
            var offsetMsg = offset >= 0 ? $" Offset 0x{offset:X}" : "";
            Output.WriteErrorLine($"({errorCount}){offsetMsg}: {msg}");
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
            if (Settings.Structure != LogWriterSettings.FormatStructure.OneBankPerFile)
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

            const int ignored = 0;
            Output.WriteLine(GenerateLine(ignored, "empty"));
        }

        private void WriteBlankLineIfStartingNewParagraph(int pointer)
        {
            if (!IsLocationAReadPoint(pointer) && !AnyLabelsPresent(pointer))
                return;

            const int ignored = 0;
            Output.WriteLine(GenerateLine(ignored, "empty"));
        }

        private bool IsLocationPoint(int pointer, InOutPoint mustHaveFlag) =>
            (Data.GetInOutPoint(pointer) & mustHaveFlag) != 0;

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
            var unvisitedLabels = new Dictionary<int, IReadOnlyLabel>(); // snes addresses

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
            if (Settings.Structure != LogWriterSettings.FormatStructure.SingleFile)
                return;

            const int ignored = 0;
            Output.WriteLine(GenerateLine(ignored, "empty"));
        }

        // --------------------------

        #region WriteOperations

        private string GenerateLine(int offset, string specialStr)
        {
            var line = "";

            foreach (var formatItem in LogCreatorLineFormatter.ParsedFormat)
            {
                line += GenerateLinePartial(offset, formatItem, specialStr);
            }

            if (specialStr == null)
                CheckForErrorsAt(offset);

            return line;
        }

        private string GenerateLinePartial(int offset, LogCreatorLineFormatter.FormatItem formatItem, string specialModifierStr)
        {
            // string literal version
            if (formatItem.IsLiteral)
                return formatItem.Value;
            
            var generatorName = GetGeneratorName(formatItem, specialModifierStr);

            var generator = GetGeneratorFor(generatorName);
            
            return generator.Emit(offset, formatItem.LengthOverride);
        }

        private static string GetGeneratorName(LogCreatorLineFormatter.FormatItem formatItem, string generatorOverrideIfCode = null)
        {
            // normal non-special case
            if (generatorOverrideIfCode == null)
                return formatItem.Value;

            return GetOverrideGeneratorNameIfCode(formatItem.Value, generatorOverrideIfCode);
        }

        private static string GetOverrideGeneratorNameIfCode(string nameToOverride, string generatorOverrideIfCode)
        {
            if (nameToOverride != "code")
                return "%empty";

            return $"%{generatorOverrideIfCode}";
        }

        private AssemblyPartialLineGenerator GetGeneratorFor(string parameter)
        {
            if (!Generators.TryGetValue(parameter, out var generator))
                throw new InvalidOperationException($"Can't find generator for {parameter}");
            
            return generator;
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
                    case 1:
                        res += Util.NumberToBaseString(Data.GetRomByte(offset + i), Util.NumberBase.Hexadecimal, 2,
                            true);
                        break;
                    case 2:
                        res += Util.NumberToBaseString(Data.GetRomWord(offset + i), Util.NumberBase.Hexadecimal, 4,
                            true);
                        break;
                    case 3:
                        res += Util.NumberToBaseString(Data.GetRomLong(offset + i), Util.NumberBase.Hexadecimal, 6,
                            true);
                        break;
                    case 4:
                        res += Util.NumberToBaseString(Data.GetRomDoubleWord(offset + i), Util.NumberBase.Hexadecimal,
                            8, true);
                        break;
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
            for (var i = 0; i < bytes; i++) text += (char) Data.GetRomByte(offset + i);
            return text + "\"";
        }

        public Dictionary<string, AssemblyPartialLineGenerator> CreateAssemblyGenerators()
        {
            var generators = AssemblyGeneratorRegistration.Create();
            generators.ForEach(kvp => kvp.Value.LogCreator = this);
            return generators;
        }

        public static bool ValidateFormatStr(string formatStr)
        {
            // this is not really a good way to do this, too much mem alloc
            // see if we can do it statically instead using data on the generators
            var logCreator = new LogCreator();
            var generators = logCreator.CreateAssemblyGenerators();

            try
            {
                var unused = new LogCreatorLineFormatter(formatStr, generators);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    #endregion
}

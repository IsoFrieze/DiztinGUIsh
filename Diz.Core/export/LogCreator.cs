using System;
using System.Collections.Generic;
using System.Diagnostics;
using Diz.Core.model;
using Diz.Core.util;
using IX.Observable;

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

        public LogWriterSettings Settings { get; set; }
        public Data Data { get; set; }

        protected LogCreatorOutput Output;
        protected Dictionary<int, Label> ExtraLabels { get; set; } = new Dictionary<int, Label>();
        protected List<Tuple<string, int>> ParseList;
        protected List<int> LabelsWeVisited;
        protected int BankSize;
        protected ObservableDictionary<int, Label> BackupOfOriginalLabelsBeforeModifying;

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

        protected virtual void Init()
        {
            InitOutput();
            SetupParseList();

            BankSize = RomUtil.GetBankSize(Data.RomMapMode);
            ErrorCount = 0;
            LabelsWeVisited = new List<int>();

            GenerateAdditionalExtraLabels();
            WriteGeneratedLabelsIntoUnderlyingData(); // MODIFIES DATA. MAKE SURE TO UNDO THIS.
        }

        private void InitOutput()
        {
            if (Settings.OutputToString)
                Output = new LogCreatorStringOutput();
            else
                Output = new LogCreatorStreamOutput();

            Output.Init(this);
        }

        protected virtual void Cleanup()
        {
            // restore original labels. SUPER IMPORTANT THIS HAPPENS WHen WE'RE DONE
            // TODO: make this unnecessary by not modifying the underlying data.
            RestoreUnderlyingDataLabels();
        }

        protected void RestoreUnderlyingDataLabels()
        {
            // SUPER IMPORTANT. THIS MUST GET DONE, ALWAYS. PROTECT THIS WITH TRY/CATCH

            if (BackupOfOriginalLabelsBeforeModifying != null)
                Data.Labels = BackupOfOriginalLabelsBeforeModifying;

            BackupOfOriginalLabelsBeforeModifying = null;
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
                ErrorCount = ErrorCount,
                Success = true,
                LogCreator = this
            };

            if (Settings.OutputToString)
                result.OutputStr = ((LogCreatorStringOutput)Output)?.OutputString;

            return result;
        }

        protected void WriteGeneratedLabelsIntoUnderlyingData()
        {
            // WARNING: THIS MODIFIES THE UNDERLYING DATA TO ADD MORE LABELS
            // *** if not properly cleaned up, the original project file's label list can get trashed. ***
            // always call this FN with a try/finally that cleans up after.

            // TODO: I really don't like us modifying and restoring the
            // underlying labels or anything in Data. Data should ideally be immutable by us.
            // we should either clone all of Data before modifying, or generate these labels on the fly.
            BackupOfOriginalLabelsBeforeModifying = Data.Labels;
            Data.Labels = new ObservableDictionary<int, Label>(Data.Labels);

            // write the new generated labels in, don't let them overwrite any real labels
            // i.e. if the user defined a label like "PlayerSwimmingSprites", and our auto-generated
            // labels also contain a label at the same address, then ignore our auto-generated label,
            // only use the explicit user-created label.
            foreach (var label in ExtraLabels)
            {
                Data.AddLabel(label.Key, label.Value, false);
            }
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

        protected void WriteSpecial(string special)
        {
            const int doesntMatter = 0;
            var line = GetLine(doesntMatter, special);
            Output.WriteLine(line);
        }

        protected void WriteMainIncludes(int size)
        {
            WriteSpecial("map");
            WriteSpecial("empty");
            WriteMainBankIncludes(size);
        }

        private void WriteMainBankIncludes(int size)
        {
            if (Settings.Structure != FormatStructure.OneBankPerFile)
                return;

            for (var i = 0; i < size; i += BankSize)
                Output.WriteLine(GetLine(i, "incsrc"));

            Output.WriteLine(GetLine(-1, "incsrc"));
        }

        protected void SetupParseList()
        {
            var split = Settings.Format.Split('%');
            ParseList = new List<Tuple<string, int>>();
            for (var i = 0; i < split.Length; i++)
            {
                if (i % 2 == 0) ParseList.Add(Tuple.Create(split[i], int.MaxValue));
                else
                {
                    var colon = split[i].IndexOf(':');

                    Tuple<string, int> tuple;
                    if (colon < 0)
                    {
                        var s1 = split[i];
                        var succeeded = Parameters.TryGetValue(s1, out var x);
                        Debug.Assert(succeeded);
                        var s2 = x.Item2;
                        
                        tuple = new Tuple<string, int>(s1, s2);
                    }
                    else
                    {
                        tuple = new Tuple<string, int>(split[i].Substring(0, colon), int.Parse(split[i].Substring(colon + 1)));
                    }

                    ParseList.Add(tuple);
                }
            }
        }

        protected void WriteAddress(ref int pointer, ref int currentBank)
        {
            SwitchBanksIfNeeded(pointer, ref currentBank);

            WriteBlankLineIfStartingNewParagraph(pointer);
            WriteTheRealLine(pointer);
            WriteBlankLineIfEndPoint(pointer);

            pointer += GetLineByteLength(pointer);
        }

        private void WriteTheRealLine(int pointer)
        {
            Output.WriteLine(GetLine(pointer, null));
        }

        private void WriteBlankLineIfEndPoint(int pointer)
        {
            if ((Data.GetInOutPoint(pointer) & InOutPoint.EndPoint) != 0)
                Output.WriteLine(GetLine(pointer, "empty"));
        }

        private void WriteBlankLineIfStartingNewParagraph(int pointer)
        {
            var isLocationAReadPoint = (Data.GetInOutPoint(pointer) & InOutPoint.ReadPoint) != 0;
            var anyLabelsPresent = Data.Labels.TryGetValue(pointer, out var label) && label.Name.Length > 0;

            if (isLocationAReadPoint || anyLabelsPresent)
                Output.WriteLine(GetLine(pointer, "empty"));
        }

        private void SwitchBanksIfNeeded(int pointer, ref int currentBank)
        {
            var snesAddress = Data.ConvertPCtoSnes(pointer);

            var thisBank = snesAddress >> 16;

            if (thisBank == currentBank)
                return;

            OpenNewBank(pointer, thisBank);
            currentBank = thisBank;

            if (snesAddress % BankSize == 0) 
                return;

            ReportError(pointer, "An instruction crossed a bank boundary.");
        }

        private void OpenNewBank(int pointer, int thisBank)
        {
            Output.SwitchToBank(thisBank);

            Output.WriteLine(GetLine(pointer, "empty"));
            Output.WriteLine(GetLine(pointer, "org"));
            Output.WriteLine(GetLine(pointer, "empty"));
        }

        protected void WriteLabels(int pointer)
        {
            var unvisitedLabels = GetUnvisitedLabels();
            WriteAnyUnivisitedLabels(pointer, unvisitedLabels);
            PrintAllLabelsIfRequested(pointer, unvisitedLabels);
        }

        private Dictionary<int, Label> GetUnvisitedLabels()
        {
            var unvisitedLabels = new Dictionary<int, Label>();

            // part 1: important: include all labels we aren't defining somewhere else. needed for disassembly
            foreach (var pair in Data.Labels)
            {
                if (LabelsWeVisited.Contains(pair.Key))
                    continue;

                // this label was not defined elsewhere in our disassembly, so we need to include it in labels.asm
                unvisitedLabels.Add(pair.Key, pair.Value);
            }

            return unvisitedLabels;
        }

        private void WriteAnyUnivisitedLabels(int pointer, Dictionary<int, Label> unvisitedLabels)
        {
            SwitchOutputStream(pointer, "labels");

            foreach (var pair in unvisitedLabels)
                Output.WriteLine(GetLine(pair.Key, "labelassign"));
        }

        private void PrintAllLabelsIfRequested(int pointer, Dictionary<int, Label> unvisitedLabels)
        {
            // part 2: optional: if requested, print all labels regardless of use.
            // Useful for debugging, documentation, or reverse engineering workflow.
            // this file shouldn't need to be included in the build, it's just reference documentation

            if (!Settings.IncludeUnusedLabels) 
                return;

            SwitchOutputStream(pointer, "all-labels.txt"); // TODO: csv in the future. escape commas
            foreach (var pair in Data.Labels)
            {
                // not the best place to add formatting, TODO: cleanup
                var category = unvisitedLabels.ContainsKey(pair.Key) ? "UNUSED" : "USED";
                Output.WriteLine($";!^!-{category}-! " + GetLine(pair.Key, "labelassign"));
            }
        }

        protected void SwitchOutputStream(int pointer, string streamName)
        {
            Output.SwitchToStream(streamName);

            // write an extra blank line if we would normally switch files here
            if (Settings.Structure == FormatStructure.SingleFile)
                Output.WriteLine(GetLine(pointer, "empty"));
        }

        // --------------------------
        #region WriteOperations

        protected string GetLine(int offset, string special)
        {
            if (special == "empty" && !Settings.OutputExtraWhitespace)
                return "";
            
            var isSpecial = special != null;
            var line = "";

            foreach (var t in ParseList)
            {
                if (t.Item2 == int.MaxValue) // string literal
                {
                    line += t.Item1;
                }
                else if (isSpecial) // special parameter (replaces code & everything else = empty)
                {
                    var v1 = (t.Item1 == "code" ? special : "empty");
                    line += GetParameter(offset, $"%{v1}", t.Item2);
                }
                else // normal parameter
                {
                    line += GetParameter(offset, t.Item1, t.Item2);
                }
            }

            if (!isSpecial)
                CheckForErrorsAtLine(offset);
            
            return line;
        }

        private void CheckForErrorsAtLine(int offset)
        {
            // throw out some errors if stuff looks fishy
            ErrorIfOperand(offset);
            ErrorIfAdjacentOperandsSeemWrong(offset);
            ErrorIfBranchToNonInstruction(offset);
        }

        private FlagType GetFlagButSwapOpcodeForOperand(int offset)
        {
            var flag = Data.GetFlag(offset);
            if (flag == FlagType.Opcode)
                return FlagType.Operand;

            return flag;
        }

        protected int GetLineByteLength(int offset)
        {
            int max = 1, step = 1;
            var size = Data.GetRomSize();

            switch (Data.GetFlag(offset))
            {
                case FlagType.Opcode:
                    return Data.OpcodeByteLength(offset);
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

            int min = step, myBank = offset / BankSize;
            while (
                min < max &&
                offset + min < size &&
                Data.GetFlag(offset + min) == Data.GetFlag(offset) &&
                Data.GetLabelName(Data.ConvertPCtoSnes(offset + min)) == "" &&
                (offset + min) / BankSize == myBank
            ) min += step;
            return min;
        }

        // just a %
        [AssemblerHandler(Token = "", Length = 1)]
        protected string GetPercent(int offset, int length)
        {
            return "%";
        }

        // all spaces
        [AssemblerHandler(Token = "%empty", Length = 1)]
        protected string GetEmpty(int offset, int length)
        {
            return string.Format("{0," + length + "}", "");
        }

        // trim to length
        // negative length = right justified
        [AssemblerHandler(Token = "label", Length = -22)]
        protected string GetLabel(int offset, int length)
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
            var label = Data.GetLabelName(snesOffset);
            if (label == null)
                return "";
            
            LabelsWeVisited.Add(snesOffset);

            var noColon = label.Length == 0 || label[0] == '-' || label[0] == '+';
            return string.Format("{0," + (length * -1) + "}", label + (noColon ? "" : ":"));
        }

        // trim to length
        [AssemblerHandler(Token = "code", Length = 37)]
        protected string GetCode(int offset, int length)
        {
            var bytes = GetLineByteLength(offset);
            string code = "";

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
                    code = Data.GetPointer(offset, 2);
                    break;
                case FlagType.Pointer24Bit:
                    code = Data.GetPointer(offset, 3);
                    break;
                case FlagType.Pointer32Bit:
                    code = Data.GetPointer(offset, 4);
                    break;
                case FlagType.Text:
                    code = Data.GetFormattedText(offset, bytes);
                    break;
            }

            return string.Format("{0," + (length * -1) + "}", code);
        }

        [AssemblerHandler(Token = "%org", Length = 37)]
        protected string GetOrg(int offset, int length)
        {
            string org = "ORG " + Util.NumberToBaseString(Data.ConvertPCtoSnes(offset), Util.NumberBase.Hexadecimal, 6, true);
            return string.Format("{0," + (length * -1) + "}", org);
        }

        [AssemblerHandler(Token = "%map", Length = 37)]
        protected string GetMap(int offset, int length)
        {
            string s = "";
            switch (Data.RomMapMode)
            {
                case RomMapMode.LoRom: s = "lorom"; break;
                case RomMapMode.HiRom: s = "hirom"; break;
                case RomMapMode.Sa1Rom: s = "sa1rom"; break; // todo
                case RomMapMode.ExSa1Rom: s = "exsa1rom"; break; // todo
                case RomMapMode.SuperFx: s = "sfxrom"; break; // todo
                case RomMapMode.ExHiRom: s = "exhirom"; break;
                case RomMapMode.ExLoRom: s = "exlorom"; break;
            }
            return string.Format("{0," + (length * -1) + "}", s);
        }

        // 0+ = bank_xx.asm, -1 = labels.asm
        [AssemblerHandler(Token = "%incsrc", Length = 1)]
        protected string GetIncSrc(int offset, int length)
        {
            string s = "incsrc \"labels.asm\"";
            if (offset >= 0)
            {
                int bank = Data.ConvertPCtoSnes(offset) >> 16;
                s = string.Format("incsrc \"bank_{0}.asm\"", Util.NumberToBaseString(bank, Util.NumberBase.Hexadecimal, 2));
            }
            return string.Format("{0," + (length * -1) + "}", s);
        }

        [AssemblerHandler(Token = "%bankcross", Length = 1)]
        protected string GetBankCross(int offset, int length)
        {
            string s = "check bankcross off";
            return string.Format("{0," + (length * -1) + "}", s);
        }

        // length forced to 6
        [AssemblerHandler(Token = "ia", Length = 6)]
        protected string GetIntermediateAddress(int offset, int length)
        {
            int ia = Data.GetIntermediateAddressOrPointer(offset);
            return ia >= 0 ? Util.NumberToBaseString(ia, Util.NumberBase.Hexadecimal, 6) : "      ";
        }

        // length forced to 6
        [AssemblerHandler(Token = "pc", Length = 6)]
        protected string GetProgramCounter(int offset, int length)
        {
            return Util.NumberToBaseString(Data.ConvertPCtoSnes(offset), Util.NumberBase.Hexadecimal, 6);
        }

        // trim to length
        [AssemblerHandler(Token = "offset", Length = -6)]
        protected string GetOffset(int offset, int length)
        {
            return string.Format("{0," + (length * -1) + "}", Util.NumberToBaseString(offset, Util.NumberBase.Hexadecimal, 0));
        }

        // length forced to 8
        [AssemblerHandler(Token = "bytes", Length = 8)]
        protected string GetRawBytes(int offset, int length)
        {
            string bytes = "";
            if (Data.GetFlag(offset) == FlagType.Opcode)
            {
                for (var i = 0; i < Data.GetInstructionLength(offset); i++)
                {
                    bytes += Util.NumberToBaseString(Data.GetRomByte(offset + i), Util.NumberBase.Hexadecimal);
                }
            }
            return $"{bytes,-8}";
        }

        // trim to length
        [AssemblerHandler(Token = "comment", Length = 1)]
        protected string GetComment(int offset, int length)
        {
            var snesOffset = Data.ConvertPCtoSnes(offset);
            return string.Format("{0," + (length * -1) + "}", Data.GetComment(snesOffset));
        }

        // length forced to 2
        [AssemblerHandler(Token = "b", Length = 2)]
        protected string GetDataBank(int offset, int length)
        {
            return Util.NumberToBaseString(Data.GetDataBank(offset), Util.NumberBase.Hexadecimal, 2);
        }

        // length forced to 4
        [AssemblerHandler(Token = "d", Length = 4)]
        protected string GetDirectPage(int offset, int length)
        {
            return Util.NumberToBaseString(Data.GetDirectPage(offset), Util.NumberBase.Hexadecimal, 4);
        }

        // if length == 1, M/m, else 08/16
        [AssemblerHandler(Token = "m", Length = 1)]
        protected string GetMFlag(int offset, int length)
        {
            var m = Data.GetMFlag(offset);
            if (length == 1) return m ? "M" : "m";
            else return m ? "08" : "16";
        }

        // if length == 1, X/x, else 08/16
        [AssemblerHandler(Token = "x", Length = 1)]
        protected string GetXFlag(int offset, int length)
        {
            var x = Data.GetXFlag(offset);
            if (length == 1) return x ? "X" : "x";
            else return x ? "08" : "16";
        }

        // output label at snes offset, and its value
        [AssemblerHandler(Token = "%labelassign", Length = 1)]
        protected string GetLabelAssign(int offset, int length)
        {
            var labelName = Data.GetLabelName(offset);
            var offsetStr = Util.NumberToBaseString(offset, Util.NumberBase.Hexadecimal, 6, true);
            var labelComment = Data.GetLabelComment(offset);

            if (string.IsNullOrEmpty(labelName))
                return "";

            labelComment ??= "";

            var finalCommentText = "";

            // TODO: sorry, probably not the best way to stuff this in here, consider putting it in the %comment% section in the future. -Dom
            if (Settings.PrintLabelSpecificComments && labelComment != "")
                finalCommentText = $"; !^ {labelComment} ^!";

            string s = $"{labelName} = {offsetStr}{finalCommentText}";
            return string.Format("{0," + (length * -1) + "}", s);
        }
    }

    #endregion
}

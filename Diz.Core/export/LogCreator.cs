using System;
using System.Collections.Generic;
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

        protected LogCreatorOutput output;
        protected Dictionary<int, Label> ExtraLabels { get; set; } = new Dictionary<int, Label>();
        protected List<Tuple<string, int>> parseList;
        protected List<int> labelsWeVisited;
        protected int bankSize;
        protected ObservableDictionary<int, Label> backupOfOriginalLabelsBeforeModifying;

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

            bankSize = RomUtil.GetBankSize(Data.RomMapMode);
            errorCount = 0;
            labelsWeVisited = new List<int>();

            GenerateAdditionalExtraLabels();
            WriteGeneratedLabelsIntoUnderlyingData(); // MODIFIES DATA. MAKE SURE TO UNDO THIS.
        }

        private void InitOutput()
        {
            if (Settings.outputToString)
                output = new LogCreatorStringOutput();
            else
                output = new LogCreatorStreamOutput();

            output.Init(this);
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

            if (backupOfOriginalLabelsBeforeModifying != null)
                Data.Labels.Dict = backupOfOriginalLabelsBeforeModifying;

            backupOfOriginalLabelsBeforeModifying = null;
        }


        private void CloseOutput(OutputResult result)
        {
            output?.Finish(result);
            output = null;
        }

        private OutputResult GetResult()
        {
            var result = new OutputResult()
            {
                error_count = errorCount,
                success = true,
                logCreator = this
            };

            if (Settings.outputToString)
                result.outputStr = ((LogCreatorStringOutput)output)?.OutputString;

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
            backupOfOriginalLabelsBeforeModifying = Data.Labels.Dict;
            Data.Labels.Dict = new ObservableDictionary<int, Label>(Data.Labels.Dict);

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
            return Settings.romSizeOverride != -1 ? Settings.romSizeOverride : Data.GetROMSize();
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
            output.WriteLine(line);
        }

        protected void WriteMainIncludes(int size)
        {
            WriteSpecial("map");
            WriteSpecial("empty");
            WriteMainBankIncludes(size);
        }

        private void WriteMainBankIncludes(int size)
        {
            if (Settings.structure != FormatStructure.OneBankPerFile)
                return;

            for (var i = 0; i < size; i += bankSize)
                output.WriteLine(GetLine(i, "incsrc"));

            output.WriteLine(GetLine(-1, "incsrc"));
        }

        protected void SetupParseList()
        {
            var split = Settings.format.Split('%');
            parseList = new List<Tuple<string, int>>();
            for (var i = 0; i < split.Length; i++)
            {
                if (i % 2 == 0) parseList.Add(Tuple.Create(split[i], int.MaxValue));
                else
                {
                    var colon = split[i].IndexOf(':');

                    Tuple<string, int> tuple;

                    if (colon < 0)
                    {
                        var s1 = split[i];
                        var s2 = Parameters[s1].Item2;
                        tuple = Tuple.Create(s1, s2);
                    }
                    else
                    {
                        tuple = Tuple.Create(split[i].Substring(0, colon), int.Parse(split[i].Substring(colon + 1)));
                    }

                    parseList.Add(tuple);
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
            output.WriteLine(GetLine(pointer, null));
        }

        private void WriteBlankLineIfEndPoint(int pointer)
        {
            if ((Data.GetInOutPoint(pointer) & Data.InOutPoint.EndPoint) != 0)
                output.WriteLine(GetLine(pointer, "empty"));
        }

        private void WriteBlankLineIfStartingNewParagraph(int pointer)
        {
            var isLocationAReadPoint = (Data.GetInOutPoint(pointer) & Data.InOutPoint.ReadPoint) != 0;
            var anyLabelsPresent = Data.Labels.Dict.TryGetValue(pointer, out var label) && label.name.Length > 0;

            if (isLocationAReadPoint || anyLabelsPresent)
                output.WriteLine(GetLine(pointer, "empty"));
        }

        private void SwitchBanksIfNeeded(int pointer, ref int currentBank)
        {
            var snesAddress = Data.ConvertPCtoSNES(pointer);

            var thisBank = snesAddress >> 16;

            if (thisBank == currentBank)
                return;

            OpenNewBank(pointer, thisBank);
            currentBank = thisBank;

            if (snesAddress % bankSize == 0) 
                return;

            ReportError(pointer, "An instruction crossed a bank boundary.");
        }

        private void OpenNewBank(int pointer, int thisBank)
        {
            output.SwitchToBank(thisBank);

            output.WriteLine(GetLine(pointer, "empty"));
            output.WriteLine(GetLine(pointer, "org"));
            output.WriteLine(GetLine(pointer, "empty"));
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
            foreach (KeyValuePair<int, Label> pair in Data.Labels.Dict)
            {
                if (labelsWeVisited.Contains(pair.Key))
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
                output.WriteLine(GetLine(pair.Key, "labelassign"));
        }

        private void PrintAllLabelsIfRequested(int pointer, Dictionary<int, Label> unvisitedLabels)
        {
            // part 2: optional: if requested, print all labels regardless of use.
            // Useful for debugging, documentation, or reverse engineering workflow.
            // this file shouldn't need to be included in the build, it's just reference documentation

            if (!Settings.includeUnusedLabels) 
                return;

            SwitchOutputStream(pointer, "all-labels.txt"); // TODO: csv in the future. escape commas
            foreach (KeyValuePair<int, Label> pair in Data.Labels.Dict)
            {
                // not the best place to add formatting, TODO: cleanup
                var category = unvisitedLabels.ContainsKey(pair.Key) ? "UNUSED" : "USED";
                output.WriteLine($";!^!-{category}-! " + GetLine(pair.Key, "labelassign"));
            }
        }

        protected void SwitchOutputStream(int pointer, string streamName)
        {
            output.SwitchToStream(streamName);

            // write an extra blank line if we would normally switch files here
            if (Settings.structure == FormatStructure.SingleFile)
                output.WriteLine(GetLine(pointer, "empty"));
        }

        // --------------------------
        #region WriteOperations

        protected string GetLine(int offset, string special)
        {
            var isSpecial = special != null;
            var line = "";

            foreach (var t in parseList)
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

        private Data.FlagType GetFlagButSwapOpcodeForOperand(int offset)
        {
            var flag = Data.GetFlag(offset);
            if (flag == Data.FlagType.Opcode)
                return Data.FlagType.Operand;

            return flag;
        }

        protected int GetLineByteLength(int offset)
        {
            int max = 1, step = 1;
            var size = Data.GetROMSize();

            switch (Data.GetFlag(offset))
            {
                case Data.FlagType.Opcode:
                    return Data.OpcodeByteLength(offset);
                case Data.FlagType.Unreached:
                case Data.FlagType.Operand:
                case Data.FlagType.Data8Bit:
                case Data.FlagType.Graphics:
                case Data.FlagType.Music:
                case Data.FlagType.Empty:
                    max = Settings.dataPerLine;
                    break;
                case Data.FlagType.Text:
                    max = 21;
                    break;
                case Data.FlagType.Data16Bit:
                    step = 2;
                    max = Settings.dataPerLine;
                    break;
                case Data.FlagType.Data24Bit:
                    step = 3;
                    max = Settings.dataPerLine;
                    break;
                case Data.FlagType.Data32Bit:
                    step = 4;
                    max = Settings.dataPerLine;
                    break;
                case Data.FlagType.Pointer16Bit:
                    step = 2;
                    max = 2;
                    break;
                case Data.FlagType.Pointer24Bit:
                    step = 3;
                    max = 3;
                    break;
                case Data.FlagType.Pointer32Bit:
                    step = 4;
                    max = 4;
                    break;
            }

            int min = step, myBank = offset / bankSize;
            while (
                min < max &&
                offset + min < size &&
                Data.GetFlag(offset + min) == Data.GetFlag(offset) &&
                Data.GetLabelName(Data.ConvertPCtoSNES(offset + min)) == "" &&
                (offset + min) / bankSize == myBank
            ) min += step;
            return min;
        }

        // just a %
        [AssemblerHandler(token = "", length = 1)]
        protected string GetPercent(int offset, int length)
        {
            return "%";
        }

        // all spaces
        [AssemblerHandler(token = "%empty", length = 1)]
        protected string GetEmpty(int offset, int length)
        {
            return string.Format("{0," + length + "}", "");
        }

        // trim to length
        // negative length = right justified
        [AssemblerHandler(token = "label", length = -22)]
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
            
            var snesOffset = Data.ConvertPCtoSNES(offset); 
            var label = Data.GetLabelName(snesOffset);
            if (label == null)
                return "";
            
            labelsWeVisited.Add(snesOffset);

            var noColon = label.Length == 0 || label[0] == '-' || label[0] == '+';
            return string.Format("{0," + (length * -1) + "}", label + (noColon ? "" : ":"));
        }

        // trim to length
        [AssemblerHandler(token = "code", length = 37)]
        protected string GetCode(int offset, int length)
        {
            var bytes = GetLineByteLength(offset);
            string code = "";

            switch (Data.GetFlag(offset))
            {
                case Data.FlagType.Opcode:
                    code = Data.GetInstruction(offset);
                    break;
                case Data.FlagType.Unreached:
                case Data.FlagType.Operand:
                case Data.FlagType.Data8Bit:
                case Data.FlagType.Graphics:
                case Data.FlagType.Music:
                case Data.FlagType.Empty:
                    code = Data.GetFormattedBytes(offset, 1, bytes);
                    break;
                case Data.FlagType.Data16Bit:
                    code = Data.GetFormattedBytes(offset, 2, bytes);
                    break;
                case Data.FlagType.Data24Bit:
                    code = Data.GetFormattedBytes(offset, 3, bytes);
                    break;
                case Data.FlagType.Data32Bit:
                    code = Data.GetFormattedBytes(offset, 4, bytes);
                    break;
                case Data.FlagType.Pointer16Bit:
                    code = Data.GetPointer(offset, 2);
                    break;
                case Data.FlagType.Pointer24Bit:
                    code = Data.GetPointer(offset, 3);
                    break;
                case Data.FlagType.Pointer32Bit:
                    code = Data.GetPointer(offset, 4);
                    break;
                case Data.FlagType.Text:
                    code = Data.GetFormattedText(offset, bytes);
                    break;
            }

            return string.Format("{0," + (length * -1) + "}", code);
        }

        [AssemblerHandler(token = "%org", length = 37)]
        protected string GetORG(int offset, int length)
        {
            string org = "ORG " + Util.NumberToBaseString(Data.ConvertPCtoSNES(offset), Util.NumberBase.Hexadecimal, 6, true);
            return string.Format("{0," + (length * -1) + "}", org);
        }

        [AssemblerHandler(token = "%map", length = 37)]
        protected string GetMap(int offset, int length)
        {
            string s = "";
            switch (Data.RomMapMode)
            {
                case Data.ROMMapMode.LoROM: s = "lorom"; break;
                case Data.ROMMapMode.HiROM: s = "hirom"; break;
                case Data.ROMMapMode.SA1ROM: s = "sa1rom"; break; // todo
                case Data.ROMMapMode.ExSA1ROM: s = "exsa1rom"; break; // todo
                case Data.ROMMapMode.SuperFX: s = "sfxrom"; break; // todo
                case Data.ROMMapMode.ExHiROM: s = "exhirom"; break;
                case Data.ROMMapMode.ExLoROM: s = "exlorom"; break;
            }
            return string.Format("{0," + (length * -1) + "}", s);
        }

        // 0+ = bank_xx.asm, -1 = labels.asm
        [AssemblerHandler(token = "%incsrc", length = 1)]
        protected string GetIncSrc(int offset, int length)
        {
            string s = "incsrc \"labels.asm\"";
            if (offset >= 0)
            {
                int bank = Data.ConvertPCtoSNES(offset) >> 16;
                s = string.Format("incsrc \"bank_{0}.asm\"", Util.NumberToBaseString(bank, Util.NumberBase.Hexadecimal, 2));
            }
            return string.Format("{0," + (length * -1) + "}", s);
        }

        [AssemblerHandler(token = "%bankcross", length = 1)]
        protected string GetBankCross(int offset, int length)
        {
            string s = "check bankcross off";
            return string.Format("{0," + (length * -1) + "}", s);
        }

        // length forced to 6
        [AssemblerHandler(token = "ia", length = 6)]
        protected string GetIntermediateAddress(int offset, int length)
        {
            int ia = Data.GetIntermediateAddressOrPointer(offset);
            return ia >= 0 ? Util.NumberToBaseString(ia, Util.NumberBase.Hexadecimal, 6) : "      ";
        }

        // length forced to 6
        [AssemblerHandler(token = "pc", length = 6)]
        protected string GetProgramCounter(int offset, int length)
        {
            return Util.NumberToBaseString(Data.ConvertPCtoSNES(offset), Util.NumberBase.Hexadecimal, 6);
        }

        // trim to length
        [AssemblerHandler(token = "offset", length = -6)]
        protected string GetOffset(int offset, int length)
        {
            return string.Format("{0," + (length * -1) + "}", Util.NumberToBaseString(offset, Util.NumberBase.Hexadecimal, 0));
        }

        // length forced to 8
        [AssemblerHandler(token = "bytes", length = 8)]
        protected string GetRawBytes(int offset, int length)
        {
            string bytes = "";
            if (Data.GetFlag(offset) == Data.FlagType.Opcode)
            {
                for (var i = 0; i < Data.GetInstructionLength(offset); i++)
                {
                    bytes += Util.NumberToBaseString(Data.GetROMByte(offset + i), Util.NumberBase.Hexadecimal);
                }
            }
            return $"{bytes,-8}";
        }

        // trim to length
        [AssemblerHandler(token = "comment", length = 1)]
        protected string GetComment(int offset, int length)
        {
            var snesOffset = Data.ConvertPCtoSNES(offset);
            return string.Format("{0," + (length * -1) + "}", Data.GetComment(snesOffset));
        }

        // length forced to 2
        [AssemblerHandler(token = "b", length = 2)]
        protected string GetDataBank(int offset, int length)
        {
            return Util.NumberToBaseString(Data.GetDataBank(offset), Util.NumberBase.Hexadecimal, 2);
        }

        // length forced to 4
        [AssemblerHandler(token = "d", length = 4)]
        protected string GetDirectPage(int offset, int length)
        {
            return Util.NumberToBaseString(Data.GetDirectPage(offset), Util.NumberBase.Hexadecimal, 4);
        }

        // if length == 1, M/m, else 08/16
        [AssemblerHandler(token = "m", length = 1)]
        protected string GetMFlag(int offset, int length)
        {
            var m = Data.GetMFlag(offset);
            if (length == 1) return m ? "M" : "m";
            else return m ? "08" : "16";
        }

        // if length == 1, X/x, else 08/16
        [AssemblerHandler(token = "x", length = 1)]
        protected string GetXFlag(int offset, int length)
        {
            var x = Data.GetXFlag(offset);
            if (length == 1) return x ? "X" : "x";
            else return x ? "08" : "16";
        }

        // output label at snes offset, and its value
        [AssemblerHandler(token = "%labelassign", length = 1)]
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
            if (Settings.printLabelSpecificComments && labelComment != "")
                finalCommentText = $"; !^ {labelComment} ^!";

            string s = $"{labelName} = {offsetStr}{finalCommentText}";
            return string.Format("{0," + (length * -1) + "}", s);
        }
    }

    #endregion
}

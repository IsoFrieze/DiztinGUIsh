using System;
using System.Collections.Generic;
using Diz.Core.model;
using Diz.Core.util;
using JetBrains.Annotations;

namespace Diz.Core.export
{
    public interface ILogCreatorDataSource : IReadOnlySnesRom, ITemporaryLabelProvider
    {

    }

    public interface ITemporaryLabelProvider
    {
        // add a temporary label which will be cleared out when we are finished the export
        public void AddTemporaryLabel(Label label);
        public void ClearTemporaryLabels();
    }

    public class TemporaryLabelProvider : ITemporaryLabelProvider
    {
        public Dictionary<int, Label> ExtraLabels { get; } = new();

        public void AddTemporaryLabel(Label label)
        {
            
        }

        public void ClearTemporaryLabels()
        {
            
        }
    }
    
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

        public LogWriterSettings Settings { get; init; }
        public ILogCreatorDataSource Data { get; init; }

        protected LogCreatorOutput Output;
        protected List<Tuple<string, int>> ParseList;
        protected List<int> LabelsWeVisited;
        protected int BankSize;
        // protected IEnumerable<KeyValuePair<int, Label>> BackupOfOriginalLabelsBeforeModifying;

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
            Data.ClearTemporaryLabels();
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


            // write the new generated labels in, don't let them overwrite any real labels
            // i.e. if the user defined a label like "PlayerSwimmingSprites", and our auto-generated
            // labels also contain a label at the same address, then ignore our auto-generated label,
            // only use the explicit user-created label.
            foreach (var label in ExtraLabels)
            {
                Data.AddTemporaryLabel(label.Key, label.Value, false);
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
            var line = GenerateLine(doesntMatter, special);
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
                Output.WriteLine(GenerateLine(i, "incsrc"));

            Output.WriteLine(GenerateLine(-1, "incsrc"));
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
                        var s2 = Parameters[s1].Item2;
                        tuple = Tuple.Create(s1, s2);
                    }
                    else
                    {
                        tuple = Tuple.Create(split[i].Substring(0, colon), int.Parse(split[i].Substring(colon + 1)));
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

        private void WriteTheRealLine(int pointer) => 
            Output.WriteLine(GenerateLine(pointer, null));

        private void WriteBlankLineIfEndPoint(int pointer)
        {
            if (IsLocationAnEndPoint(pointer))
                Output.WriteLine(GenerateLine(pointer, "empty"));
        }

        private void WriteBlankLineIfStartingNewParagraph(int pointer)
        {
            if (IsLocationAReadPoint(pointer) || AnyLabelsPresent(pointer))
                Output.WriteLine(GenerateLine(pointer, "empty"));
        }

        private bool IsLocationPoint(int pointer, InOutPoint mustHaveFlag) => (Data.GetInOutPoint(pointer) & mustHaveFlag) != 0;
        private bool IsLocationAnEndPoint(int pointer) => IsLocationPoint(pointer, InOutPoint.EndPoint);
        private bool IsLocationAReadPoint(int pointer) => IsLocationPoint(pointer, InOutPoint.ReadPoint);
        
        private bool AnyLabelsPresent(int pointer) => Data.GetLabel(pointer)?.Name.Length > 0;

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

            Output.WriteLine(GenerateLine(pointer, "empty"));
            Output.WriteLine(GenerateLine(pointer, "org"));
            Output.WriteLine(GenerateLine(pointer, "empty"));
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
                Output.WriteLine(GenerateLine(pair.Key, "labelassign"));
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
                Output.WriteLine($";!^!-{category}-! " + GenerateLine(pair.Key, "labelassign"));
            }
        }

        protected void SwitchOutputStream(int pointer, string streamName)
        {
            Output.SwitchToStream(streamName);

            // write an extra blank line if we would normally switch files here
            if (Settings.Structure == FormatStructure.SingleFile)
                Output.WriteLine(GenerateLine(pointer, "empty"));
        }

        // --------------------------
        #region WriteOperations

        protected string GenerateLine(int offset, string special)
        {
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

            var pc = Data.ConvertSnesToPc(ia);
            if (pc >= 0 && Data.GetLabelName(ia) != "") param = Data.GetLabelName(ia);
            return string.Format(format, param);
        }

        public string GetFormattedText(int offset, int bytes)
        {
            var text = "db \"";
            for (var i = 0; i < bytes; i++) text += (char)Data.GetRomByte(offset + i);
            return text + "\"";
        }

        public string GetDefaultLabel(int snes)
        {
            var pcoffset = Data.ConvertSnesToPc(snes);
            var prefix = RomUtil.TypeToLabel(Data.GetFlag(pcoffset));
            var labelAddress = Util.ToHexString6(snes);
            return $"{prefix}_{labelAddress}";
        }

        // just a %
        [UsedImplicitly]
        [AssemblerHandler(Token = "", Length = 1)]
        protected string GetPercent(int offset, int length)
        {
            return "%";
        }

        // all spaces
        [UsedImplicitly]
        [AssemblerHandler(Token = "%empty", Length = 1)]
        protected string GetEmpty(int offset, int length)
        {
            return string.Format("{0," + length + "}", "");
        }

        // trim to length
        // negative length = right justified
        [UsedImplicitly]
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

            return LeftAlign(length, label + (noColon ? "" : ":"));
        }

        // trim to length
        [UsedImplicitly]
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
                    code = GetFormattedBytes(offset, 1, bytes);
                    break;
                case FlagType.Data16Bit:
                    code = GetFormattedBytes(offset, 2, bytes);
                    break;
                case FlagType.Data24Bit:
                    code = GetFormattedBytes(offset, 3, bytes);
                    break;
                case FlagType.Data32Bit:
                    code = GetFormattedBytes(offset, 4, bytes);
                    break;
                case FlagType.Pointer16Bit:
                    code = GetPointer(offset, 2);
                    break;
                case FlagType.Pointer24Bit:
                    code = GetPointer(offset, 3);
                    break;
                case FlagType.Pointer32Bit:
                    code = GetPointer(offset, 4);
                    break;
                case FlagType.Text:
                    code = GetFormattedText(offset, bytes);
                    break;
            }

            return LeftAlign(length, code);
        }

        [UsedImplicitly]
        [AssemblerHandler(Token = "%org", Length = 37)]
        protected string GetOrg(int offset, int length)
        {
            string org = "ORG " + Util.NumberToBaseString(Data.ConvertPCtoSnes(offset), Util.NumberBase.Hexadecimal, 6, true);
            return LeftAlign(length, org);
        }

        [UsedImplicitly]
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
            return LeftAlign(length, s);
        }

        // 0+ = bank_xx.asm, -1 = labels.asm
        [UsedImplicitly]
        [AssemblerHandler(Token = "%incsrc", Length = 1)]
        protected string GetIncSrc(int offset, int length)
        {
            string s = "incsrc \"labels.asm\"";
            if (offset >= 0)
            {
                int bank = Data.ConvertPCtoSnes(offset) >> 16;
                s = string.Format("incsrc \"bank_{0}.asm\"", Util.NumberToBaseString(bank, Util.NumberBase.Hexadecimal, 2));
            }
            return LeftAlign(length, s);
        }

        [UsedImplicitly]
        [AssemblerHandler(Token = "%bankcross", Length = 1)]
        protected string GetBankCross(int offset, int length)
        {
            string s = "check bankcross off";
            return LeftAlign(length, s);
        }

        // length forced to 6
        [UsedImplicitly]
        [AssemblerHandler(Token = "ia", Length = 6)]
        protected string GetIntermediateAddress(int offset, int length)
        {
            int ia = Data.GetIntermediateAddressOrPointer(offset);
            return ia >= 0 ? Util.ToHexString6(ia) : "      ";
        }

        // length forced to 6
        [UsedImplicitly]
        [AssemblerHandler(Token = "pc", Length = 6)]
        protected string GetProgramCounter(int offset, int length)
        {
            return Util.ToHexString6(Data.ConvertPCtoSnes(offset));
        }

        // trim to length
        [UsedImplicitly]
        [AssemblerHandler(Token = "offset", Length = -6)]
        protected string GetOffset(int offset, int length)
        {
            return LeftAlign(length, Util.NumberToBaseString(offset, Util.NumberBase.Hexadecimal, 0));
        }
        
        private static string LeftAlign(int length, string str) => string.Format($"{{0,-{length}}}", str);

        // length forced to 8
        [UsedImplicitly]
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
        [UsedImplicitly]
        [AssemblerHandler(Token = "comment", Length = 1)]
        protected string GetComment(int offset, int length)
        {
            var snesOffset = Data.ConvertPCtoSnes(offset);
            return LeftAlign(length, Data.GetCommentText(snesOffset));
        }

        // length forced to 2
        [UsedImplicitly]
        [AssemblerHandler(Token = "b", Length = 2)]
        protected string GetDataBank(int offset, int length)
        {
            return Util.NumberToBaseString(Data.GetDataBank(offset), Util.NumberBase.Hexadecimal, 2);
        }

        // length forced to 4
        [UsedImplicitly]
        [AssemblerHandler(Token = "d", Length = 4)]
        protected string GetDirectPage(int offset, int length)
        {
            return Util.NumberToBaseString(Data.GetDirectPage(offset), Util.NumberBase.Hexadecimal, 4);
        }

        // if length == 1, M/m, else 08/16
        [UsedImplicitly]
        [AssemblerHandler(Token = "m", Length = 1)]
        protected string GetMFlag(int offset, int length)
        {
            var m = Data.GetMFlag(offset);
            if (length == 1) return m ? "M" : "m";
            else return m ? "08" : "16";
        }

        // if length == 1, X/x, else 08/16
        [UsedImplicitly]
        [AssemblerHandler(Token = "x", Length = 1)]
        protected string GetXFlag(int offset, int length)
        {
            var x = Data.GetXFlag(offset);
            if (length == 1) return x ? "X" : "x";
            else return x ? "08" : "16";
        }

        // output label at snes offset, and its value
        [UsedImplicitly]
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

            // TODO: probably not the best way to stuff this in here. -Dom
            // we should consider putting this in the %comment% section in the future.
            // for now, just hacking this in so it's included somewhere. this option defaults to OFF
            if (Settings.PrintLabelSpecificComments && labelComment != "")
                finalCommentText = $"; !^ {labelComment} ^!";

            return LeftAlign(length, $"{labelName} = {offsetStr}{finalCommentText}");
        }
    }

    #endregion
}

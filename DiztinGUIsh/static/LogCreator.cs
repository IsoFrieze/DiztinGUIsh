using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DiztinGUIsh
{
    public struct LogWriterSettings
    {
        // struct because we want to make a bunch of copies of this struct.
        // The plumbing could use a pass of something like 'ref readonly' because:
        // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/ref#reference-return-values

        public string format;
        public int dataPerLine;
        public LogCreator.FormatUnlabeled unlabeled;
        public LogCreator.FormatStructure structure;
        public bool includeUnusedLabels;
        public bool printLabelSpecificComments;

        // these are both paths to files or folders. TODO: rename for better description
        public string file;
        public string error;

        public void SetDefaults()
        {
            format = "%label:-22% %code:37%;%pc%|%bytes%|%ia%; %comment%";
            dataPerLine = 8;
            unlabeled = LogCreator.FormatUnlabeled.ShowInPoints;
            structure = LogCreator.FormatStructure.OneBankPerFile;
            includeUnusedLabels = false;
            printLabelSpecificComments = false;
            file = ""; // path to file or folder, rename
            error = ""; // path to file or folder, rename
        }
    }

    public class LogCreator
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

        public class OutputResult
        {
            public bool success;
            public int error_count;
        }

        private class AssemblerHandler : Attribute
        {
            public string token;
            public int weight;
        }

        public StreamWriter StreamOutput { get; set; }
        public StreamWriter StreamError { get; set; }
        public LogWriterSettings Settings { get; set; }
        public Data Data { get; set; }


        // dont use directly except to cache attributes
        private static Dictionary<string, Tuple<MethodInfo, int>> parameters;

        // safe to use directly.
        private static Dictionary<string, Tuple<MethodInfo, int>> Parameters
        {
            get
            {
                CacheAssemblerAttributeInfo();
                return parameters;
            }
        }

        private List<Tuple<string, int>> parseList;
        private List<int> usedLabels;
        private int errorCount, bankSize;
        private string folder;

        private static void CacheAssemblerAttributeInfo()
        {
            if (parameters != null)
                return;

            parameters = new Dictionary<string, Tuple<MethodInfo, int>>();

            var methodsWithAttributes = typeof(LogCreator)
                .GetMethods()
                .Where(
                    x => x.GetCustomAttributes(typeof(AssemblerHandler), false).FirstOrDefault() != null
                );

            foreach (var method in methodsWithAttributes)
            {
                var assemblerHandler = method.GetCustomAttribute<AssemblerHandler>();
                var token = assemblerHandler.token;
                var weight = assemblerHandler.weight;

                // check your method signature if you hit this stuff.
                Debug.Assert(method.GetParameters().Length == 2);

                Debug.Assert(method.GetParameters()[0].ParameterType == typeof(int));
                Debug.Assert(method.GetParameters()[0].Name == "offset");

                Debug.Assert(method.GetParameters()[1].ParameterType == typeof(int));
                Debug.Assert(method.GetParameters()[1].Name == "length");

                Debug.Assert(method.ReturnType == typeof(string));

                parameters.Add(token, (new Tuple<MethodInfo, int>(method, weight)));
            }
        }

        public string GetParameter(int offset, string parameter, int length)
        {
            if (!Parameters.TryGetValue(parameter, out var methodAndWeight))
            {
                throw new InvalidDataException($"Unknown parameter: {parameter}");
            }

            var methodInfo = methodAndWeight.Item1;
            var callParams = new object[] { offset, length };

            var returnValue = methodInfo.Invoke(this, callParams);

            Debug.Assert(returnValue is string);
            return returnValue as string;
        }

        public OutputResult CreateLog()
        {
            // var aliases = Data.GetAllLabels(); // junk
            // Data.Restore(a: new Dictionary<int, Label>(aliases)); // junk
            // AliasList.me.locked = true; // notify observers. do this outside this class.

            bankSize = Util.GetBankSize(Data.RomMapMode);
            errorCount = 0;

            GenerateGenericLabels();

            usedLabels = new List<int>();

            SetupParseList();

            var size = Data.GetROMSize();
            var pointer = WriteMainIncludes(size);

            int bank = -1;

            // show a progress bar while this happens
            ProgressBarJob.Loop(size, () =>
            {
                if (pointer >= size)
                    return -1; // stop looping

                WriteAddress(ref pointer, ref bank);

                return (long) pointer; // report current address as the progress
            });

            WriteLabels(pointer);

            if (Settings.structure == FormatStructure.OneBankPerFile)
                StreamOutput.Close();

            // // TODO: notify observers     AliasList.me.locked = false;
            return new OutputResult()
            {
                error_count = errorCount,
                success = true,
            };
        }

        private int WriteMainIncludes(int size)
        {
            var pointer = 0;
            if (Settings.structure == FormatStructure.OneBankPerFile)
            {
                folder = Path.GetDirectoryName(((FileStream) StreamOutput.BaseStream).Name);
                StreamOutput.WriteLine(GetLine(pointer, "map"));
                StreamOutput.WriteLine(GetLine(pointer, "empty"));
                for (var i = 0; i < size; i += bankSize)
                    StreamOutput.WriteLine(GetLine(i, "incsrc"));

                StreamOutput.WriteLine(GetLine(-1, "incsrc"));
            }
            else
            {
                StreamOutput.WriteLine(GetLine(pointer, "map"));
                StreamOutput.WriteLine(GetLine(pointer, "empty"));
            }

            return pointer;
        }

        // TODO: These are labels like "CODE_856469" and "DATA_763525".
        // ISSUE: the original code just modified Data, but, we can't do that anymore.
        // Either we need to copy all of it, or, we need another list of labels and use that.
        private void GenerateGenericLabels()
        {
            var addressList = new List<int>();
            var pointer = 0;

            while (pointer < Data.GetROMSize())
            {
                var addr = GetLabelTargetAddress(pointer, out var length);
                pointer += length;

                if (addr != -1)
                    addressList.Add(addr);
            }

            // TODO: +/- labels
            foreach (var t in addressList)
            {
                var label = new Label()
                {
                    name = Data.GetDefaultLabel(t)
                };

                throw new NotImplementedException("see note above, not implemented yet.");
                // if we were just going to add them we could do this:
                // Data.AddLabel(t, label, false);
            }
        }

        private int GetLabelTargetAddress(int pointer, out int length)
        {
            length = GetLineByteLength(pointer);

            var flag = Data.GetFlag(pointer);

            bool c1 = Settings.unlabeled == LogCreator.FormatUnlabeled.ShowAll;
            bool c2 = Settings.unlabeled != LogCreator.FormatUnlabeled.ShowNone &&
                      (flag == Data.FlagType.Opcode || flag == Data.FlagType.Pointer16Bit ||
                       flag == Data.FlagType.Pointer24Bit || flag == Data.FlagType.Pointer32Bit);

            if (c1)
            {
                return Data.ConvertPCtoSNES(pointer);
            }
            else if (c2)
            {
                var ia = Data.GetIntermediateAddressOrPointer(pointer);

                if (ia >= 0 && Data.ConvertSNEStoPC(ia) >= 0)
                    return ia;
            }

            return -1;
        }

        private void SetupParseList()
        {
            // TODO: this is probably not correct now, check

            string[] split = Settings.format.Split('%');
            parseList = new List<Tuple<string, int>>();
            for (int i = 0; i < split.Length; i++)
            {
                if (i % 2 == 0) parseList.Add(Tuple.Create(split[i], int.MaxValue));
                else
                {
                    var colon = split[i].IndexOf(':');
                    parseList.Add(colon < 0
                        ? Tuple.Create(split[i], Parameters[split[i]].Item2)
                        : Tuple.Create(split[i].Substring(0, colon), int.Parse(split[i].Substring(colon + 1))));
                }
            }
        }

        private void WriteAddress(ref int pointer, ref int bank)
        {
            var snes = Data.ConvertPCtoSNES(pointer);
            if ((snes >> 16) != bank)
            {
                // TODO: combine w/ SwitchOutputFile?
                if (Settings.structure == FormatStructure.OneBankPerFile)
                {
                    StreamOutput.Close();
                    StreamOutput = new StreamWriter(
                        $"{folder}/bank_{Util.NumberToBaseString((snes >> 16), Util.NumberBase.Hexadecimal, 2)}.asm");
                }

                StreamOutput.WriteLine(GetLine(pointer, "empty"));
                StreamOutput.WriteLine(GetLine(pointer, "org"));
                StreamOutput.WriteLine(GetLine(pointer, "empty"));
                if ((snes % bankSize) != 0)
                    StreamError.WriteLine("({0}) Offset 0x{1:X}: An instruction crossed a bank boundary.", ++errorCount, pointer);
                bank = snes >> 16;
            }

            var c1 = (Data.GetInOutPoint(pointer) & (Data.InOutPoint.ReadPoint)) != 0;
            var c2 = (Data.GetAllLabels().TryGetValue(pointer, out var label) && label.name.Length > 0);
            if (c1 || c2)
                StreamOutput.WriteLine(GetLine(pointer, "empty"));

            StreamOutput.WriteLine(GetLine(pointer, null));
            if ((Data.GetInOutPoint(pointer) & (Data.InOutPoint.EndPoint)) != 0) StreamOutput.WriteLine(GetLine(pointer, "empty"));
            pointer += GetLineByteLength(pointer);
        }

        private void WriteLabels(int pointer)
        {
            SwitchOutputFile(pointer, $"{folder}/labels.asm");

            var listToPrint = new Dictionary<int, Label>();

            // part 1: important: include all labels we aren't defining somewhere else. needed for disassembly
            foreach (var pair in Data.GetAllLabels())
            {
                if (usedLabels.Contains(pair.Key)) 
                    continue;

                // this label was not defined elsewhere in our disassembly, so we need to include it in labels.asm
                listToPrint.Add(pair.Key, pair.Value);
            }

            foreach (var pair in listToPrint)
            {
                StreamOutput.WriteLine(GetLine(pair.Key, "labelassign"));
            }

            // part 2: optional: if requested, print all labels regardless of use.
            // Useful for debugging, documentation, or reverse engineering workflow.
            // this file shouldn't need to be included in the build, it's just reference documentation
            if (Settings.includeUnusedLabels)
            {
                SwitchOutputFile(pointer, $"{folder}/all-labels.txt");
                foreach (var pair in Data.GetAllLabels())
                {
                    // not the best place to add formatting, TODO: cleanup
                    var category = listToPrint.ContainsKey(pair.Key) ? "INLINE" : "EXTRA ";
                    StreamOutput.WriteLine($";!^!-{category}-! " + GetLine(pair.Key, "labelassign"));
                }
            }
        }

        private void SwitchOutputFile(int pointer, string path)
        {
            if (Settings.structure == FormatStructure.OneBankPerFile)
            {
                StreamOutput.Close();
                StreamOutput = new StreamWriter(path);
            }
            else
            {
                StreamOutput.WriteLine(GetLine(pointer, "empty"));
            }
        }

        // --------------------------
        #region WriteOperations

        private string GetLine(int offset, string special)
        {
            var line = "";
            foreach (var t in parseList)
            {
                if (t.Item2 == int.MaxValue) // string literal
                {
                    line += t.Item1;
                }
                else if (special != null) // special parameter (replaces code & everything else = empty)
                {
                    line += GetParameter(offset, "%" + (t.Item1 == "code" ? special : "empty"), t.Item2);
                }
                else // normal parameter
                {
                    line += GetParameter(offset, t.Item1, t.Item2);
                }
            }

            if (special != null) 
                return line;

            // throw out some errors if stuff looks fishy
            Data.FlagType flag = Data.GetFlag(offset), check = flag == Data.FlagType.Opcode ? Data.FlagType.Operand : flag;
            int step = flag == Data.FlagType.Opcode ? GetLineByteLength(offset) : Util.TypeStepSize(flag), size = Data.GetROMSize();
            if (flag == Data.FlagType.Operand) StreamError.WriteLine("({0}) Offset 0x{1:X}: Bytes marked as operands formatted as Data.", ++errorCount, offset);
            else if (step > 1)
            {
                for (var i = 1; i < step; i++)
                {
                    if (offset + i >= size)
                    {
                        StreamError.WriteLine("({0}) Offset 0x{1:X}: {2} extends past the end of the ROM.", ++errorCount, offset, Util.TypeToString(check));
                        break;
                    }
                    else if (Data.GetFlag(offset + i) != check)
                    {
                        StreamError.WriteLine("({0}) Offset 0x{1:X}: Expected {2}, but got {3} instead.", ++errorCount, offset + i, Util.TypeToString(check), Util.TypeToString(Data.GetFlag(offset + i)));
                        break;
                    }
                }
            }
            var ia = Data.GetIntermediateAddress(offset, true);
            if (ia >= 0 && flag == Data.FlagType.Opcode && Data.GetInOutPoint(offset) == Data.InOutPoint.OutPoint && Data.GetFlag(Data.ConvertSNEStoPC(ia)) != Data.FlagType.Opcode)
            {
                StreamError.WriteLine("({0}) Offset 0x{1:X}: Branch or jump instruction to a non-instruction.", ++errorCount, offset);
            }

            return line;
        }

        private int GetLineByteLength(int offset)
        {
            int max = 1, step = 1;
            var size = Data.GetROMSize();
            var data = Data;

            switch (data.GetFlag(offset))
            {
                case Data.FlagType.Opcode:
                    return data.OpcodeByteLength(offset);
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
                data.GetFlag(offset + min) == data.GetFlag(offset) &&
                data.GetLabelName(data.ConvertPCtoSNES(offset + min)) == "" &&
                (offset + min) / bankSize == myBank
            ) min += step;
            return min;
        }

        public static bool ValidateFormat(string formatString)
        {
            var tokens = formatString.ToLower().Split('%');

            // not valid if format has an odd amount of %s
            if (tokens.Length % 2 == 0) return false;

            for (int i = 1; i < tokens.Length; i += 2)
            {
                int indexOfColon = tokens[i].IndexOf(':');
                string kind = indexOfColon >= 0 ? tokens[i].Substring(0, indexOfColon) : tokens[i];

                // not valid if base token isn't one we know of
                if (!Parameters.ContainsKey(kind))
                    return false;

                // not valid if parameter isn't an integer
                int oof;
                if (indexOfColon >= 0 && !int.TryParse(tokens[i].Substring(indexOfColon + 1), out oof)) 
                    return false;
            }

            return true;
        }

        // just a %
        [AssemblerHandler(token = "", weight = 1)]
        private static string GetPercent(int offset, int length)
        {
            return "%";
        }

        // all spaces
        [AssemblerHandler(token = "%empty", weight = 1)]
        private static string GetEmpty(int offset, int length)
        {
            return string.Format("{0," + length + "}", "");
        }

        // trim to length
        // negative length = right justified
        [AssemblerHandler(token = "label", weight = -22)]
        private string GetLabel(int offset, int length)
        {
            var snes = Data.ConvertPCtoSNES(offset);
            var label = Data.GetLabelName(snes);
            if (label == null)
                return "";
            
            usedLabels.Add(snes);
            bool noColon = label.Length == 0 || label[0] == '-' || label[0] == '+';
            return string.Format("{0," + (length * -1) + "}", label + (noColon ? "" : ":"));
        }

        // trim to length
        [AssemblerHandler(token = "code", weight = 37)]
        private string GetCode(int offset, int length)
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

        [AssemblerHandler(token = "%org", weight = 37)]
        private string GetORG(int offset, int length)
        {
            string org = "ORG " + Util.NumberToBaseString(Data.ConvertPCtoSNES(offset), Util.NumberBase.Hexadecimal, 6, true);
            return string.Format("{0," + (length * -1) + "}", org);
        }

        [AssemblerHandler(token = "%map", weight = 37)]
        private string GetMap(int offset, int length)
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
        [AssemblerHandler(token = "%incsrc", weight = 1)]
        private string GetIncSrc(int offset, int length)
        {
            string s = "incsrc \"labels.asm\"";
            if (offset >= 0)
            {
                int bank = Data.ConvertPCtoSNES(offset) >> 16;
                s = string.Format("incsrc \"bank_{0}.asm\"", Util.NumberToBaseString(bank, Util.NumberBase.Hexadecimal, 2));
            }
            return string.Format("{0," + (length * -1) + "}", s);
        }

        [AssemblerHandler(token = "%bankcross", weight = 1)]
        private string GetBankCross(int offset, int length)
        {
            string s = "check bankcross off";
            return string.Format("{0," + (length * -1) + "}", s);
        }

        // length forced to 6
        [AssemblerHandler(token = "ia", weight = 6)]
        private string GetIntermediateAddress(int offset, int length)
        {
            int ia = Data.GetIntermediateAddressOrPointer(offset);
            return ia >= 0 ? Util.NumberToBaseString(ia, Util.NumberBase.Hexadecimal, 6) : "      ";
        }

        // length forced to 6
        [AssemblerHandler(token = "pc", weight = 6)]
        private string GetProgramCounter(int offset, int length)
        {
            return Util.NumberToBaseString(Data.ConvertPCtoSNES(offset), Util.NumberBase.Hexadecimal, 6);
        }

        // trim to length
        [AssemblerHandler(token = "offset", weight = -6)]
        private string GetOffset(int offset, int length)
        {
            return string.Format("{0," + (length * -1) + "}", Util.NumberToBaseString(offset, Util.NumberBase.Hexadecimal, 0));
        }

        // length forced to 8
        [AssemblerHandler(token = "bytes", weight = 8)]
        private string GetRawBytes(int offset, int length)
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
        [AssemblerHandler(token = "comment", weight = 1)]
        private string GetComment(int offset, int length)
        {
            return string.Format("{0," + (length * -1) + "}", Data.GetComment(Data.ConvertPCtoSNES(offset)));
        }

        // length forced to 2
        [AssemblerHandler(token = "b", weight = 2)]
        private string GetDataBank(int offset, int length)
        {
            return Util.NumberToBaseString(Data.GetDataBank(offset), Util.NumberBase.Hexadecimal, 2);
        }

        // length forced to 4
        [AssemblerHandler(token = "d", weight = 4)]
        private string GetDirectPage(int offset, int length)
        {
            return Util.NumberToBaseString(Data.GetDirectPage(offset), Util.NumberBase.Hexadecimal, 4);
        }

        // if length == 1, M/m, else 08/16
        [AssemblerHandler(token = "m", weight = 1)]
        private string GetMFlag(int offset, int length)
        {
            var m = Data.GetMFlag(offset);
            if (length == 1) return m ? "M" : "m";
            else return m ? "08" : "16";
        }

        // if length == 1, X/x, else 08/16
        [AssemblerHandler(token = "x", weight = 1)]
        private string GetXFlag(int offset, int length)
        {
            var x = Data.GetXFlag(offset);
            if (length == 1) return x ? "X" : "x";
            else return x ? "08" : "16";
        }

        // output label at snes offset, and its value
        [AssemblerHandler(token = "%labelassign", weight = 1)]
        private string GetLabelAssign(int offset, int length)
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

using DiztinGUIsh.window;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiztinGUIsh
{
    public static class LogCreator
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

        public static Dictionary<string, Tuple<Func<int, int, string>, int>> parameters = new Dictionary<string, Tuple<Func<int, int, string>, int>>
        {
            { "", Tuple.Create<Func<int, int, string>, int>(GetPercent, 1) },
            { "label", Tuple.Create<Func<int, int, string>, int>(GetLabel, -22) },
            { "code", Tuple.Create<Func<int, int, string>, int>(GetCode, 37) },
            { "ia", Tuple.Create<Func<int, int, string>, int>(GetIntermediateAddress, 6) },
            { "pc", Tuple.Create<Func<int, int, string>, int>(GetProgramCounter, 6) },
            { "offset", Tuple.Create<Func<int, int, string>, int>(GetOffset, -6) },
            { "bytes", Tuple.Create<Func<int, int, string>, int>(GetRawBytes, 8) },
            { "comment", Tuple.Create<Func<int, int, string>, int>(GetComment, 1) },
            { "b", Tuple.Create<Func<int, int, string>, int>(GetDataBank, 2) },
            { "d", Tuple.Create<Func<int, int, string>, int>(GetDirectPage, 4) },
            { "m", Tuple.Create<Func<int, int, string>, int>(GetMFlag, 1) },
            { "x", Tuple.Create<Func<int, int, string>, int>(GetXFlag, 1) },
            { "%org", Tuple.Create<Func<int, int, string>, int>(GetORG, 37) },
            { "%map", Tuple.Create<Func<int, int, string>, int>(GetMap, 37) },
            { "%empty", Tuple.Create<Func<int, int, string>, int>(GetEmpty, 1) },
            { "%incsrc", Tuple.Create<Func<int, int, string>, int>(GetIncSrc, 1) },
            { "%bankcross", Tuple.Create<Func<int, int, string>, int>(GetBankCross, 1) },
            { "%labelassign", Tuple.Create<Func<int, int, string>, int>(GetLabelAssign, 1) },
        };

        public static string format = "%label:-22% %code:37%;%pc%|%bytes%|%ia%; %comment%";
        public static int dataPerLine = 8;
        public static FormatUnlabeled unlabeled = FormatUnlabeled.ShowInPoints;
        public static FormatStructure structure = FormatStructure.OneBankPerFile;
        public static bool includeUnusedLabels;
        public static bool printLabelSpecificComments;

        private static List<Tuple<string, int>> list;
        private static List<int> usedLabels;
        private static StreamWriter err;
        private static int errorCount, bankSize;
        private static string folder;

        public static int CreateLog(StreamWriter sw, StreamWriter er)
        {
            var tempAlias = Data.GetAllLabels();
            Data.Restore(a: new Dictionary<int, Data.AliasInfo>(tempAlias));
            AliasList.me.locked = true;
            bankSize = Data.GetROMMapMode() == Data.ROMMapMode.LoROM ? 0x8000 : 0x10000; // todo

            AddTemporaryLabels();

            string[] split = format.Split('%');
            err = er;
            errorCount = 0;
            usedLabels = new List<int>();

            list = new List<Tuple<string, int>>();
            for (int i = 0; i < split.Length; i++)
            {
                if (i % 2 == 0) list.Add(Tuple.Create(split[i], int.MaxValue));
                else
                {
                    int colon = split[i].IndexOf(':');
                    if (colon < 0) list.Add(Tuple.Create(split[i], parameters[split[i]].Item2));
                    else list.Add(Tuple.Create(split[i].Substring(0, colon), int.Parse(split[i].Substring(colon + 1))));
                }
            }

            int pointer = 0, size = (Data.GetTable() == ExportDisassembly.sampleTable) ? 0x7B : Data.GetROMSize(), bank = -1;

            if (structure == FormatStructure.OneBankPerFile)
            {
                folder = Path.GetDirectoryName(((FileStream)sw.BaseStream).Name);
                sw.WriteLine(GetLine(pointer, "map"));
                sw.WriteLine(GetLine(pointer, "empty"));
                for (int i = 0; i < size; i += bankSize) sw.WriteLine(GetLine(i, "incsrc"));
                sw.WriteLine(GetLine(-1, "incsrc"));
            } else
            {
                sw.WriteLine(GetLine(pointer, "map"));
                sw.WriteLine(GetLine(pointer, "empty"));
            }

            // show a progress bar while this happens
            LongJob.Loop(size, () =>
            {
                if (pointer >= size)
                    return -1; // stop looping

                WriteAddress(ref sw, ref pointer, tempAlias, ref bank);

                return (long) pointer; // report current address as the progress
            });

            WriteLabels(ref sw, pointer);

            if (structure == FormatStructure.OneBankPerFile) sw.Close();
            Data.Restore(a: tempAlias);
            AliasList.me.locked = false;
            return errorCount;
        }

        private static void WriteAddress(ref StreamWriter sw, ref int pointer, Dictionary<int, Data.AliasInfo> tempAlias, ref int bank)
        {
            int snes = Util.ConvertPCtoSNES(pointer);
            if ((snes >> 16) != bank)
            {
                if (structure == FormatStructure.OneBankPerFile)
                {
                    sw.Close();
                    sw = new StreamWriter(string.Format("{0}/bank_{1}.asm", folder,
                        Util.NumberToBaseString((snes >> 16), Util.NumberBase.Hexadecimal, 2)));
                }

                sw.WriteLine(GetLine(pointer, "empty"));
                sw.WriteLine(GetLine(pointer, "org"));
                sw.WriteLine(GetLine(pointer, "empty"));
                if ((snes % bankSize) != 0)
                    err.WriteLine("({0}) Offset 0x{1:X}: An instruction crossed a bank boundary.", ++errorCount, pointer);
                bank = snes >> 16;
            }

            var c1 = (Data.GetInOutPoint(pointer) & (Data.InOutPoint.ReadPoint)) != 0;
            var c2 = (tempAlias.TryGetValue(pointer, out var aliasInfo) && aliasInfo.name.Length > 0);
            if (c1 || c2)
                sw.WriteLine(GetLine(pointer, "empty"));

            sw.WriteLine(GetLine(pointer, null));
            if ((Data.GetInOutPoint(pointer) & (Data.InOutPoint.EndPoint)) != 0) sw.WriteLine(GetLine(pointer, "empty"));
            pointer += GetLineByteLength(pointer);
        }

        private static void WriteLabels(ref StreamWriter sw, int pointer)
        {
            SwitchOutputFile(ref sw, pointer, $"{folder}/labels.asm");

            Dictionary<int, Data.AliasInfo> listToPrint = new Dictionary<int, Data.AliasInfo>();

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
                sw.WriteLine(GetLine(pair.Key, "labelassign"));
            }

            // part 2: optional: if requested, print all labels regardless of use.
            // Useful for debugging, documentation, or reverse engineering workflow.
            // this file shouldn't need to be included in the build, it's just reference documentation
            if (includeUnusedLabels)
            {
                SwitchOutputFile(ref sw, pointer, $"{folder}/all-labels.txt");
                foreach (var pair in Data.GetAllLabels())
                {
                    // not the best place to add formatting, TODO: cleanup
                    var category = listToPrint.ContainsKey(pair.Key) ? "INLINE" : "EXTRA ";
                    sw.WriteLine($";!^!-{category}-! " + GetLine(pair.Key, "labelassign"));
                }
            }
        }

        private static void SwitchOutputFile(ref StreamWriter sw, int pointer, string path)
        {
            if (structure == FormatStructure.OneBankPerFile)
            {
                sw.Close();
                sw = new StreamWriter(path);
            }
            else
            {
                sw.WriteLine(GetLine(pointer, "empty"));
            }
        }

        private static void AddTemporaryLabels()
        {
            List<int> addMe = new List<int>();
            int pointer = 0;

            while (pointer < Data.GetROMSize())
            {
                int length = GetLineByteLength(pointer);
                Data.FlagType flag = Data.GetFlag(pointer);

                if (unlabeled == FormatUnlabeled.ShowAll) addMe.Add(Util.ConvertPCtoSNES(pointer));
                else if (unlabeled != FormatUnlabeled.ShowNone &&
                    (flag == Data.FlagType.Opcode || flag == Data.FlagType.Pointer16Bit || flag == Data.FlagType.Pointer24Bit || flag == Data.FlagType.Pointer32Bit))
                {
                    int ia = Util.GetIntermediateAddressOrPointer(pointer);
                    if (ia >= 0 && Util.ConvertSNEStoPC(ia) >= 0) addMe.Add(ia);
                }

                pointer += length;
            }

            // TODO +/- labels
            for (int i = 0; i < addMe.Count; i++)
            {
                Data.AddLabel(addMe[i], 
                    new Data.AliasInfo()
                    {
                        name = Util.GetDefaultLabel(addMe[i])
                    }, 
                    false);
            }
        }

        private static string GetLine(int offset, string special)
        {
            string line = "";
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Item2 == int.MaxValue) // string literal
                {
                    line += list[i].Item1;
                }
                else if (special != null) // special parameter (replaces code & everything else = empty)
                {
                    line += GetParameter(offset, "%" + (list[i].Item1 == "code" ? special : "empty"), list[i].Item2);
                }
                else // normal parameter
                {
                    line += GetParameter(offset, list[i].Item1, list[i].Item2);
                }
            }

            if (special == null)
            {
                // throw out some errors if stuff looks fishy
                Data.FlagType flag = Data.GetFlag(offset), check = flag == Data.FlagType.Opcode ? Data.FlagType.Operand : flag;
                int step = flag == Data.FlagType.Opcode ? GetLineByteLength(offset) : Util.TypeStepSize(flag), size = Data.GetROMSize();
                if (flag == Data.FlagType.Operand) err.WriteLine("({0}) Offset 0x{1:X}: Bytes marked as operands formatted as data.", ++errorCount, offset);
                else if (step > 1)
                {
                    for (int i = 1; i < step; i++)
                    {
                        if (offset + i >= size)
                        {
                            err.WriteLine("({0}) Offset 0x{1:X}: {2} extends past the end of the ROM.", ++errorCount, offset, Util.TypeToString(check));
                            break;
                        }
                        else if (Data.GetFlag(offset + i) != check)
                        {
                            err.WriteLine("({0}) Offset 0x{1:X}: Expected {2}, but got {3} instead.", ++errorCount, offset + i, Util.TypeToString(check), Util.TypeToString(Data.GetFlag(offset + i)));
                            break;
                        }
                    }
                }
                int ia = Util.GetIntermediateAddress(offset, true);
                if (ia >= 0 && flag == Data.FlagType.Opcode && Data.GetInOutPoint(offset) == Data.InOutPoint.OutPoint && Data.GetFlag(Util.ConvertSNEStoPC(ia)) != Data.FlagType.Opcode)
                {
                    err.WriteLine("({0}) Offset 0x{1:X}: Branch or jump instruction to a non-instruction.", ++errorCount, offset);
                }
            }

            return line;
        }

        private static int GetLineByteLength(int offset)
        {
            int max = 1, step = 1;
            int size = Data.GetROMSize();

            switch (Data.GetFlag(offset))
            {
                case Data.FlagType.Opcode:
                    switch (Data.GetArchitechture(offset))
                    {
                        case Data.Architechture.CPU65C816: return CPU65C816.GetInstructionLength(offset);
                        case Data.Architechture.APUSPC700: return 1;
                        case Data.Architechture.GPUSuperFX: return 1;
                    }
                    return 1;
                case Data.FlagType.Unreached:
                case Data.FlagType.Operand:
                case Data.FlagType.Data8Bit:
                case Data.FlagType.Graphics:
                case Data.FlagType.Music:
                case Data.FlagType.Empty:
                    max = dataPerLine;
                    break;
                case Data.FlagType.Text:
                    max = 21;
                    break;
                case Data.FlagType.Data16Bit:
                    step = 2;
                    max = dataPerLine;
                    break;
                case Data.FlagType.Data24Bit:
                    step = 3;
                    max = dataPerLine;
                    break;
                case Data.FlagType.Data32Bit:
                    step = 4;
                    max = dataPerLine;
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
                Data.GetLabelName(Util.ConvertPCtoSNES(offset + min)) == "" &&
                (offset + min) / bankSize == myBank
            ) min += step;
            return min;
        }

        public static string GetParameter(int offset, string parameter, int length)
        {
            Tuple<Func<int, int, string>, int> tup;
            if (parameters.TryGetValue(parameter, out tup)) return tup.Item1.Invoke(offset, length);
            return "";
        }

        // just a %
        private static string GetPercent(int offset, int length)
        {
            return "%";
        }

        // all spaces
        private static string GetEmpty(int offset, int length)
        {
            return string.Format("{0," + length + "}", "");
        }

        // trim to length
        // negative length = right justified
        private static string GetLabel(int offset, int length)
        {
            int snes = Util.ConvertPCtoSNES(offset);
            string label = Data.GetLabelName(snes);
            if (label == null)
                return "";
            
            usedLabels.Add(snes);
            bool noColon = label.Length == 0 || label[0] == '-' || label[0] == '+';
            return string.Format("{0," + (length * -1) + "}", label + (noColon ? "" : ":"));
        }

        // trim to length
        private static string GetCode(int offset, int length)
        {
            int bytes = GetLineByteLength(offset);
            string code = "";

            switch (Data.GetFlag(offset))
            {
                case Data.FlagType.Opcode:
                    code = Util.GetInstruction(offset);
                    break;
                case Data.FlagType.Unreached:
                case Data.FlagType.Operand:
                case Data.FlagType.Data8Bit:
                case Data.FlagType.Graphics:
                case Data.FlagType.Music:
                case Data.FlagType.Empty:
                    code = Util.GetFormattedBytes(offset, 1, bytes);
                    break;
                case Data.FlagType.Data16Bit:
                    code = Util.GetFormattedBytes(offset, 2, bytes);
                    break;
                case Data.FlagType.Data24Bit:
                    code = Util.GetFormattedBytes(offset, 3, bytes);
                    break;
                case Data.FlagType.Data32Bit:
                    code = Util.GetFormattedBytes(offset, 4, bytes);
                    break;
                case Data.FlagType.Pointer16Bit:
                    code = Util.GetPointer(offset, 2);
                    break;
                case Data.FlagType.Pointer24Bit:
                    code = Util.GetPointer(offset, 3);
                    break;
                case Data.FlagType.Pointer32Bit:
                    code = Util.GetPointer(offset, 4);
                    break;
                case Data.FlagType.Text:
                    code = Util.GetFormattedText(offset, bytes);
                    break;
            }

            return string.Format("{0," + (length * -1) + "}", code);
        }

        private static string GetORG(int offset, int length)
        {
            string org = "ORG " + Util.NumberToBaseString(Util.ConvertPCtoSNES(offset), Util.NumberBase.Hexadecimal, 6, true);
            return string.Format("{0," + (length * -1) + "}", org);
        }

        private static string GetMap(int offset, int length)
        {
            string s = "";
            switch (Data.GetROMMapMode())
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
        private static string GetIncSrc(int offset, int length)
        {
            string s = "incsrc \"labels.asm\"";
            if (offset >= 0)
            {
                int bank = Util.ConvertPCtoSNES(offset) >> 16;
                s = string.Format("incsrc \"bank_{0}.asm\"", Util.NumberToBaseString(bank, Util.NumberBase.Hexadecimal, 2));
            }
            return string.Format("{0," + (length * -1) + "}", s);
        }

        private static string GetBankCross(int offset, int length)
        {
            string s = "check bankcross off";
            return string.Format("{0," + (length * -1) + "}", s);
        }

        // length forced to 6
        private static string GetIntermediateAddress(int offset, int length)
        {
            int ia = Util.GetIntermediateAddressOrPointer(offset);
            return ia >= 0 ? Util.NumberToBaseString(ia, Util.NumberBase.Hexadecimal, 6) : "      ";
        }

        // length forced to 6
        private static string GetProgramCounter(int offset, int length)
        {
            return Util.NumberToBaseString(Util.ConvertPCtoSNES(offset), Util.NumberBase.Hexadecimal, 6);
        }

        // trim to length
        private static string GetOffset(int offset, int length)
        {
            return string.Format("{0," + (length * -1) + "}", Util.NumberToBaseString(offset, Util.NumberBase.Hexadecimal, 0));
        }

        // length forced to 8
        private static string GetRawBytes(int offset, int length)
        {
            string bytes = "";
            if (Data.GetFlag(offset) == Data.FlagType.Opcode)
            {
                for (int i = 0; i < Manager.GetInstructionLength(offset); i++)
                {
                    bytes += Util.NumberToBaseString(Data.GetROMByte(offset + i), Util.NumberBase.Hexadecimal);
                }
            }
            return string.Format("{0,-8}", bytes);
        }

        // trim to length
        private static string GetComment(int offset, int length)
        {
            return string.Format("{0," + (length * -1) + "}", Data.GetComment(Util.ConvertPCtoSNES(offset)));
        }

        // length forced to 2
        private static string GetDataBank(int offset, int length)
        {
            return Util.NumberToBaseString(Data.GetDataBank(offset), Util.NumberBase.Hexadecimal, 2);
        }

        // length forced to 4
        private static string GetDirectPage(int offset, int length)
        {
            return Util.NumberToBaseString(Data.GetDirectPage(offset), Util.NumberBase.Hexadecimal, 4);
        }

        // if length == 1, M/m, else 08/16
        private static string GetMFlag(int offset, int length)
        {
            bool m = Data.GetMFlag(offset);
            if (length == 1) return m ? "M" : "m";
            else return m ? "08" : "16";
        }

        // if length == 1, X/x, else 08/16
        private static string GetXFlag(int offset, int length)
        {
            bool x = Data.GetXFlag(offset);
            if (length == 1) return x ? "X" : "x";
            else return x ? "08" : "16";
        }

        // output label at snes offset, and its value
        private static string GetLabelAssign(int offset, int length)
        {
            var labelName = Data.GetLabelName(offset);
            var offsetStr = Util.NumberToBaseString(offset, Util.NumberBase.Hexadecimal, 6, true);
            var labelComment = Data.GetLabelComment(offset);

            if (string.IsNullOrEmpty(labelName))
                return "";

            if (labelComment == null)
                labelComment = "";

            var finalCommentText = "";

            // TODO: sorry, probably not the best way to stuff this in here, consider putting it in the %comment% section in the future. -Dom
            if (printLabelSpecificComments && labelComment != "")
                finalCommentText = $"; !^ {labelComment} ^!";

            string s = $"{labelName} = {offsetStr}{finalCommentText}";
            return string.Format("{0," + (length * -1) + "}", s);
        }
    }
}

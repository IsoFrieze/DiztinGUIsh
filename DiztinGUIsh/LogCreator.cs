using System;
using System.Collections.Generic;
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
            ShowInPoints = 1,
            ShowInPointsPlusMinus = 2,
            ShowNone = 3
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
            { "ea", Tuple.Create<Func<int, int, string>, int>(GetEffectiveAddress, 6) },
            { "pc", Tuple.Create<Func<int, int, string>, int>(GetProgramCounter, 6) },
            { "offset", Tuple.Create<Func<int, int, string>, int>(GetOffset, -6) },
            { "bytes", Tuple.Create<Func<int, int, string>, int>(GetRawBytes, 8) },
            { "comment", Tuple.Create<Func<int, int, string>, int>(GetComment, 1) },
            { "b", Tuple.Create<Func<int, int, string>, int>(GetDataBank, 2) },
            { "d", Tuple.Create<Func<int, int, string>, int>(GetDirectPage, 4) },
            { "m", Tuple.Create<Func<int, int, string>, int>(GetMFlag, 1) },
            { "x", Tuple.Create<Func<int, int, string>, int>(GetXFlag, 1) },
        };

        public static string format = "%label:-22% %code:37%;%pc%|%bytes%|%ea%; %comment%";
        public static int dataPerLine = 8;
        public static FormatUnlabeled unlabeled = FormatUnlabeled.ShowNone;
        public static FormatStructure structure = FormatStructure.SingleFile;

        private static List<Tuple<string, int>> list;

        public static bool CreateLog(StreamWriter sw, StreamWriter er)
        {
            string[] split = format.Split('%');

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

            int pointer = 0, size = (Data.GetTable() == ExportDisassembly.sample) ? 0x7B : Data.GetROMSize();
            while (pointer < size)
            {
                if ((Data.GetInOutPoint(pointer) & (Data.InOutPoint.ReadPoint | Data.InOutPoint.InPoint)) != 0 || (Data.GetLabel(pointer).Length > 0)) sw.WriteLine(GetLine(pointer, true));
                sw.WriteLine(GetLine(pointer, false));
                if ((Data.GetInOutPoint(pointer) & (Data.InOutPoint.EndPoint | Data.InOutPoint.OutPoint)) != 0) sw.WriteLine(GetLine(pointer, true));
                pointer += GetLineByteLength(pointer);
            }

            return false;
        }

        private static string GetLine(int offset, bool empty)
        {
            string line = "";
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Item2 == int.MaxValue) line += list[i].Item1;
                else if (empty) line += string.Format("{0," + list[i].Item2 + "}", "");
                else line += GetParameter(offset, list[i].Item1, list[i].Item2);
            }
            return line;
        }

        private static int GetLineByteLength(int offset)
        {
            int max = 1, step = 1;
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

            int min = step;
            while (min < max && offset + min < Data.GetROMSize() && Data.GetFlag(offset + min) == Data.GetFlag(offset)) min += step;
            return min;
        }

        public static string GetParameter(int offset, string parameter, int length)
        {
            if (parameters.TryGetValue(parameter, out Tuple<Func<int, int, string>, int> tup)) return tup.Item1.Invoke(offset, length);
            return "";
        }

        // just a %
        private static string GetPercent(int offset, int length)
        {
            return "%";
        }

        // trim to length
        // negative length = right justified
        private static string GetLabel(int offset, int length)
        {
            string label = Data.GetLabel(offset);
            return string.Format("{0," + (length * -1) + "}", label + (label.Length > 0 ? ":" : ""));
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

        // length forced to 6
        private static string GetEffectiveAddress(int offset, int length)
        {
            int ea = -1;
            switch (Data.GetFlag(offset))
            {
                case Data.FlagType.Opcode:
                    ea = Util.GetEffectiveAddress(offset);
                    break;
                case Data.FlagType.Pointer16Bit:
                    int ptr = Util.GetROMWord(offset);
                    ea = (Data.GetDataBank(offset) << 16) | ptr;
                    break;
                case Data.FlagType.Pointer24Bit:
                case Data.FlagType.Pointer32Bit:
                    ea = Util.GetROMLong(offset);
                    break;
            }
            return ea >= 0 ? Util.NumberToBaseString(ea, Util.NumberBase.Hexadecimal, 6) : "      ";
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
            return string.Format("{0," + (length * -1) + "}", Data.GetComment(offset));
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
    }
}

using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Core.export
{
    internal static class LogCreatorExtensions
    {
        public static string GetFormattedText(this ILogCreatorDataSource data, int offset, int bytes)
        {
            var text = "db \"";
            for (var i = 0; i < bytes; i++) 
                text += (char) data.GetRomByte(offset + i);
            
            return text + "\"";
        }
        
        public static int GetLineByteLength(this ILogCreatorDataSource data, int offset, int romSizeMax,
            int countPerLine)
        {
            var flagType = data.GetFlag(offset);

            if (flagType == FlagType.Opcode)
                return data.GetInstructionLength(offset);

            GetLineByteLengthMaxAndStep(flagType, out var max, out var step, countPerLine);

            var bankSize = data.GetBankSize();
            var myBank = offset / bankSize;

            var min = step;
            while (
                min < max &&
                offset + min < romSizeMax &&
                data.GetFlag(offset + min) == flagType &&
                data.Labels.GetLabelName(data.ConvertPCtoSnes(offset + min)) == "" &&
                (offset + min) / bankSize == myBank
            ) min += step;
            return min;
        }
        
        private static void GetLineByteLengthMaxAndStep(FlagType flagType, out int max, out int step, int dataPerLineSize)
        {
            max = 1; step = 1;

            switch (flagType)
            {
                case FlagType.Opcode:
                    break;
                case FlagType.Unreached:
                case FlagType.Operand:
                case FlagType.Data8Bit:
                case FlagType.Graphics:
                case FlagType.Music:
                case FlagType.Empty:
                    max = dataPerLineSize;
                    break;
                case FlagType.Text:
                    max = 21;
                    break;
                case FlagType.Data16Bit:
                    step = 2;
                    max = dataPerLineSize;
                    break;
                case FlagType.Data24Bit:
                    step = 3;
                    max = dataPerLineSize;
                    break;
                case FlagType.Data32Bit:
                    step = 4;
                    max = dataPerLineSize;
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
        }
        
        public static string GeneratePointerStr(this ILogCreatorDataSource data, int offset, int bytes)
        {
            var ia = -1;
            string format = "", param = "";
            switch (bytes)
            {
                case 2:
                    ia = (data.GetDataBank(offset) << 16) | data.GetRomWord(offset);
                    format = "dw {0}";
                    param = Util.NumberToBaseString(data.GetRomWord(offset), Util.NumberBase.Hexadecimal, 4, true);
                    break;
                case 3:
                    ia = data.GetRomLong(offset);
                    format = "dl {0}";
                    param = Util.NumberToBaseString(data.GetRomLong(offset), Util.NumberBase.Hexadecimal, 6, true);
                    break;
                case 4:
                    ia = data.GetRomLong(offset);
                    format = "dl {0}" +
                             $" : db {Util.NumberToBaseString(data.GetRomByte(offset + 3), Util.NumberBase.Hexadecimal, 2, true)}";
                    param = Util.NumberToBaseString(data.GetRomLong(offset), Util.NumberBase.Hexadecimal, 6, true);
                    break;
            }

            if (data.ConvertSnesToPc(ia) >= 0)
            {
                var labelName = data.Labels.GetLabelName(ia);
                if (labelName != "")
                    param = labelName;
            }

            return string.Format(format, param);
        }
        
        public static string GetFormattedBytes(this ILogCreatorDataSource data, int offset, int step, int bytes)
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
                        res += Util.NumberToBaseString(data.GetRomByte(offset + i), Util.NumberBase.Hexadecimal, 2,
                            true);
                        break;
                    case 2:
                        res += Util.NumberToBaseString(data.GetRomWord(offset + i), Util.NumberBase.Hexadecimal, 4,
                            true);
                        break;
                    case 3:
                        res += Util.NumberToBaseString(data.GetRomLong(offset + i), Util.NumberBase.Hexadecimal, 6,
                            true);
                        break;
                    case 4:
                        res += Util.NumberToBaseString(data.GetRomDoubleWord(offset + i), Util.NumberBase.Hexadecimal,
                            8, true);
                        break;
                }
            }

            return res;
        }
    }
}
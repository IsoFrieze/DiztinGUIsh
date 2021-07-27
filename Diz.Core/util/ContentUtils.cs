using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Diz.Core.model;
using JetBrains.Annotations;

namespace Diz.Core.util
{
    public static class ContentUtils
    {
        public static Dictionary<int, Label> ReadLabelsFromCsv(string importFilename, ref int errLine)
        {
            var newValues = new Dictionary<int, Label>();
            var lines = Util.ReadLines(importFilename).ToArray();

            var validLabelChars = new Regex(@"^([a-zA-Z0-9_\-]*)$");

            // NOTE: this is kind of a risky way to parse CSV files, won't deal with weirdness in the comments
            // section. replace with something better
            for (var i = 0; i < lines.Length; i++)
            {
                var label = new Label();

                errLine = i + 1;

                Util.SplitOnFirstComma(lines[i], out var labelAddress, out var remainder);
                Util.SplitOnFirstComma(remainder, out var labelName, out var labelComment);

                label.Name = labelName.Trim();
                label.Comment = labelComment;

                if (!validLabelChars.Match(label.Name).Success)
                    throw new InvalidDataException("invalid label name: " + label.Name);

                newValues.Add(int.Parse(labelAddress, NumberStyles.HexNumber, null), label);
            }

            errLine = -1;
            return newValues;
        }
        
#if PORTING_FROM_V3_BRANCH // this isn't needed yet but will be part of v3.0.
        public static void ImportLabelsFromCsv(this ILabelProvider labelProvider, string importFilename, bool replaceAll, ref int errLine)
        {
            var labelsFromCsv = ReadLabelsFromCsv(importFilename, ref errLine);
            
            if (replaceAll)
                labelProvider.DeleteAllLabels();
            
            foreach (var (key, value) in labelsFromCsv)
            {
                labelProvider.AddLabel(key, value, true);
            }
        }
#endif
        
        public static object SingleOrDefaultOfType<T>(this IEnumerable<T> enumerable, Type desiredType)
        {
            return enumerable.SingleOrDefault(item => item.GetType() == desiredType);
        }
        
        [CanBeNull]
        public static TDesired SingleOrDefaultOfType<TDesired, T>(this IEnumerable<T> enumerable)
        {
            return enumerable.OfType<TDesired>().SingleOrDefault();
        }
    }
}
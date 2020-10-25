using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Diz.Core.serialization.xml_serializer
{
    public static class RepeaterCompression
    {
        public static void Decompress(ref List<string> lines)
        {
            var output = new List<string>();

            foreach (var line in lines)
            {
                if (!line.StartsWith("r"))
                {
                    output.Add(line);
                    continue;
                }

                var split = line.Split(' ');
                if (split.Length != 3)
                    throw new InvalidDataException("Invalid repeater command");

                var count = int.Parse(split[1]);
                for (var i = 0; i < count; ++i)
                {
                    output.Add(split[2]);
                }
            }

            lines = output;
        }

        public static void Compress(ref List<string> lines)
        {
            if (lines.Count < 8)
                return; // forget it, too small to care.

            var output = new List<string>();

            var lastLine = lines[0];
            var consecutive = 1;

            // adjustable, just pick something > 8 or it's not worth the optimization.
            // we want to catch large consecutive blocks of data.
            const int minNumberRepeatsBeforeWeBother = 8;

            var totalLinesDebug = 0;

            for (var i = 1; i < lines.Count; ++i)
            {
                var line = lines[i];
                Debug.Assert(!line.StartsWith("r"));

                var different = line != lastLine;
                var finalLine = i == lines.Count - 1;

                if (!different)
                {
                    consecutive++;

                    if (!finalLine)
                        continue;
                }

                if (consecutive >= minNumberRepeatsBeforeWeBother)
                {
                    // replace multiple repeated lines with one new statement
                    output.Add($"r {consecutive.ToString()} {lastLine}");
                }
                else
                {
                    // output 1 or more copies of the last line
                    // this is also how we print single lines too
                    output.AddRange(Enumerable.Repeat(lastLine, consecutive).ToList());
                }

                if (finalLine && different)
                {
                    output.Add(line);
                    totalLinesDebug++;
                }

                totalLinesDebug += consecutive;

                lastLine = line;
                consecutive = 1;
            }

            Debug.Assert(totalLinesDebug == lines.Count);

            lines = output;
        }
    }
}

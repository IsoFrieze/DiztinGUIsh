using System.Collections.Generic;
using System.Diagnostics;

namespace Diz.Core.serialization.xml_serializer
{
    // kind of a manually made / crappy huffman table encoding type thing.
    // this is no great work of genius, more just some cheap hacks to reduce filesize.
    // these are based on one half-assembled ROM i was using and probably don't 
    // universally work perfectly.  I expect at some point we could collect a bunch of projects
    // and make a Table2, add that here without having to bump the XML revision version.
    //
    // alternatively, replace with something like gzip.
    // the only requirement is: output data should be vaguely human readable, preserve line numbers,
    // and be mergeable by git. in other words, we need to preserve newlines in the output.
    public static class SubstitutionCompression
    {
        private class CompressionEntry
        {
            public string LongTextPattern;
            public string ShortTextToEncode;
        }

        private static readonly List<CompressionEntry> Table1 = new()
        {
            new CompressionEntry {LongTextPattern = "0001E", ShortTextToEncode="ZQ"},
            new CompressionEntry {LongTextPattern = "B0001", ShortTextToEncode="Zq"},
            new CompressionEntry {LongTextPattern = "C0001", ShortTextToEncode="ZX"},
            new CompressionEntry {LongTextPattern = "B7E", ShortTextToEncode="Zx"},
            new CompressionEntry {LongTextPattern = "07F01", ShortTextToEncode="ZY"},
            new CompressionEntry {LongTextPattern = "0001D", ShortTextToEncode="Zy"},
            new CompressionEntry {LongTextPattern = "C7E", ShortTextToEncode="ZZ"},
            new CompressionEntry {LongTextPattern = "07E", ShortTextToEncode="Zz"},
            new CompressionEntry {LongTextPattern = "00001", ShortTextToEncode="ZS"},
            new CompressionEntry {LongTextPattern = "0001", ShortTextToEncode="Zs"},
        };

        public static void DecodeCompression_Table1(ref List<string> lines)
        {
            // performance: use for loops instead of foreach for squeezing some extra perf here
            
            var lineCount = lines.Count;
            var table1Count = Table1.Count;
            
            for (var i = 0; i < lineCount; ++i)
            {
                // shouldn't matter much but, apply in reverse to ensure it's done the same
                // way as the encoding process
                for (var j = table1Count - 1; j >= 0; --j) {
                    var e = Table1[j];
                    lines[i] = lines[i].Replace(e.ShortTextToEncode, e.LongTextPattern);
                }
            }
        }

        public static void EncodeCompression_Table1(ref List<string> lines)
        {
            // heavily optimized, please profile before changing significantly. avoid allocations/etc
            var lineCount = lines.Count;
            var table1Count = Table1.Count;
            
            for (var i = 0; i < lineCount; ++i)
            {
                for (var j = 0; j < table1Count; ++j)
                {
                    var e = Table1[j];
                    Debug.Assert(!lines[i].Contains(e.ShortTextToEncode));
                    lines[i] = lines[i].Replace(e.LongTextPattern, e.ShortTextToEncode);
                }
            }
        }
    }
}

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Diz.Core.serialization.xml_serializer
{
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
            for (var i = 0; i < lines.Count; ++i)
            {
                // shouldn't matter much but, apply in reverse to ensure it's done the same
                // way as the encoding process
                foreach (var e in Table1.Reverse<CompressionEntry>())
                {
                    lines[i] = lines[i].Replace(e.ShortTextToEncode, e.LongTextPattern);
                }
            }
        }

        public static void EncodeCompression_Table1(ref List<string> lines)
        {
            // kind of a manually made / crappy huffman table encoding type thing.
            // this is no great work of genius, more just some cheap hacks to reduce filesize.
            // these are based on one half-assembled ROM i was using and probably don't 
            // universally work perfectly.  I expect at some point we could collect a bunch of projects
            // and make a Table2, add that here without having to bump the file version.
            for (var i = 0; i < lines.Count; ++i)
            {
                foreach (var e in Table1)
                {
                    Debug.Assert(!lines[i].Contains(e.ShortTextToEncode));
                    lines[i] = lines[i].Replace(e.LongTextPattern, e.ShortTextToEncode);
                }
            }
        }
    }
}

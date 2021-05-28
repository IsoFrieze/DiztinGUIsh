using System;

namespace Diz.Core.import
{
    public partial class BsnesTraceLogImporter
    {
        // PERFORMANCE
        // this code exists ONLY for performance optimization.
        //
        // Turns out, using string.IndexOf() to find relative positions of text is SUPER SLOW.
        // Tracelogs from even a few seconds of SNES runtime can produce something like
        // 250 MILLION lines of text.
        //
        // Optimization: since the BSNES tracelogs contain lines that are always the same width with the data always
        // starting on the same offset. so, what we can take a known sample line, and then parse it ONE TIME.
        // we'll then save the position of each section of data
        //
        // This is basically the fastest way to parse text tracelogs (80 bytes per line). HOWEVER, if you really
        // want to move fast, you should use a binary file format (for BSNES, it's just 8 bytes per instruction,
        // and no text parsing required) 
        //
        // NOTE: we will make the assumption that lines in a file will always be the same width and offsets.
        // however, different versions of BSNES can output different line lengths, so always re-parse the first line
        // on each use of this file. it's up to the caller to figure that out.

        public class CachedTraceLineTextIndex
        {
            // index of the start of the info
            public int
                Addr,
                D,
                Db,
                Flags,
                Fn,
                Fv,
                Fm,
                Fx,
                Fd,
                Fi,
                Fz,
                Fc;

            public int LastLineLength { get; set; } = -1;

            public void RecomputeCachedIndicesBasedOn(string templateLine)
            {
                LastLineLength = templateLine.Length;

                int GetIndexOfDataAfterToken(string token)
                {
                    return templateLine.IndexOf(token, StringComparison.Ordinal) + token.Length;
                }

                Addr = 0;
                D = GetIndexOfDataAfterToken("D:");
                Db = GetIndexOfDataAfterToken("DB:");
                Flags = Db + 3;

                // flags: nvmxdizc
                Fn = Flags + 0;
                Fv = Flags + 1;
                Fm = Flags + 2;
                Fx = Flags + 3;
                Fd = Flags + 4;
                Fi = Flags + 5;
                Fz = Flags + 6;
                Fc = Flags + 7;
            }
        }
    }
}
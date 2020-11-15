using System.IO;

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
        // Optimization: since the BSNES tracelogs contain lines that are always 80 chars long with the data always
        // starting on the same offset. so, what we can take a known sample line, and then parse it ONE TIME.
        // we'll then save the position of each section of data
        //
        // This is basically the fastest way to parse text tracelogs (80 bytes per line). HOWEVER, if you really
        // want to move fast, you should use a binary file format (for BSNES, it's just 8 bytes per instruction,
        // and no text parsing required) 
        
        public class CachedTraceLineTextIndex
        {
            // index of the start of the info
            public readonly int
                Addr,
                D,
                Db,
                Flags,
                FN,
                FV,
                FM,
                FX,
                FD,
                FI,
                FZ,
                FC;

            public CachedTraceLineTextIndex()
            {
                if (SampleLineText.Length != 97)
                    throw new InvalidDataException("BSNES sample tracelog line must be EXACTLY 80 bytes in length");

                static int GetIndexOfDataAfterToken(string token)
                {
                    return SampleLineText.IndexOf(token) + token.Length;
                }

                Addr = 0;
                D = GetIndexOfDataAfterToken("D:");
                Db = GetIndexOfDataAfterToken("DB:");
                Flags = Db + 3;

                // flags: nvmxdizc
                FN = Flags + 0;
                FV = Flags + 1;
                FM = Flags + 2;
                FX = Flags + 3;
                FD = Flags + 4;
                FI = Flags + 5;
                FZ = Flags + 6;
                FC = Flags + 7;
            }
        }
        
        // NOTE: newer versions of BSNES use dots to represent flags.
        // i.e. instead of "nvmxdiZC" it would be "......ZC"
        public const string SampleLineText =
            @"028cde rep #$30               A:0004 X:0000 Y:0004 S:1fdd D:0000 DB:02 nvmxdiZC V:133 H: 654 F:36";

        private static readonly CachedTraceLineTextIndex CachedIdx = new CachedTraceLineTextIndex();
    }
}
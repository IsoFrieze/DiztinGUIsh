// #define EXTRA_DEBUG_CHECKS

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Core.serialization.xml_serializer
{
    public class RomByteEncoding
    {
        public class FlagEncodeEntry
        {
            public char C;
            public FlagType F;
        };

        private static readonly IReadOnlyCollection<FlagEncodeEntry> FlagEncodeTable = new List<FlagEncodeEntry> {
            new FlagEncodeEntry() {F = FlagType.Unreached, C = 'U'},

            new FlagEncodeEntry() {F = FlagType.Opcode, C = '+'},
            new FlagEncodeEntry() {F = FlagType.Operand, C = '.'},

            new FlagEncodeEntry() {F = FlagType.Graphics, C = 'G'},
            new FlagEncodeEntry() {F = FlagType.Music, C = 'M'},
            new FlagEncodeEntry() {F = FlagType.Empty, C = 'X'},
            new FlagEncodeEntry() {F = FlagType.Text, C = 'T'},

            new FlagEncodeEntry() {F = FlagType.Data8Bit, C = 'A'},
            new FlagEncodeEntry() {F = FlagType.Data16Bit, C = 'B'},
            new FlagEncodeEntry() {F = FlagType.Data24Bit, C = 'C'},
            new FlagEncodeEntry() {F = FlagType.Data32Bit, C = 'D'},

            new FlagEncodeEntry() {F = FlagType.Pointer16Bit, C = 'E'},
            new FlagEncodeEntry() {F = FlagType.Pointer24Bit, C = 'F'},
            new FlagEncodeEntry() {F = FlagType.Pointer32Bit, C = 'G'},
        };

        private readonly StringBuilder cachedPadSb = new StringBuilder(LineMaxLen);

        private const int LineMaxLen = 9;

        // note: performance-intensive function. be really careful when adding stuff here.
        public RomByte DecodeRomByte(string line)
        {
            var input = PrepLine(line);

            var newByte = new RomByte();

            var flagTxt = input[0];
            var otherFlags1 = Fake64Encoding.DecodeHackyBase64(input[1]);
            newByte.DataBank = ByteUtil.ByteParseHex2(input[2], input[3]);
            newByte.DirectPage = (int)ByteUtil.ByteParseHex4(input[4], input[5], input[6], input[7]);
            newByte.Arch = (Architecture)(ByteUtil.ByteParseHex1(input[8]) & 0x3);

            #if EXTRA_DEBUG_CHECKS
            Debug.Assert(Fake64Encoding.EncodeHackyBase64(otherFlags1) == o1_str);
            #endif

            newByte.XFlag = ((otherFlags1 >> 2) & 0x1) != 0;
            newByte.MFlag = ((otherFlags1 >> 3) & 0x1) != 0;
            newByte.Point = (InOutPoint)((otherFlags1 >> 4) & 0xF);

            var found = false;
            foreach (var e in FlagEncodeTable)
            {
                if (e.C != flagTxt)
                    continue;

                newByte.TypeFlag = e.F;
                found = true;
                break;
            }
            if (!found)
                throw new InvalidDataException("Unknown FlagType");

            return newByte;
        }

        // perf note: re-uses a cached StringBuilder for subsequent runs
        private StringBuilder PrepLine(string line)
        {
            if (cachedPadSb.Length == 0)
                cachedPadSb.Append("000000000"); // any 9 chars

            // light decompression. ensure our line is always 9 chars long.
            // if any characters are missing, pad them with zeroes
            //
            // perf: string.PadRight() is simpler but too slow, so do it by hand
            var inSize = line.Length;
            for (var i = 0; i < LineMaxLen; ++i)
            {
                cachedPadSb[i] = i < inSize ? line[i] : '0';
            }

            Debug.Assert(cachedPadSb.Length == LineMaxLen);

            return cachedPadSb;
        }

        public string EncodeByte(RomByte instance)
        {
            // use a custom formatter here to save space. there are a LOT of ROMBytes.
            // despite that we're still going for:
            // 1) text only for slightly human readability
            // 2) mergability in git/etc
            //
            // some of this can be unpacked further to increase readability without
            // hurting the filesize too much. figure out what's useful.
            //
            // sorry, I know the encoding looks insane and weird and specific.  this reduced my
            // save file size from 42MB to less than 13MB

            // NOTE: must be uppercase letter or "=" or "-"
            // if you add things here, make sure you understand the compression settings above.
            var flagTxt = ' ';
            foreach (var e in FlagEncodeTable)
            {
                if (e.F == instance.TypeFlag)
                {
                    flagTxt = e.C;
                    break;
                }
            }

            if (flagTxt == ' ')
                throw new InvalidDataException("Unknown FlagType");

            // max 6 bits if we want to fit in 1 base64 ASCII digit
            byte otherFlags1 = (byte)(
                (instance.XFlag ? 1 : 0) << 2 | // 1 bit
                (instance.MFlag ? 1 : 0) << 3 | // 1 bit
                (byte)instance.Point << 4   // 4 bits
                                            // LEAVE OFF THE LAST 2 BITS. it'll mess with the base64 below
            );
            // reminder: when decoding, have to cut off all but the first 6 bits
            var o1Str = Fake64Encoding.EncodeHackyBase64(otherFlags1);
            Debug.Assert(Fake64Encoding.DecodeHackyBase64(o1Str) == otherFlags1);

            if (!instance.XFlag && !instance.MFlag && instance.Point == 0)
            {
                Debug.Assert(o1Str == '0'); // sanity
            }

            // this is basically going to be "0" almost 100% of the time.
            // we'll put it on the end of the string so it's most likely not output
            byte otherFlags2 = (byte)(
                (byte)instance.Arch << 0 // 2 bits
            );
            var o2Str = otherFlags2.ToString("X1"); Debug.Assert(o2Str.Length == 1);

            // ordering: put DB and D on the end, they're likely to be zero and compressible
            var sb = new StringBuilder(9);
            sb.Append(flagTxt);
            sb.Append(o1Str);
            sb.Append(instance.DataBank.ToString("X2"));
            sb.Append(instance.DirectPage.ToString("X4"));
            sb.Append(o2Str);

            Debug.Assert(sb.Length == 9);
            var data = sb.ToString();

            // light compression: chop off any trailing zeroes.
            // this alone saves a giant amount of space.
            data = data.TrimEnd(new char[] { '0' });

            // future compression but dilutes readability:
            // if type is opcode or operand with same flags, combine those into 1 type.
            // i.e. take an opcode with 3 operands, represent it using one digit in the file.
            // instead of "=---", we swap with "+" or something. small optimization.

            return data;
        }
    }
}

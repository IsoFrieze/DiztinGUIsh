using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ExtendedXmlSerializer.ContentModel;
using ExtendedXmlSerializer.ContentModel.Format;

// everything else in the savefiles is straight up normal XML, but,
// the amount of bytes for the ROM metadata can be huge.
// by using a custom serializer for this one section, we can cut a 4MB rom savefile down from ~45MB to ~1.5MB
//
// This uses some hacky compression methods designed to preserve:
// 1) newlines
// 2) slight human readability for merging
// 3) using pattern substitution tables that won't change from PC to PC.
//
// The easiest thing would be use zlib (and we can use it with the output of the entire XML if we want)
// but, for projects with multiple collaborators using diztinGUIsh, mergability in text/git/editors/etc
// is a must.  We aim for a tradeoff between decent compression and some small semblance of human readability.
//
// It's not.. super-pretty code, but it compresses well.
namespace DiztinGUIsh.loadsave.xml_serializer
{
    sealed class RomBytesSerializer : ISerializer<RomBytes>
    {
        // let the outer XML class do the heavy lifting on versioning.
        // but, let's add one here just because this specific class is complex.
        private const int CURRENT_DATA_FORMAT_VERSION = 200;

        public static RomBytesSerializer Default { get; } = new RomBytesSerializer();

        RomBytesSerializer() { }

        public bool compress_groupblock = true;
        public bool compress_using_table_1 = true;

        public RomBytes Get(IFormatReader parameter)
        {
            var romBytesOut = new RomBytes();

            var lines = parameter.Content().Split(new char[] { '\n' }, 3).ToList();
            var options = lines[1].Split(new char[] { ',' }).ToList();
            lines = lines[2].Split(new char[] { '\n' }).ToList();
            if (lines[lines.Count - 1] == "")
                lines.RemoveAt(lines.Count - 1);

            CheckForCompatibleVersion(options);

            // always apply options in same order here and in saving function
            if (options.Exists(s => s == "compress_table_1"))
                DecodeCompression_Table1(ref lines);

            if (options.Exists(s => s == "compress_groupblocks"))
                DecodeCompression_GroupsBlocks(ref lines);

            int lineNum = 0;
            try
            {
                foreach (var line in lines)
                {
                    romBytesOut.Add(DecodeRomByte(line));
                    lineNum++;
                }
            }
            catch (Exception ex)
            {
                ex.Data.Add("ParseLineNum", "Near line#" + lineNum);
                throw;
            }

            return romBytesOut;
        }

        private static void CheckForCompatibleVersion(IEnumerable<string> options)
        {
            try
            {
                var versionOption = options.SingleOrDefault(s => s.Contains("version:"));

                if (versionOption == null)
                {
                    throw new InvalidDataException(
                        $"Exactly 1 'version' tag must be in options, unable to continue");
                }

                var split = versionOption.Split(':');
                Debug.Assert(split.Length == 2);
                if (!int.TryParse(split[1], out var version_num))
                    throw new InvalidDataException(
                        $"Couldn't parse version # from version tag");

                if (version_num > CURRENT_DATA_FORMAT_VERSION)
                    throw new InvalidDataException(
                        $"Newer file format detected: {version_num}. This version of distinguish only supports data table formats up to {CURRENT_DATA_FORMAT_VERSION}.");

                // In the future, we can add migrations here for older version. For now, just reject it.
                if (version_num < CURRENT_DATA_FORMAT_VERSION)
                    throw new InvalidDataException(
                        $"Newer file format detected: {version_num}. This version of distinguish only supports data table formats up to {CURRENT_DATA_FORMAT_VERSION}.");
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Data table loader: Version error: {ex.Message}");
            }
        }

        public void Write(IFormatWriter writer, RomBytes instance)
        {
            var options = new List<string>
            {
                $"version:{CURRENT_DATA_FORMAT_VERSION}",
            };

            var lines = new List<string>();
            foreach (var rb in instance)
            {
                var encoded = EncodeByte(rb);
                lines.Add(encoded);

                // debug check, optional:
                var decoded = DecodeRomByte(encoded);
                Debug.Assert(decoded.EqualsButNoRomByte(rb));
            }

            if (compress_groupblock)
            {
                options.Add("compress_groupblocks");
                ApplyCompression_GroupsBlocks(ref lines);
            }

            if (compress_using_table_1)
            {
                options.Add("compress_table_1");
                EncodeCompression_Table1(ref lines);
            }

            writer.Content($"\n{string.Join(",", options)}\n");

            foreach (var line in lines)
            {
                writer.Content(line + "\n");
            }
        }

        class CompressionEntry
        {
            public string LongTextPattern;
            public string ShortTextToEncode;
        }
        private static List<CompressionEntry> table1 = new List<CompressionEntry>
        {
            new CompressionEntry() {LongTextPattern = "0001E", ShortTextToEncode="ZQ"},
            new CompressionEntry() {LongTextPattern = "B0001", ShortTextToEncode="Zq"},
            new CompressionEntry() {LongTextPattern = "C0001", ShortTextToEncode="ZX"},
            new CompressionEntry() {LongTextPattern = "B7E", ShortTextToEncode="Zx"},
            new CompressionEntry() {LongTextPattern = "07F01", ShortTextToEncode="ZY"},
            new CompressionEntry() {LongTextPattern = "0001D", ShortTextToEncode="Zy"},
            new CompressionEntry() {LongTextPattern = "C7E", ShortTextToEncode="ZZ"},
            new CompressionEntry() {LongTextPattern = "07E", ShortTextToEncode="Zz"},
            new CompressionEntry() {LongTextPattern = "00001", ShortTextToEncode="ZS"},
            new CompressionEntry() {LongTextPattern = "0001", ShortTextToEncode="Zs"},
        };

        private void DecodeCompression_Table1(ref List<string> lines)
        {
            for (int i = 0; i < lines.Count; ++i)
            {
                // shouldn't matter much but, apply in reverse to ensure it's done the same
                // way as the encoding process
                foreach (var e in table1.Reverse<CompressionEntry>())
                {
                    lines[i] = lines[i].Replace(e.ShortTextToEncode, e.LongTextPattern);
                }
            }
        }

        private void EncodeCompression_Table1(ref List<string> lines)
        {
            // kind of a manually made / crappy huffman table encoding type thing.
            // this is no great work of genius, more just some cheap hacks to reduce filesize.
            // these are based on one half-assembled ROM i was using and probably don't 
            // universally work perfectly.  I expect at some point we could collect a bunch of projects
            // and make a Table2, add that here without having to bump the file version.
            for (int i = 0; i < lines.Count; ++i)
            {
                foreach (var e in table1)
                {
                    Debug.Assert(!lines[i].Contains(e.ShortTextToEncode));
                    lines[i] = lines[i].Replace(e.LongTextPattern, e.ShortTextToEncode);
                }
            }
        }

        private void DecodeCompression_GroupsBlocks(ref List<string> lines)
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
                for (int i = 0; i < count; ++i)
                {
                    output.Add(split[2]);
                }
            }

            lines = output;
        }

        private void ApplyCompression_GroupsBlocks(ref List<string> lines)
        {
            if (lines.Count < 8)
                return; // forget it, too small to care.

            var output = new List<string>();

            var lastline = lines[0];
            var consecutive = 1;

            // adjustable, just pick something > 8 or it's not worth the optimization.
            // we want to catch large consecutive blocks of data.
            const int min_number_repeats_before_we_bother = 8;

            int totalLinesDebug = 0;

            for (var i = 1; i < lines.Count; ++i)
            {
                var line = lines[i];
                Debug.Assert(!line.StartsWith("r"));

                bool different = line != lastline;
                bool finalLine = i == lines.Count-1;

                if (!different) {
                    consecutive++;

                    if (!finalLine)
                        continue;

                    // special case for the final line.
                    // since our loop only ever prints out the LAST line, we have to handle this separately.
                    consecutive++;
                }

                if (consecutive >= min_number_repeats_before_we_bother)
                {
                    // replace multiple repeated lines with one new statement
                    output.Add($"r {consecutive.ToString()} {lastline}");
                }
                else
                {
                    // output 1 or more copies of the last line
                    // this is also how we print single lines too
                    output.AddRange(Enumerable.Repeat(lastline, consecutive).ToList());
                }

                if (finalLine && different) {
                    output.Add(line);
                    totalLinesDebug++;
                }

                totalLinesDebug += consecutive;

                lastline = line;
                consecutive = 1;
            }

            Debug.Assert(totalLinesDebug == lines.Count);

            lines = output;
        }

        public class FlagEncodeEntry
        {
            public string c;
            public Data.FlagType f;
        };

        private static readonly List<FlagEncodeEntry> flagEncodeTable = new List<FlagEncodeEntry> {
            new FlagEncodeEntry() {f = Data.FlagType.Unreached, c = "U"},

            new FlagEncodeEntry() {f = Data.FlagType.Opcode, c = "+"},
            new FlagEncodeEntry() {f = Data.FlagType.Operand, c = "."},

            new FlagEncodeEntry() {f = Data.FlagType.Graphics, c = "G"},
            new FlagEncodeEntry() {f = Data.FlagType.Music, c = "M"},
            new FlagEncodeEntry() {f = Data.FlagType.Empty, c = "X"},
            new FlagEncodeEntry() {f = Data.FlagType.Text, c = "T"},

            new FlagEncodeEntry() {f = Data.FlagType.Data8Bit, c = "A"},
            new FlagEncodeEntry() {f = Data.FlagType.Data16Bit, c = "B"},
            new FlagEncodeEntry() {f = Data.FlagType.Data24Bit, c = "C"},
            new FlagEncodeEntry() {f = Data.FlagType.Data32Bit, c = "D"},

            new FlagEncodeEntry() {f = Data.FlagType.Pointer16Bit, c = "E"},
            new FlagEncodeEntry() {f = Data.FlagType.Pointer24Bit, c = "F"},
            new FlagEncodeEntry() {f = Data.FlagType.Pointer32Bit, c = "G"},
        };

        private ROMByte DecodeRomByte(string line)
        {
            var newByte = new ROMByte();

            // light decompression. always 9 chars long
            line = line.PadRight(9, '0');
            Debug.Assert(line.Length == 9);

            var asHex = System.Globalization.NumberStyles.HexNumber;

            var flagTxt = line.Substring(0, 1);
            var o1_str = line.Substring(1, 1);
            newByte.DataBank = byte.Parse(line.Substring(2, 2), asHex);
            newByte.DirectPage = int.Parse(line.Substring(4, 4), asHex);
            var o2_str = byte.Parse(line.Substring(8, 1), asHex);

            newByte.Arch = (Data.Architecture)((o2_str >> 0) & 0x3);

            var otherFlags1 = DecodeHackyBase64(o1_str);
            Debug.Assert(EncodeHackyBase64(otherFlags1) == o1_str);

            newByte.XFlag = ((otherFlags1 >> 2) & 0x1) != 0;
            newByte.MFlag = ((otherFlags1 >> 3) & 0x1) != 0;
            newByte.Point = (Data.InOutPoint)((otherFlags1 >> 4) & 0xF);

            bool found = false;
            foreach (var e in flagEncodeTable)
            {
                if (e.c == flagTxt)
                {
                    newByte.TypeFlag = e.f;
                    found = true;
                    break;
                }
            }
            if (!found)
                throw new InvalidDataException("Unknown FlagType");

            return newByte;
        }
        
        private string EncodeByte(ROMByte instance)
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
            string flagTxt = "";
            foreach (var e in flagEncodeTable)
            {
                if (e.f == instance.TypeFlag)
                {
                    flagTxt = e.c;
                    break;
                }
            }

            if (flagTxt == "")
                throw new InvalidDataException("Unknown FlagType");

            // max 6 bits if we want to fit in 1 base64 ASCII digit
            byte otherFlags1 = (byte)(
                (instance.XFlag ? 1 : 0) << 2 | // 1 bit
                (instance.MFlag ? 1 : 0) << 3 | // 1 bit
                (byte)instance.Point << 4   // 4 bits
                // LEAVE OFF THE LAST 2 BITS. it'll mess with the base64 below
            );
            // reminder: when decoding, have to cut off all but the first 6 bits
            var o1_str = EncodeHackyBase64(otherFlags1);
            Debug.Assert(DecodeHackyBase64(o1_str) == otherFlags1);

            if (!instance.XFlag && !instance.MFlag && instance.Point == 0)
                Debug.Assert(o1_str == "0"); // sanity

            // this is basically going to be "0" almost 100% of the time.
            // we'll put it on the end of the string so it's most likely not output
            byte otherFlags2 = (byte)(
                (byte)instance.Arch << 0 // 2 bits
            );
            var o2_str = otherFlags2.ToString("X1"); Debug.Assert(o2_str.Length == 1);

            // ordering: put DB and D on the end, they're likely to be zero and compressible
            string data =
                flagTxt + // 1
                o1_str +  // 1
                instance.DataBank.ToString("X2") +  // 2
                instance.DirectPage.ToString("X4") + // 4
                o2_str; // 1

            Debug.Assert(data.Length == 9);

            // light compression: chop off any trailing zeroes.
            // this alone saves a giant amount of space.
            data = data.TrimEnd(new char[] { '0' });

            // future compression but dilutes readability:
            // if type is opcode or operand with same flags, combine those into 1 type.
            // i.e. take an opcode with 3 operands, represent it using one digit in the file.
            // instead of "=---", we swap with "+" or something. small optimization.

            return data;
        }

        private static void Swap0forA(ref string input)
        {
            // *****dumbest thing in the entire world*****
            //
            // the more zeroes our ouput text has, the more compressed we get later.
            // so, let's swap base64's "A" (index 0) for "0" (index 52).
            //
            // if you got here after being really fucking confused about why
            // your Base64 encoding algo wasn't working, then I owe you a beer. super-sorry.
            //
            // you are now allowed to flip your desk over. say it with me
            // "Damnit Dom!!! Y U DO THIS"
            if (input == "A") 
                input = "0";
            else if (input == "0") 
                input = "A";
        }

        // base64 decode, but we swap "0" for "A"
        private static byte DecodeHackyBase64(string input)
        {
            Swap0forA(ref input);
            var superHackyBase64 = input + "A=="; // we dont care about > 6 bits, so we can fake this.
            var result = Convert.FromBase64CharArray(superHackyBase64.ToCharArray(), 0, superHackyBase64.Length);
            Debug.Assert(result.Length == 1);
            return result[0];
        }

        // base64 encode, but we swap "0" for "A"
        private static string EncodeHackyBase64(byte input)
        {
            var output = System.Convert.ToBase64String(new byte[] {input});
            Debug.Assert(output.Length == 4);
            Debug.Assert(output.Substring(1) == "A==");
            output = output.Remove(1);
            Swap0forA(ref output);
            return output;
        }
    }
}
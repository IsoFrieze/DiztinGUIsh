using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Diz.Core.util
{
    public static class ByteUtil
    {
        // take input addresses that can be in any formats that look like this, and convert to an int.
        // This is useful if pasting addresses from other editors/tools/asm/etc trying to get a clean address.
        // C0FFFF
        // $C0FFFF
        // C7/AAAA
        // $C6/BBBB
        public static bool StripFormattedAddress(ref string addressTxt, NumberStyles style, out int address)
        {
            address = -1;

            if (string.IsNullOrEmpty(addressTxt))
                return false;

            var inputText = new string(Array.FindAll(addressTxt.ToCharArray(), (c =>
                    (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                )));

            if (!int.TryParse(inputText, style, null, out address))
                return false;

            addressTxt = inputText;
            return true;
        }

        public delegate int AddressConverter(int address);

        public static int ReadStringsTable(byte[] bytes, int startingIndex, int stringsPerEntry, AddressConverter converter, Action<int, string[]> processTableEntry)
        {
            var strings = new List<string>();

            var pos = startingIndex;
            var numTableEntries = ConvertByteArrayToInt32(bytes, pos);
            pos += 4;

            for (var entry = 0; entry < numTableEntries; ++entry)
            {
                var offset = converter(ConvertByteArrayToInt32(bytes, pos));
                pos += 4;

                strings.Clear();
                for (var j = 0; j < stringsPerEntry; ++j)
                {
                    pos += ReadNullTerminatedString(bytes, pos, out var str);
                    strings.Add(str);
                }
                processTableEntry(offset, strings.ToArray());
            }

            return pos - startingIndex;
        }

        public static int ReadNullTerminatedString(byte[] bytes, int startingOffset, out string str)
        {
            str = "";
            var pos = startingOffset;
            while (bytes[pos] != 0)
                str += (char)bytes[pos++];
            pos++;
            return pos - startingOffset;
        }

        public static byte[] IntegerToByteArray(uint val)
        {
            return new[]
            {
                (byte)val,
                (byte)(val >> 8),
                (byte)(val >> 16),
                (byte)(val >> 24)
            };
        }

        public static void IntegerIntoByteArray(uint src, IList<byte> dst, int dstStartingOffset)
        {
            var arr = IntegerToByteArray(src);
            for (var i = 0; i < arr.Length; i++) 
                dst[dstStartingOffset + i] = arr[i];
        }

        public static void AppendIntegerToByteList(uint src, IList<byte> dst)
        {
            var arr = IntegerToByteArray(src);
            foreach (var t in arr)
                dst.Add(t);
        }

        public static uint ConvertByteArrayToUInt32(IReadOnlyList<byte> src, int srcStartingOffset = 0)
        {
            return
                src[srcStartingOffset] |
                ((uint)src[srcStartingOffset + 1] << 8) |
                ((uint)src[srcStartingOffset + 2] << 16) |
                ((uint)src[srcStartingOffset + 3] << 24);
        }

        public static int ConvertByteArrayToInt32(IReadOnlyList<byte> src, int srcStartingOffset = 0)
        {
            return
                src[srcStartingOffset] |
                (src[srcStartingOffset + 1] << 8) |
                (src[srcStartingOffset + 2] << 16) |
                (src[srcStartingOffset + 3] << 24);
        }

        public static int ByteArrayToInt24(IReadOnlyList<byte> src, int srcStartingOffset = 0)
        {
            return
                src[srcStartingOffset] |
                (src[srcStartingOffset + 1] << 8) |
                (src[srcStartingOffset + 2] << 16);
        }

        public static int ByteArrayToInt16(IReadOnlyList<byte> src, int srcStartingOffset = 0)
        {
            return
                src[srcStartingOffset] |
                (src[srcStartingOffset + 1] << 8);
        }

        public static byte[] StringToNullTermByteArray(string src)
        {
            var array = new byte[src.Length + 1];
            for (var i = 0; i < src.Length; i++) 
                array[i] = (byte)src[i];
            
            array[src.Length] = 0;
            return array;
        }
        
        
        // there's builtin C# code that parses Hex digits from a string, BUT, it's super-slow:
        // slow --> Byte.Parse(x, isHex) 
        // 
        // this is less flexible but way faster, crucial for fast sections of our code. 
        // idea credit: Daniel-Lemire
        private static readonly int[] HexAsciiToDigit = {
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 0,  1,  2,  3,  4,  5,  6,  7,  8,
            9,  -1, -1, -1, -1, -1, -1, -1, 10, 11, 12, 13, 14, 15, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, 10, 11, 12, 13, 14, 15, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1};

        // 4bit value
        public static byte ByteParseHex1(char hexChar)
        {
            var result = HexAsciiToDigit[hexChar];
            if (result == -1)
                throw new InvalidDataException("Invalid hex digit");

            return (byte)result;
        }

        // 8bit value
        public static byte ByteParseHex2(char hexChar1, char hexChar2)
        {
            return (byte)(ByteParseHex1(hexChar1) * 0x10 + ByteParseHex1(hexChar2));
        }

        // 16bit value
        public static uint ByteParseHex4(char hexChar1, char hexChar2, char hexChar3, char hexChar4)
        {
            return
                ByteParseHex1(hexChar1) * 0x1000u +
                ByteParseHex1(hexChar2) * 0x100u +
                ByteParseHex1(hexChar3) * 0x10u +
                ByteParseHex1(hexChar4);
        }

        public static uint ByteParseHex(string str)
        {
            return ByteParseHex(str, 0, str.Length);
        }

        // note: this helper isn't quite as fast, use one of the other ByteParseHex1/2/3/4() functions directly
        // if you need the best performance.
        //
        // this function is a faster but more specific implementation of:
        // Convert.ToInt32(line.Substring(startIndex, length), 16);
        public static uint ByteParseHex(string str, int strStartIndex, int numHexDigits)
        {
            if (numHexDigits <= 0 || numHexDigits > 8)
                throw new ArgumentException("numHexDigits out of range");

            var offset = numHexDigits - 1;
            var multiplier = 1u;
            var result = 0u;
            
            for (var i = 0; i < numHexDigits; ++i)
            {
                if (numHexDigits >= i + 1)
                {
                    result += ByteParseHex1(str[strStartIndex + offset]) * multiplier;
                    offset--;
                }
                multiplier *= 0x10;
            }

            return result;
        }
        
        // Cart names in the ROM use "shift-JIS" encoding, which is ASCII with extra japanese chars.
        // SNES games use it for their text fields in the header, particularly the cartridge title.
        // This needs to be parsed carefully.
        private static bool _registeredEncoding;
        public static Encoding ShiftJisEncoding
        {
            get
            {
                if (!_registeredEncoding)
                {
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    _registeredEncoding = true;
                }

                return Encoding.GetEncoding(932);
            }
        }

        public static string ReadShiftJisEncodedString(byte[] buffer, int index = 0, int count = -1) => 
            ReadStringFromByteArray(buffer, index, count, ShiftJisEncoding);
        
        public static byte[] ConvertUtf8ToShiftJisEncodedBytes(string str) => 
            Encoding.Convert(Encoding.UTF8, ShiftJisEncoding, Encoding.UTF8.GetBytes(str));
        
        public static byte[] GetRawShiftJisBytesFromStr(string inputStr)
        {
            var shiftJisEncodedBytes = ConvertUtf8ToShiftJisEncodedBytes(inputStr);
            var shiftJisStr = ShiftJisEncoding.GetString(shiftJisEncodedBytes);
            var rawShiftJisBytes = ShiftJisEncoding.GetBytes(shiftJisStr);
            return rawShiftJisBytes;
        }
        
        // pad any empty space (for a total of 21 bytes) using the space (0x20) character
        public static byte[] PadCartridgeTitleBytes(byte[] srcBytes) => 
            PadBytes(srcBytes, RomUtil.LengthOfTitleName - srcBytes.Length);

        private static byte[] PadBytes(IEnumerable<byte> srcBytes, int numPadBytesNeeded) => 
            srcBytes.Concat(Enumerable.Repeat((byte) 0x20, numPadBytesNeeded)).ToArray();

        // read a fixed length string from an array of bytes. does not check for null termination.
        // allows using a non-UTF8 encoding
        public static string ReadStringFromByteArray(byte[] bytes, int index = 0, int count = -1, Encoding srcEncoding = null)
        {
            var utfBytes = Encoding.Convert(
                srcEncoding ?? Encoding.UTF8, 
                Encoding.UTF8, 
                bytes, 
                index, 
                count != -1 ? count : bytes.Length);
            
            return Encoding.UTF8.GetString(utfBytes);
        }
    }
}

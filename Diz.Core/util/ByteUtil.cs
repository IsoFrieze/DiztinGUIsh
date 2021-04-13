using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Diz.Core.util
{
    public static class ByteUtil
    {
        // take input addresses that be in any formats that look like this, and convert to an int.
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

            var inputText = new string(Array.FindAll<char>(addressTxt.ToCharArray(), (c =>
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
            var numTableEntries = ByteArrayToInt32(bytes, pos);
            pos += 4;

            for (var entry = 0; entry < numTableEntries; ++entry)
            {
                var offset = converter(ByteArrayToInt32(bytes, pos));
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

        public static byte[] IntegerToByteArray(int a)
        {
            return new byte[]
            {
                (byte)a,
                (byte)(a >> 8),
                (byte)(a >> 16),
                (byte)(a >> 24)
            };
        }

        public static void IntegerIntoByteArray(int a, byte[] data, int offset)
        {
            byte[] arr = IntegerToByteArray(a);
            for (int i = 0; i < arr.Length; i++) data[offset + i] = arr[i];
        }

        public static void IntegerIntoByteList(int a, List<byte> list)
        {
            byte[] arr = IntegerToByteArray(a);
            foreach (var t in arr)
                list.Add(t);
        }

        public static int ByteArrayToInt32(byte[] data, int offset = 0)
        {
            return
                data[offset] |
                (data[offset + 1] << 8) |
                (data[offset + 2] << 16) |
                (data[offset + 3] << 24);
        }

        public static int ByteArrayToInt24(byte[] data, int offset = 0)
        {
            return
                data[offset] |
                (data[offset + 1] << 8) |
                (data[offset + 2] << 16);
        }

        public static int ByteArrayToInt16(byte[] data, int offset = 0)
        {
            return
                data[offset] |
                (data[offset + 1] << 8);
        }

        public static byte[] StringToNullTermByteArray(string s)
        {
            var array = new byte[s.Length + 1];
            for (var i = 0; i < s.Length; i++) array[i] = (byte)s[i];
            array[s.Length] = 0;
            return array;
        }
        
        
        // there's builtin C# code that parses Hex digits from a string, BUT, it's super-slow:
        // slow --> Byte.Parse(x, isHex) 
        // 
        // this is less flexible but way faster, crucial for fast sections of our code. 
        // idea credit: Daniel-Lemire
        public static readonly int[] HexAsciiToDigit = {
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

        // note: likely isn't quite as fast, use one of the other ByteParseHex1/2/3/4() functions directly
        // if you have to care about performance.
        //
        // this function is a faster but more specific version of: Convert.ToInt32(line.Substring(startIndex, length), 16);
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
    }
}

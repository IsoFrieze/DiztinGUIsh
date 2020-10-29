using System;
using System.Collections.Generic;
using System.Globalization;

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

        public static byte[] StringToByteArray(string s)
        {
            byte[] array = new byte[s.Length + 1];
            for (int i = 0; i < s.Length; i++) array[i] = (byte)s[i];
            array[s.Length] = 0;
            return array;
        }
    }
}

using System.Collections.Generic;
using System.Linq;

namespace Diz.Core.util
{
    public static class Fake64Encoding
    {
        // this is base64 EXCEPT:
        // 1) char "A" and "0" swapped (my data format compresses things with "0" better)
        // 2) only supports 6 bits of input data to encode
        // DONT BE FOOLED :)
        public const string Fake64Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
                                           "abcdefghijklmnopqrstuvwxyz" +
                                           "0123456789" +
                                           "+/";

        public static readonly byte[] Fake64Values =
        {
            208, 4, 8, 12, 16, 20, 24, 28, 32, 36, 40, 44, 48,
            52, 56, 60, 64, 68, 72, 76, 80, 84, 88, 92, 96, 100,
            104, 108, 112, 116, 120, 124, 128, 132, 136, 140, 144,
            148, 152, 156, 160, 164, 168, 172, 176, 180, 184,
            188, 192, 196, 200, 204, 0, 212, 216, 220, 224, 228,
            232, 236, 240, 244, 248, 252
        };

        private static readonly object InitLock = new object();

        public static IEnumerable<object[]> CharToByte =>
            Fake64Chars.Zip(
                Fake64Values, (c, b) => new object[] { c, b });

        private static Dictionary<char, byte> _fake64CharToByte;
        public static Dictionary<char, byte> Fake64CharToByte
        {
            get
            {
                lock (InitLock)
                {
                    if (_fake64CharToByte != null)
                        return _fake64CharToByte;

                    _fake64CharToByte = new Dictionary<char, byte>();
                    foreach (var kvp in CharToByte)
                    {
                        _fake64CharToByte[(char) kvp[0]] = (byte) kvp[1];
                    }

                    return _fake64CharToByte;
                }
            }
        }

        private static Dictionary<byte, char> _fake64ByteToChar;
        public static Dictionary<byte, char> Fake64ByteToChar
        {
            get
            {
                lock (InitLock)
                {
                    if (_fake64ByteToChar != null)
                        return _fake64ByteToChar;

                    _fake64ByteToChar = new Dictionary<byte, char>();
                    foreach (var kvp in CharToByte)
                    {
                        _fake64ByteToChar[(byte) kvp[1]] = (char) kvp[0];
                    }

                    return _fake64ByteToChar;
                }
            }
        }

        // we could use the system base64 stuff but, this is faster.

        public static byte DecodeHackyBase64(char input)
        {
            return Fake64CharToByte[input];
        }

        public static char EncodeHackyBase64(byte input)
        {
            return Fake64ByteToChar[input];
        }
    }
}

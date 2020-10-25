using System.Collections.Generic;
using System.Linq;
using Diz.Core.serialization.xml_serializer;
using Diz.Core.util;
using Xunit;

namespace Diz.Test
{
    public static class Fake64Test
    {
        public class Fake64Tests
        {
            public static IEnumerable<object[]> CharToByte =>
                Fake64Encoding.Fake64Chars.Zip(
                    Fake64Encoding.Fake64Values, (c, b) => new object[] { c, b });

            [Theory]
            [MemberData(nameof(CharToByte))]
            public static void TestDecodeFake64(char c, byte b)
            {
                Assert.Equal(b, Fake64Encoding.DecodeHackyBase64(c));
            }

            [Theory]
            [MemberData(nameof(CharToByte))]
            public static void TestEncodeFake64(char c, byte b)
            {
                Assert.Equal(c, Fake64Encoding.EncodeHackyBase64(b));
            }

            [Fact]
            public static void TestHex()
            {
                Assert.Equal(0xF0, RomByteEncoding.ByteParseHex2('F', '0'));
            }

            [Fact]
            public static void TestHex4()
            {
                Assert.Equal(0xF029, RomByteEncoding.ByteParseHex4('F', '0', '2', '9'));
            }
        }
    }
}
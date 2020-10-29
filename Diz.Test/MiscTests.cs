using System.Collections.Generic;
using System.Linq;
using Diz.Core.serialization.xml_serializer;
using Diz.Core.util;
using Xunit;

namespace Diz.Test
{
    public static class MiscTests
    {
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
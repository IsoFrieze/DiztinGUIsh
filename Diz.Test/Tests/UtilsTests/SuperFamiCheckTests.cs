using System.Collections.Generic;
using Diz.Test.Utils.SuperFamiCheckUtil;
using FluentAssertions;
using Sprache;
using Xunit;

namespace Diz.Test.Tests.UtilsTests
{
    public class SuperFamiCheckTests
    {
        public static IEnumerable<object[]> TestHexData => new List<object[]>
        {
            new object[] { "0x134572AB", 0x134572AB },
            new object[] { "0xFFFF", 0xFFFF },
            new object[] { "FFFF", 0xFFFF },
            new object[] { "0x01ABF", 0x01ABF },
            new object[] { "AABBCCDD", 0xAABBCCDD },
        };
        
        public static IEnumerable<object[]> InvalidHexData => new List<object[]>
        {
            new object[] { "0x000000000000" },
            new object[] { "0x000000000001" },
            new object[] { "asdf" },
            new object[] { "0x9999-" },
            new object[] { "x34572AB" },
            new object[] { "99999999FG" },
            new object[] { "AABBCCDDE"},
        };


        [Theory]
        [MemberData(nameof(InvalidHexData))]
        public static void TestHexParseInvalid(string invalidHexData)
        {
            DizSuperFamiCheckParse.HexNumber.End().Invoking(x => x.Parse(invalidHexData))
                .Should().Throw<ParseException>();
        }
        
        [Theory]
        [MemberData(nameof(TestHexData))]
        public static void TestHexParse(string hexInput, uint expectedOutput)
        {
            var num = DizSuperFamiCheckParse.HexNumber.End().Parse(hexInput);
            num.Should().Be(expectedOutput);
        }

        [Fact]
        public static void TestOneLine()
        {
            const string input = "         Checksum    0x788c            ";

            DizSuperFamiCheckParse.ParseKvpLine(input)
                .Should()
                .Be(("Checksum", 0x788c));
        }
    }
}
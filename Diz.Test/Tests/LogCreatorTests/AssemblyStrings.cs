using System.Collections.Generic;
using Diz.LogWriter;
using FluentAssertions;
using Xunit;

namespace Diz.Test;

public class AssemblyStrings
{
    public static IEnumerable<object[]> TestHexData => new List<object[]>
    {
        new object[] { "test text", "db \"test text\"" },
        new object[] { "test text\0", "db \"test text\", $00" },
        new object[] { "\0 test text\0123\0hi", "db $00, \" test text\", $00, \"123\", $00, \"hi\"" }
    };
    
    [Theory]
    [MemberData(nameof(TestHexData))]
    public static void TestHexParse(string inputStr, string outputLine)
    {
        LogCreatorExtensions.CreateAssemblyFormattedTextLine(inputStr).Should().Be(outputLine);
    }
}
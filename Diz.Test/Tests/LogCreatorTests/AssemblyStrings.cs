using System.Collections.Generic;
using Diz.LogWriter;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.LogCreatorTests;

public class AssemblyStrings
{
    public static IEnumerable<object[]> TestHexData => new List<object[]>
    {
        new object[] { "test text", "db \"test text\"" },
        new object[] { "test text\0", "db \"test text\", $00" },
        new object[] { "\0\0 test text\0123\0hi", "db $00, $00, \" test text\", $00, \"123\", $00, \"hi\"" },
        
        new object[]
        {
            "\0test1\ntest\0\r\n\0", 
            @"db $00, ""test1"", $0A, ""test"", $00, $0D, $0A, $00"
        },
    };
    
    [Theory]
    [MemberData(nameof(TestHexData))]
    public static void TestHexParse(string inputStr, string outputLine)
    {
        LogCreatorExtensions.CreateAssemblyFormattedTextLine(inputStr).Should().Be(outputLine);
    }
}
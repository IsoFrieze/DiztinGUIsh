using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Diz.Core.export;
using Diz.Core.export.assemblyGenerators;
using Diz.Core.util;
using Diz.Test.Utils;
using Moq;
using Xunit;

public static class AsmGeneratorTests
{
    public static TheoryData<(int, string)> SampleEmpties =>
        new List<int> {0, 22, -22, -1, 1, 100}
            .Select(i => new Func<(int, string)>(
                    () => (i, new string(' ', Math.Abs(i)))
                )
            )
            .CreateTheoryData();

    [Theory]
    [MemberData(nameof(SampleEmpties))]
    public static void TestEmptyGenerator((int, string) expected)
    {
        var generator = new AssemblyGenerateEmpty();
        var eLen = expected.Item1;
        var eString = expected.Item2;
        var gen = new Func<string>(() => generator.Emit(null, eLen));

        if (eLen != 0)
            Assert.Equal(eString, gen());
        else
            Assert.Throws<InvalidDataException>(gen);
    }

    [Fact]
    public static void TestLeftAlign()
    {
        Assert.Equal("xyz  ", Util.LeftAlign(5, "xyz"));
        Assert.Equal("xyz", Util.LeftAlign(3, "xyz"));
        Assert.Equal("{0,-22}",Util.GetLeftAlignFormatStr(22));
        Assert.Equal("{0,22}",Util.GetLeftAlignFormatStr(-22));
    }

    [Fact]
    public static void TestHex6()
    {
        Assert.Equal("000000", Util.ToHexString6(0));
        Assert.Equal("000002", Util.ToHexString6(2));
        Assert.Equal("FFFFFF", Util.ToHexString6(0xFFFFFF));
    }

    [Fact]
    public static void TestGenerator()
    {
        // var mock = new Mock<IAssemblyPartialGenerator>();
        // mock.Setup(foo => 
        //     foo.Emit(It.IsAny<int?>(), It.IsAny<int?>())).
        //
        // var gen = mock.Object;
        // Assert.Equal("qrx", gen.Emit(3,78));
        // Assert.Equal("qrx", gen.Emit(3,null));
        // Assert.Equal("qrx", gen.Emit(null,null));
        // Assert.Equal("qrx", gen.Emit(null,45));
    }
    
    public static TheoryData<(int, string)> SamplePC =>
        new List<int> {0, 22, -22, -1, 1, 100}
            .Select(i => new Func<(int, string)>(
                    () => (i, new string(' ', Math.Abs(i)))
                )
            )
            .CreateTheoryData();
    
    [Theory]
    [MemberData(nameof(SamplePC))]
    public static void TestPCGenerator((int, string) expected)
    {
        var generator = new AssemblyGenerateProgramCounter();
        
        var eLen = expected.Item1;
        var eString = expected.Item2;
        
        // var gen = new Func<string>(() => generator.Emit(null, eLen));

        Assert.Equal(eString, generator.Emit(27, null));
    }
}
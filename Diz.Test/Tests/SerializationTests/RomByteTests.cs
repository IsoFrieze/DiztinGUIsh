using Diz.Core;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.Cpu._65816;
using Diz.Test.Utils;
using Xunit;

namespace Diz.Test;

public class RomByteTests : ContainerFixture
{
    private static RomByte SampleRomByte1()
    {
        return new RomByte
        {
            Arch = Architecture.Apuspc700,
            DataBank = 90,
            DirectPage = 3,
            MFlag = true,
            XFlag = false,
            TypeFlag = FlagType.Graphics,
            Point = InOutPoint.InPoint | InOutPoint.ReadPoint,
            Rom = 0x78,
        };
    }

    private static RomByte SampleRomByte2()
    {
        // same as above, but just change .Rom
        var rb = SampleRomByte1();
        rb.Rom = 0x99;
        return rb;
    }

    private readonly ISampleDataFactory createSampleData = null!;
        
    [Fact]
    public void TestWhenNoIaPresent()
    {
        var sampleData = createSampleData.Create();
        const int offset = 0x1C1F;
        var result = sampleData.GetSnesApi()?.GetIntermediateAddressOrPointer(offset);
        Assert.Equal(result, -1);
    }
        
    [Fact]
    public void TestGetAddressMode()
    {
        var sampleData = createSampleData.Create();
        const int romOffset1 = 0xEB;
        var snesApi = sampleData.GetSnesApi();
        
        var mode1 = Cpu65C816<ISnesData>.GetAddressMode(snesApi, romOffset1);
        Assert.Equal(Cpu65C816Constants.AddressMode.Constant8, mode1);

        Assert.True(romOffset1 >= sampleData.GetTag<SampleDataGenerationTag>()!.OriginalRomSizeBeforePadding);

        var mode2 = Cpu65C816<ISnesData>.GetAddressMode(snesApi, 0x0A);
        Assert.Equal(Cpu65C816Constants.AddressMode.Constant8, mode2);
    }

    [Fact]
    public void TestEqualsButNotCompareByte()
    {
        var rb1 = SampleRomByte1();
        var rb2 = SampleRomByte2();

        Assert.True(rb1.EqualsButNoRomByte(rb2));
        Assert.False(rb1.Equals(rb2));

        rb1.Point = InOutPoint.EndPoint;
        Assert.False(rb1.EqualsButNoRomByte(rb2));
        Assert.False(rb1.Equals(rb2));
    }

    [Fact]
    public void TestEquals()
    {
        var rb1 = SampleRomByte1();
        var rb2 = SampleRomByte1();

        Assert.True(rb1.Equals(rb2));
        Assert.True(rb1.EqualsButNoRomByte(rb2));
    }
}
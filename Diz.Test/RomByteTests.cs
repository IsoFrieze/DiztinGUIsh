using Diz.Core;
using Diz.Core.arch;
using Diz.Core.model;
using Xunit;

namespace Diz.Test
{
    public sealed class RomByteTests
    {
        private static RomByte SampleRomByte1()
        {
            return new RomByte() {
                Arch = Data.Architecture.Apuspc700,
                DataBank = 90,
                DirectPage = 3,
                MFlag = true,
                XFlag = false,
                TypeFlag = Data.FlagType.Graphics,
                Point = Data.InOutPoint.InPoint | Data.InOutPoint.ReadPoint,
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
        
        [Fact]
        public static void TestWhenNoIAPresent()
        {
            var sampleData = SampleRomData.SampleData;
            const int offset = 0x1C1F;
            var result = sampleData.GetIntermediateAddressOrPointer(offset);
            Assert.Equal(result, -1);
        }
        
        [Fact]
        public static void TestGetAddressMode()
        {
            var sampleData = SampleRomData.SampleData;
            const int romOffset1 = 0xEB;
            var mode1 = Cpu65C816.GetAddressMode(sampleData, romOffset1);
            Assert.Equal(Cpu65C816.AddressMode.Constant8, mode1);

            Assert.True(romOffset1 >= sampleData.OriginalRomSizeBeforePadding);

            var mode2 = Cpu65C816.GetAddressMode(sampleData, 0x0A);
            Assert.Equal(Cpu65C816.AddressMode.Constant8, mode2);
        }

        [Fact]
        public void TestEqualsButNotCompareByte()
        {
            var rb1 = SampleRomByte1();
            var rb2 = SampleRomByte2();

            Assert.True(rb1.EqualsButNoRomByte(rb2));
            Assert.False(rb1.Equals(rb2));

            rb1.Point = Data.InOutPoint.EndPoint;
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
}

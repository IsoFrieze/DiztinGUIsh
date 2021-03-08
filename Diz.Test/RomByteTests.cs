using Diz.Core.model;
using Xunit;

namespace Diz.Test
{
    public sealed class RomByteTests
    {
        private static RomByte SampleRomByte1()
        {
            return new RomByte() {
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
}

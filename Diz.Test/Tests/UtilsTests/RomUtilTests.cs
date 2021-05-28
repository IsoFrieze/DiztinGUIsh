using Diz.Core.util;
using Xunit;

namespace Diz.Test.Tests.UtilsTests
{
    public static class RomUtilTests
    {
        [Fact]
        public static void ConvertSnesToPcHiRom()
        {
            var romSize = RomUtil.GetBankSize(RomMapMode.HiRom) * 64;
            Assert.Equal(-1, RomUtil.ConvertSnesToPc(0x202000, RomMapMode.HiRom, romSize));
            Assert.Equal(0x01FFFF, RomUtil.ConvertSnesToPc(0x41FFFF, RomMapMode.HiRom, romSize));
            Assert.Equal(0x000123, RomUtil.ConvertSnesToPc(0xC00123, RomMapMode.HiRom, romSize));
            Assert.Equal(0x3F0123, RomUtil.ConvertSnesToPc(0xFF0123, RomMapMode.HiRom, romSize));
            Assert.Equal(-1, RomUtil.ConvertSnesToPc(0x10000000, RomMapMode.HiRom, romSize));
        }

        [Fact]
        public static void ConvertSnesToPcLoRom()
        {
            var romSize = RomUtil.GetBankSize(RomMapMode.LoRom) * 8;
            Assert.Equal(-1, RomUtil.ConvertSnesToPc(0x790000, RomMapMode.LoRom, romSize));
            Assert.Equal(0x00, RomUtil.ConvertSnesToPc(0x808000, RomMapMode.LoRom, romSize));
        }
    }
}
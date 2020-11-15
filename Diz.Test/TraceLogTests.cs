using Diz.Core.import;
using Xunit;

namespace Diz.Test
{
    public static class TraceLogTests
    {
        [Fact]
        public static void TestCacheLines()
        {
            var cache = new BsnesTraceLogImporter.CachedTraceLineTextIndex();

            // example line:
            // @"028cde rep #$30               A:0004 X:0000 Y:0004 S:1fdd D:0000 DB:02 nvmxdiZC V:133 H: 654 F:36";
            
            // make sure the indices are correct
            Assert.Equal(0, cache.Addr);
            Assert.Equal(60, cache.D);
            Assert.Equal(68, cache.Db);
            Assert.Equal(71, cache.FN);
            Assert.Equal(72, cache.FV);
            Assert.Equal(73, cache.FM);
            Assert.Equal(74, cache.FX);
            Assert.Equal(75, cache.FD);
            Assert.Equal(76, cache.FI);
            Assert.Equal(77, cache.FZ);
            Assert.Equal(78, cache.FC);
            Assert.Equal(cache.Flags, cache.FN);
        }
        
        [Fact]
        public static void TestParseText()
        {
            var sampleLine = BsnesTraceLogImporter.SampleLineText;

            var modData = new BsnesTraceLogImporter.ModificationData();
            BsnesTraceLogImporter.ParseTextLine(sampleLine, modData);
            
            Assert.Equal(0x28cde, modData.SnesAddress);
            Assert.Equal(0x0000, modData.DirectPage);
            Assert.Equal(0x02, modData.DataBank);
            Assert.False(modData.XFlagSet);
            Assert.False(modData.MFlagSet);
        }
    }
}


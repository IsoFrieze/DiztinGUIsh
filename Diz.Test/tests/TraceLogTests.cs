using Diz.Core.import;
using Xunit;

namespace Diz.Test.tests
{
    public static class TraceLogTests
    {
        const string example1 = @"028cde rep #$30               A:0004 X:0000 Y:0004 S:1fdd D:0000 DB:02 nvMxdiZC V:133 H: 654 F:36";
        const string example2 = @"c3091a mvn $00,$00            A:001c X:0923 Y:0953 S:06ef D:F001 DB:AF nvmXdIzC V:241 H:  9 F:19";
        const string example3 = @"90c860 sta $420d      [00420d] A:0001 X:0000 Y:0000 S:01ff D:2134 DB:9A ..1B.I.. V:  0 H: 62 F: 0";

        [Fact]
        public static void TestCacheLines1()
        {
            var cache = new BsnesTraceLogImporter.CachedTraceLineTextIndex();
            cache.RecomputeCachedIndicesBasedOn(example1);

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
            
            Assert.Equal(cache.LastLineLength, example1.Length);
        }
        
        [Fact]
        public static void TestCacheLines2()
        {
            var cache = new BsnesTraceLogImporter.CachedTraceLineTextIndex();
            cache.RecomputeCachedIndicesBasedOn(example2);

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
            
            Assert.Equal(cache.LastLineLength, example2.Length);
            
            cache.RecomputeCachedIndicesBasedOn(example3);
            Assert.Equal(cache.LastLineLength, example3.Length);
        }
        
        
        [Fact]
        public static void TestCacheLines3()
        {
            var cache = new BsnesTraceLogImporter.CachedTraceLineTextIndex();
            cache.RecomputeCachedIndicesBasedOn(example3);

            // make sure the indices are correct
            Assert.Equal(0, cache.Addr);
            Assert.Equal(61, cache.D);
            Assert.Equal(69, cache.Db);
            Assert.Equal(72, cache.FN);
            Assert.Equal(73, cache.FV);
            Assert.Equal(74, cache.FM);
            Assert.Equal(75, cache.FX);
            Assert.Equal(76, cache.FD);
            Assert.Equal(77, cache.FI);
            Assert.Equal(78, cache.FZ);
            Assert.Equal(79, cache.FC);
            Assert.Equal(cache.Flags, cache.FN);
        }
        
        [Fact]
        public static void TestParseText1()
        {
            var modData = new BsnesTraceLogImporter.ModificationData();
            var importer = new BsnesTraceLogImporter(null);
            
            importer.ParseTextLine(example1, modData);
            
            Assert.Equal(0x28cde, modData.SnesAddress);
            Assert.Equal(0x0000, modData.DirectPage);
            Assert.Equal(0x02, modData.DataBank);
            Assert.True(modData.MFlagSet);
            Assert.False(modData.XFlagSet);
        }
        
        [Fact]
        public static void TestParseText2()
        {
            var modData = new BsnesTraceLogImporter.ModificationData();
            var importer = new BsnesTraceLogImporter(null);
            
            importer.ParseTextLine(example2, modData);
            Assert.Equal(0xc3091a, modData.SnesAddress);
            Assert.Equal(0xF001, modData.DirectPage);
            Assert.Equal(0xAF, modData.DataBank);
            Assert.False(modData.MFlagSet);
            Assert.True(modData.XFlagSet);
        }
        
        [Fact]
        public static void TestParseText3()
        {
            var modData = new BsnesTraceLogImporter.ModificationData();
            var importer = new BsnesTraceLogImporter(null);
            
            importer.ParseTextLine(example3, modData);

            Assert.Equal(0x90c860, modData.SnesAddress);
            Assert.Equal(0x2134, modData.DirectPage);
            Assert.Equal(0x9A, modData.DataBank);
            Assert.True(modData.MFlagSet);
            Assert.True(modData.XFlagSet);
        }
    }
}


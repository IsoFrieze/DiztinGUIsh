using Diz.Core.model.byteSources;
using Microsoft.Diagnostics.Tracing.Parsers;
using Xunit;

namespace Diz.Test.tests
{
    public static class ByteStorageTests
    {
        [Fact]
        public static void TestSparseStorage()
        {
            ByteStorage byteStorage = new SparseByteStorage(null, 10);
            byteStorage[1] = new ByteOffsetData {Byte = 0xE1};
            byteStorage[7] = new ByteOffsetData {Byte = 0xE7};

            var i = 0;
            foreach (var b in byteStorage)
            {
                if (i == 1 || i == 7)
                    Assert.True(b.Byte == 0xE0 + i);
                else
                    Assert.Null(b);

                ++i;
            }
            
            Assert.Equal(10, i);
        } 
    }
}
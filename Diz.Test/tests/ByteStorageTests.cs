using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Diz.Core.model.byteSources;
using Diz.Test.Utils;
using Xunit;

namespace Diz.Test.tests
{
    public static class ByteStorageTests
    {
        public static TheoryData<ByteStorage> SampleValidSparseStorage =>
            new List<Func<ByteStorage>> {
                () => 
                {
                    var byteStorage = new SparseByteStorage(null, 10);
                    byteStorage[1] = new ByteOffsetData {Byte = 0xE1};
                    byteStorage[7] = new ByteOffsetData {Byte = 0xE7};
                    return byteStorage;
                },
                () =>
                {
                    const int count = 10;
                    var byteStorage = new SparseByteStorage(null, count);
                    for (var i = 0; i < count; ++i)
                    {
                        Debug.Assert(byteStorage[i] == null);
                        byteStorage[i] = new ByteOffsetData {Byte = (byte?)(i+0xE0)};
                    }
                    return byteStorage;
                },
                () =>
                {
                    const int count = 10;
                    var srcList = Enumerable.Range(0, count)
                        .Select(i => new ByteOffsetData {Byte = (byte?)(i+0xE0)}).ToList();
                    return new SparseByteStorage(null, srcList);
                },
            }.CreateTheoryData();

        [Theory]
        [MemberData(nameof(SampleValidSparseStorage))]
        public static void TestSparseStorageAdd(ByteStorage byteStorage)
        {
            var hasGaps = ((SparseByteStorage) byteStorage)?.ActualCount != byteStorage.Count;
            var shouldBeNonNullOn = new List<int> {1,7};
         
            var i = 0;
            foreach (var b in byteStorage)
            {
                if (b != null)
                    Assert.True(b.Byte == 0xE0 + i);

                if (hasGaps)
                {
                    var expectedNonNull = shouldBeNonNullOn.Contains(i);
                    Assert.Equal(expectedNonNull, b != null);
                }
                
                ++i;
            }
            
            Assert.Equal(10, i);
        }

        [Theory]
        [MemberData(nameof(SampleValidSparseStorage))]
        public static void TestSparseStorageDict(ByteStorage byteStorage)
        {
            var byteStorageSparse = (SparseByteStorage)byteStorage;
            
            var hasGaps = byteStorageSparse?.ActualCount != byteStorage.Count;
            Assert.Equal(10, byteStorage.Count);

            List<int> expectedKeys;
            int expectedActualCount;
            if (hasGaps)
            {
                expectedActualCount = 2;
                expectedKeys = new List<int> {1, 7};
            }
            else
            {
                expectedActualCount = 10;
                expectedKeys = Enumerable.Range(0, 10).ToList();
            }
            Assert.Equal(expectedActualCount, byteStorageSparse?.ActualCount);
            
            Assert.NotNull(byteStorageSparse);
            var en = byteStorageSparse.GetSparseEnumerator();
            
            var i = 0;
            while (en.MoveNext())
            {
                var expectedKey = expectedKeys[i];
                Assert.Equal(expectedKey, en.Current.Key);
                Assert.Equal(expectedKey, en.Current.Value.ContainerOffset);
                i++;
            }
            
            // actual # of elements in sparse region
            Assert.Equal(expectedActualCount, i);
        }

        [Theory]
        [MemberData(nameof(SampleValidSparseStorage))]
        public static void TestSparseStorageRange(ByteStorage byteStorage)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => byteStorage[-1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => byteStorage[10]);
        }
    }
}
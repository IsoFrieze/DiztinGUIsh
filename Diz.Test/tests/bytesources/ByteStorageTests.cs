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
        public static TheoryData<ByteStorage> SampleStorages =>
            new List<Func<ByteStorage>> {
                () => 
                {
                    var byteStorage = new SparseByteStorage(10)
                    {
                        [1] = new() {Byte = 0xE1}, 
                        [7] = new() {Byte = 0xE7}
                    };
                    return byteStorage;
                },
                () =>
                {
                    const int count = 10;
                    var byteStorage = new SparseByteStorage(count);
                    for (var i = 0; i < count; ++i)
                    {
                        byteStorage[i] = new ByteEntry {Byte = (byte?)(i+0xE0)};
                    }
                    return byteStorage;
                },
                () =>
                {
                    const int count = 10;
                    var srcList = Enumerable.Range(0, count)
                        .Select(i => new ByteEntry {Byte = (byte?)(i+0xE0)}).ToList();
                    return new SparseByteStorage(srcList);
                },
                
                () =>
                {
                    const int count = 10;
                    var srcList = Enumerable.Range(0, count)
                        .Select(i => new ByteEntry {Byte = (byte?)(i+0xE0)}).ToList();
                    return new ByteList(srcList);
                },
            }.CreateTheoryData();

        [Theory]
        [MemberData(nameof(SampleStorages))]
        public static void TestSparseStorageAdd(ByteStorage byteStorage)
        {
            var hasGaps = byteStorage is SparseByteStorage sparse && sparse.ActualCount != byteStorage.Count;
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
        [MemberData(nameof(SampleStorages))]
        public static void TestSparseStorageDict(ByteStorage byteStorage)
        {
            var byteStorageSparse = byteStorage as SparseByteStorage;
            var hasGaps = byteStorageSparse != null && byteStorageSparse.ActualCount != byteStorage.Count;
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

            if (byteStorageSparse == null)
                return;
            
            Assert.NotNull(byteStorageSparse);
            Assert.Equal(expectedActualCount, byteStorageSparse.ActualCount);
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
        [MemberData(nameof(SampleStorages))]
        public static void TestSparseStorageRange(ByteStorage byteStorage)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => byteStorage[-1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => byteStorage[10]);
        }
    }
}
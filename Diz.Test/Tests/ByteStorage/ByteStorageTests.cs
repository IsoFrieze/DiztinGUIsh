using System;
using System.Collections.Generic;
using System.Linq;
using Diz.Core.model.byteSources;
using Diz.Test.Utils;
using Xunit;

namespace Diz.Test.Tests.ByteStorage
{
    public static class ByteStorageTests
    {
        public static TheoryData<Storage<Core.model.byteSources.ByteEntry>> SampleStorages =>
            new List<Func<Storage<Core.model.byteSources.ByteEntry>>> {
                () => 
                {
                    var byteStorage = new StorageSparse<Core.model.byteSources.ByteEntry>(10)
                    {
                        [1] = new() {Byte = 0xE1}, 
                        [7] = new() {Byte = 0xE7}
                    };
                    return byteStorage;
                },
                () =>
                {
                    const int count = 10;
                    var byteStorage = new StorageSparse<Core.model.byteSources.ByteEntry>(count);
                    for (var i = 0; i < count; ++i)
                    {
                        byteStorage[i] = new Core.model.byteSources.ByteEntry {Byte = (byte?)(i+0xE0)};
                    }
                    return byteStorage;
                },
                () =>
                {
                    const int count = 10;
                    var srcList = Enumerable.Range(0, count)
                        .Select(i => new Core.model.byteSources.ByteEntry {Byte = (byte?)(i+0xE0)}).ToList();
                    return new StorageSparse<Core.model.byteSources.ByteEntry>(srcList);
                },
                
                () =>
                {
                    const int count = 10;
                    var srcList = Enumerable.Range(0, count)
                        .Select(i => new Core.model.byteSources.ByteEntry {Byte = (byte?)(i+0xE0)}).ToList();
                    return new StorageList<Core.model.byteSources.ByteEntry>(srcList);
                },
            }.CreateTheoryData();

        [Theory]
        [MemberData(nameof(SampleStorages))]
        public static void TestSparseStorageAdd(Storage<Core.model.byteSources.ByteEntry> byteStorage)
        {
            var hasGaps = byteStorage is StorageSparse<Core.model.byteSources.ByteEntry> sparse && sparse.ActualCount != byteStorage.Count;
            var shouldBeNonNullOn = new List<int> {1,7};
         
            var i = 0;
            foreach (Core.model.byteSources.ByteEntry b in byteStorage)
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
        public static void TestSparseStorageDict(Storage<Core.model.byteSources.ByteEntry> byteStorage)
        {
            var byteStorageSparse = byteStorage as StorageSparse<Core.model.byteSources.ByteEntry>;
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
            var en = byteStorageSparse.GetRealEnumerator();
            
            var i = 0;
            while (en.MoveNext())
            {
                var expectedKey = expectedKeys[i];
                Assert.Equal(expectedKey, en.Current.Key);
                Assert.Equal(expectedKey, en.Current.Value.ParentIndex);
                i++;
            }
            
            // actual # of elements in sparse region
            Assert.Equal(expectedActualCount, i);
        }

        [Theory]
        [MemberData(nameof(SampleStorages))]
        public static void TestSparseStorageRange(Storage<Core.model.byteSources.ByteEntry> byteStorage)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => byteStorage[-1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => byteStorage[10]);
        }
    }
}
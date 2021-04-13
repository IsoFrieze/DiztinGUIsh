using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Diz.Core.model.byteSources;
using Diz.Test.Utils;
using Xunit;

namespace Diz.Test.tests
{
    public class ByteSourceTests
    {
        /*public static TheoryData<ByteSource> SampleValidByteSources =>
            new List<Func<ByteSource>> {
                () => new ByteSource {
                    ByteStorageType = typeof(SparseByteStorage)
                },
                () => new ByteSource {
                    ByteStorageType = typeof(ByteList)
                },
            }
                // .Aggregate(new List<Func<ByteSource>>, (newfns, fn) => fn().)
                .CreateTheoryData();*/
        
        /*[Theory]
        [MemberData(nameof(SampleValidByteSources))]
        public static void TestSparseStorageAdd(ByteSource byteStorage)
        {
        }*/
    }
}
using System;
using System.Collections.Generic;
using Diz.Core.model;
using Diz.Test.Utils;
using Diz.Core.model.byteSources;
using Xunit;

namespace Diz.Test.Tests.AnnotationCollectionTests
{
    public class AnnotationCollectionEqualityTests
    {
        public static TheoryData<(AnnotationCollection, AnnotationCollection, bool)> AnnotationEqualityTestData
        {
            get
            {
                static AnnotationCollection OneComment() => new() {new Comment()};
                static AnnotationCollection ZeroCount() => new();

                return new List<Func<(AnnotationCollection, AnnotationCollection, bool)>>
                {
                    // comparisonItem1, comparisonItem2, shouldBeEqual
                    () => (null, null, true),
                    () => (ZeroCount(), null, true),
                    () => (null, ZeroCount(), true),
                    () => (ZeroCount(), ZeroCount(), true),

                    () => (OneComment(), ZeroCount(), false),
                    () => (OneComment(), null, false),
                    () => (ZeroCount(), OneComment(), false),
                    () => (null, OneComment(), false),
                }.CreateTheoryData();
            }
        }

        [Theory]
        [MemberData(nameof(AnnotationEqualityTestData))]
        public void TestAnnotationCollectionEquality((AnnotationCollection, AnnotationCollection, bool) harness)
        {
            var (collection1, collection2, expected) = harness;
            AssertEqualityBothWays(expected, collection1, collection2);
        }

        private static void AssertEqualityBothWays(bool expectedEqual, AnnotationCollection o1, AnnotationCollection o2)
        {
            // method 1
            {
                var byteEntry1 = new ByteEntry(o1);
                var byteEntry2 = new ByteEntry(o2);
                AssertEqualityBothWays(expectedEqual, o1, o2, byteEntry1, byteEntry2);
            }

            // method 2
            {
                var byteEntry3 = new ByteEntry {Annotations = o1};
                var byteEntry4 = new ByteEntry {Annotations = o2};
                AssertEqualityBothWays(expectedEqual, o1, o2, byteEntry3, byteEntry4);
            }
        }

        private static void AssertEqualityBothWays(bool expectedEqual, AnnotationCollection o1, AnnotationCollection o2,
            ByteEntry byteEntry1, ByteEntry byteEntry2)
        {
            // note: AnnotationCollection.Equals() does NOT call EffectivelyEqual() by default
            if (expectedEqual)
            {
                Assert.True(AnnotationCollection.EffectivelyEqual(o1, o2));
                Assert.Equal(byteEntry1, byteEntry2);
                Assert.Equal(byteEntry2, byteEntry1);
            }
            else
            {
                Assert.False(AnnotationCollection.EffectivelyEqual(o1, o2));
                Assert.NotEqual(byteEntry1, byteEntry2);
                Assert.NotEqual(byteEntry2, byteEntry1);
            }
        }
    }
}
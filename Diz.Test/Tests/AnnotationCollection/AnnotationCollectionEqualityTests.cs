using System;
using System.Collections.Generic;
using Diz.Core.model;
using Diz.Test.Utils;
using Xunit;

namespace Diz.Test.Tests.AnnotationCollection
{
    public class AnnotationCollectionEqualityTests
    {
        public static TheoryData<(Core.model.byteSources.AnnotationCollection, Core.model.byteSources.AnnotationCollection, bool)> AnnotationEqualityTestData
        {
            get
            {
                static Core.model.byteSources.AnnotationCollection OneComment() => new() {new Comment()};
                static Core.model.byteSources.AnnotationCollection ZeroCount() => new();

                return new List<Func<(Core.model.byteSources.AnnotationCollection, Core.model.byteSources.AnnotationCollection, bool)>>
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
        public void TestAnnotationCollectionEquality((Core.model.byteSources.AnnotationCollection, Core.model.byteSources.AnnotationCollection, bool) harness)
        {
            var (collection1, collection2, expected) = harness;
            AssertEqualityBothWays(expected, collection1, collection2);
        }

        private static void AssertEqualityBothWays(bool expectedEqual, Core.model.byteSources.AnnotationCollection o1, Core.model.byteSources.AnnotationCollection o2)
        {
            // method 1
            {
                var byteEntry1 = new Core.model.byteSources.ByteEntry(o1);
                var byteEntry2 = new Core.model.byteSources.ByteEntry(o2);
                AssertEqualityBothWays(expectedEqual, o1, o2, byteEntry1, byteEntry2);
            }

            // method 2
            {
                var byteEntry3 = new Core.model.byteSources.ByteEntry {Annotations = o1};
                var byteEntry4 = new Core.model.byteSources.ByteEntry {Annotations = o2};
                AssertEqualityBothWays(expectedEqual, o1, o2, byteEntry3, byteEntry4);
            }
        }

        private static void AssertEqualityBothWays(bool expectedEqual, Core.model.byteSources.AnnotationCollection o1, Core.model.byteSources.AnnotationCollection o2,
            Core.model.byteSources.ByteEntry byteEntry1, Core.model.byteSources.ByteEntry byteEntry2)
        {
            // note: AnnotationCollection.Equals() does NOT call EffectivelyEqual() by default
            if (expectedEqual)
            {
                Assert.True(Core.model.byteSources.AnnotationCollection.EffectivelyEqual(o1, o2));
                Assert.Equal(byteEntry1, byteEntry2);
                Assert.Equal(byteEntry2, byteEntry1);
            }
            else
            {
                Assert.False(Core.model.byteSources.AnnotationCollection.EffectivelyEqual(o1, o2));
                Assert.NotEqual(byteEntry1, byteEntry2);
                Assert.NotEqual(byteEntry2, byteEntry1);
            }
        }
    }
}
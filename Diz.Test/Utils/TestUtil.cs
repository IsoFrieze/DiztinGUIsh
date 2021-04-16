using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Diz.Test.Utils
{
    public static class TheoryDataGenerator
    {
        public static TheoryData<T> CreateTheoryData<T>(this IEnumerable<Func<T>> data)
        {
            return data
                .Select(fn => fn())
                .Aggregate(new TheoryData<T>(), (theoryData, item) =>
                {
                    theoryData.Add(item);
                    return theoryData;
                });
        }
    }
    
    public static class TestUtil
    {
        public static void AssertCollectionEqual<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual)
        {
            // do some weirdness here to better display the differences in the output window.
            
            var largestListCount = Math.Max(expected.Count, actual.Count);
            for (var i = 0; i < largestListCount; ++i)
            {
                // if this gets hit, lengths of lists are different
                Assert.True(i < actual.Count);
                Assert.True(i < expected.Count);

                var expectedItem = expected[i];
                var actualItem = actual[i];
                
                Assert.Equal(expectedItem, actualItem);
            }
            
            Assert.Equal(expected.Count, actual.Count);

            Assert.True(expected.SequenceEqual(actual));
        }
    }
}
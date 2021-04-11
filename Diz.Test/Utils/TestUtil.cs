using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Diz.Test.Utils
{
    public static class TestUtil
    {
        public static void AssertCollectionEqual<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual)
        {
            Assert.Equal(expected.Count, actual.Count);

            for (var i = 0; i < expected.Count; ++i) {
                Assert.Equal(expected[i], actual[i]);
            }

            Assert.True(expected.SequenceEqual(actual));
        }
    }
}
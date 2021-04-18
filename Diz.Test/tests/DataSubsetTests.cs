/*using System.Collections.Generic;
using System.Linq;
using Diz.Core.serialization.xml_serializer;
using Xunit;

namespace Diz.Test
{
    public static class DataSubsetTests
    {
        private static IEnumerable<TOut> Repeat<TOut>(TOut toRepeat, int times)
        {
            for (var i = 0; i < times; ++i)
            {
                yield return toRepeat;
            }
        }

        private static (IEnumerable<string>, IEnumerable<string>) GenerateRepeat(string toRepeat, int times)
        {
            var repeated = Repeat($"{toRepeat}", times);
            return (repeated.ToList(),
                    times >= 8
                        ? new List<string>(new[] {$"r {times} {toRepeat}"})
                        : repeated.ToList()
                );
        }

        /*public static TheoryData<IEnumerable<string>, IEnumerable<string>> ValidCompressionData
        {
            get
            {
                var tmp = new TheoryData<IEnumerable<string>, IEnumerable<string>>();
                ValidDataReal.ForEach(i => tmp.Add(i.Item1, i.Item2));
                return tmp;
                
                // ValidDataReal.Select(x => )
            }
        }#1#
        
        public static TheoryData<IEnumerable<string>, IEnumerable<string>> ValidTest =>
            new TheoryData<IEnumerable<string>, IEnumerable<string>>
            {
                { -4, -6, -10 },
                { -2, 2, 0 },
                { int.MinValue, -1, int.MaxValue }
            };

        /*
        private static List<(IEnumerable<string>, IEnumerable<string>)> ValidDataReal =>
            new List<(IEnumerable<string>, IEnumerable<string>)>
            {
                GenerateRepeat("TestItem", 20),
                
                GenerateRepeat("TestItem", 30),
                
                (
                    Repeat("YO", 20).Concat(new[]
                    {
                        "different @ end"
                    }),
                    new List<string>(new[]
                    {
                        "r 20 YO",
                        "different @ end"
                    })
                ),
                
                (
                    new List<string>()
                        {
                            "start",
                            "start2",
                        }
                        .Concat(
                            Repeat("YO1", 22).Concat(
                                Repeat("YO2", 20).Concat(
                                    new[]
                                    {
                                        "different @ end"
                                    }))),
                    new List<string>(new[]
                    {
                        "start",
                        "start2",
                        "r 22 YO1",
                        "r 20 YO2",
                        "different @ end"
                    })
                )
            };#1#

        [Theory]
        [MemberData(nameof(ValidTest))]
        public static void TestWindowSizings(int startingLargeIndex, int rowCount, int selectedLargeOffset, int expectedAtValue)
        {
            var dataSubset = new DataSubsetWithSelection();
            
            Assert.Equal(expected, inputListCopy);
            Assert.Equal(inputListCopy, input);
        }
    }
}*/
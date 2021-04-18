using System.Collections.Generic;
using System.Linq;
using Diz.Core.serialization.xml_serializer;
using Xunit;

namespace Diz.Test.tests
{
    public static class CompressionTest
    {
        private static IEnumerable<TOut> Repeat<TOut>(TOut toRepeat, int times) {
            for (var i = 0; i < times; ++i) {
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

        public static TheoryData<IEnumerable<string>, IEnumerable<string>> ValidCompressionData
        {
            get
            {
                // TODO: probably a way simpler way to do this. works ok.
                var p = ValidDataReal();
                var xx = new TheoryData<IEnumerable<string>, IEnumerable<string>>();
                p.ForEach(i => xx.Add(i.Item1, i.Item2));
                return xx;
            }
        }

        private static List<(IEnumerable<string>, IEnumerable<string>)> ValidDataReal() =>
            new()
            {
                GenerateRepeat("TestItem", 20),
                GenerateRepeat("TestItem", 30),
                (
                    Repeat("YO", 20).Concat(new[] {
                        "different @ end"
                    }),
                    new List<string>(new[] {
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
                                new[] {
                                "different @ end"
                            }))),
                    new List<string>(new[] {
                        "start",
                        "start2",
                        "r 22 YO1",
                        "r 20 YO2",
                        "different @ end"
                    })
                )
            };

        [Theory]
        [MemberData(nameof(ValidCompressionData))]
        public static void TestCompressionsValid(IEnumerable<string> input, IEnumerable<string> expected)
        {
            var inputListCopy = new List<string>(input);
            RepeaterCompression.Compress(ref inputListCopy);
            Assert.Equal(expected, inputListCopy);
            
            RepeaterCompression.Decompress(ref inputListCopy);
            Assert.Equal(inputListCopy, input);
        }
    }
}
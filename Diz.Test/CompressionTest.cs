using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Diz.Core.serialization.xml_serializer;
using Diz.Core.util;
using IX.System.Collections.Generic;
using Xunit;

namespace Diz.Test
{
    public static class CompressionTest
    {
        public static IEnumerable<TOut> Repeat<TOut>(TOut toRepeat, int times)
        {
            for (var i = 0; i < times; ++i)
            {
                yield return toRepeat;
            }
        }

        public static TheoryData<IEnumerable<string>, IEnumerable<string>> ValidCompressionData =>
            new TheoryData<IEnumerable<string>, IEnumerable<string>>
            {
                {
                    Repeat("YO", 3),
                    Repeat("YO", 3)
                },
                {
                    Repeat("YO", 20),
                    new[] {"r YO"}
                }
            };

        [Theory]
        [MemberData(nameof(ValidCompressionData))]
        public static void TestCompressionsValid(IEnumerable<string> input, IEnumerable<string> output)
        {
            //var serializer = new RomBytesSerializer();
            //RomBytesXMLSerializer.ApplyCompression_GroupsBlocks()
            Assert.Equal(input.ToList(), output.ToList());
        }
    }
}
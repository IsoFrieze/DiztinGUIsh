using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Test.Utils;
using Xunit;

namespace Diz.Test.Tests.Serialization
{
    public class XmlByteStorageTests
    {
        private static Core.model.byteSources.ByteEntry CreateSampleEntryX()
        {
            return new()
            {
                Byte = 0xCA, MFlag = true, DataBank = 0x80, DirectPage = 0x2100,
                Annotations = {new Label {Name = "SomeLabel"}, new Comment {Text = "This is a comment"}}
            };
        }
        
        [Fact]
        public void XmlFullCycleByteStorageSparse()
        {
            var e1 = CreateSampleEntryX();
            e1.Point = InOutPoint.InPoint;
            e1.DirectPage = 3;

            XmlTestUtils.RunFullCycle(() =>
            {
                var sampleEmptyByteSparse = new StorageSparse<Core.model.byteSources.ByteEntry>(10) {[1] = CreateSampleEntryX(), [8] = e1};
                return sampleEmptyByteSparse;
            }, out var unchanged, out var cycled);
            
            Assert.Equal(unchanged, cycled);
        }
    }
}
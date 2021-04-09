using System.Linq;
using System.Xml;
using Diz.Core.model;
using Diz.Core.serialization.xml_serializer;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using IX.Observable;
using Xunit;
using Xunit.Abstractions;

namespace Diz.Test
{
    public class SerializerDictionaryTest
    {
        private readonly ITestOutputHelper testOutputHelper;

        public SerializerDictionaryTest(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        public class TestRoot
        {
            public ObservableDictionary<int, Comment> ODW { get; } = new() {
                {1, new Comment{Text="Z test1"}},
                {2, new Comment{Text="Z test3"}},
            };
            public ObservableDictionary<int, Label> ODW2 { get; } = new() {
                {100, new Label {Comment = "c1", Name = "location1"}},
                {200, new Label {Comment = "c2", Name = "location2"}},
            };

            #region Equality
            protected bool Equals(TestRoot other)
            {
                return
                    ODW.SequenceEqual(other.ODW) &&
                    ODW2.SequenceEqual(other.ODW2);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((TestRoot)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((ODW != null ? ODW.GetHashCode() : 0) * 397) ^ (ODW2 != null ? ODW2.GetHashCode() : 0);
                }
            }
            #endregion
        }

        private static IConfigurationContainer GetSerializer()
        {
            return XmlSerializerSupport.GetSerializer()
                .EnableImplicitTyping(typeof(TestRoot));
        }

        [Fact]
        private void Serializer()
        {
            var serializer = GetSerializer().Create();

            var xmlStr = serializer.Serialize(
                new XmlWriterSettings(),
                testRootElementGood);

            testOutputHelper.WriteLine(xmlStr);

            Assert.Equal(xmlShouldBe, xmlStr);
        }

        [Fact]
        private void DeSerialize()
        {
            var serializer = GetSerializer().Create();
            var restoredRoot = serializer.Deserialize<TestRoot>(xmlShouldBe);

            Assert.Equal(testRootElementGood, restoredRoot);
        }

        private readonly TestRoot testRootElementGood = new();

        string xmlShouldBe = "<?xml version=\"1.0\" encoding=\"utf-8\"?><SerializerDictionaryTest-TestRoot xmlns:ns1=\"clr-namespace:IX.Observable;assembly=IX.Observable\" xmlns:sys=\"https://extendedxmlserializer.github.io/system\" xmlns:exs=\"https://extendedxmlserializer.github.io/v2\" xmlns:ns2=\"clr-namespace:Diz.Core.model;assembly=Diz.Core\"><ODW AutomaticallyCaptureSubItems=\"false\" HistoryLevels=\"50\"><sys:Item Key=\"1\"><Value Text=\"Z test1\" /></sys:Item><sys:Item Key=\"2\"><Value Text=\"Z test3\" /></sys:Item></ODW><ODW2 AutomaticallyCaptureSubItems=\"false\" HistoryLevels=\"50\"><sys:Item Key=\"100\"><Value Name=\"location1\" Comment=\"c1\" /></sys:Item><sys:Item Key=\"200\"><Value Name=\"location2\" Comment=\"c2\" /></sys:Item></ODW2></SerializerDictionaryTest-TestRoot>";
    }
}
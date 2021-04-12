using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Diz.Core.model;
using Diz.Core.serialization.xml_serializer;
using Diz.Test.Utils;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
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
            public Dictionary<int, Comment> Odw { get; } = new() {
                {1, new Comment{Text="Z test1"}},
                {2, new Comment{Text="Z test3"}},
            };
            public Dictionary<int, Label> Odw2 { get; } = new() {
                {100, new Label {Comment = "c1", Name = "location1"}},
                {200, new Label {Comment = "c2", Name = "location2"}},
            };

            #region Equality
            protected bool Equals(TestRoot other)
            {
                return
                    Odw.SequenceEqual(other.Odw) &&
                    Odw2.SequenceEqual(other.Odw2);
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
                    return ((Odw != null ? Odw.GetHashCode() : 0) * 397) ^ (Odw2 != null ? Odw2.GetHashCode() : 0);
                }
            }
            #endregion
        }

        private static IConfigurationContainer GetSerializer()
        {
            return XmlSerializerSupport.GetSerializer()
                .EnableImplicitTyping(typeof(TestRoot));
        }

        [Theory]
        [EmbeddedResourceData("Diz.Test/Resources/serialize-dictionary-test.xml")]
        private void Serializer(string expectedXml)
        {
            var serializer = GetSerializer().Create();
            var toSerialize = new TestRoot();

            var xmlStr = serializer.Serialize(
                new XmlWriterSettings(),
                toSerialize);

            testOutputHelper.WriteLine(xmlStr);

            Assert.Equal(expectedXml, xmlStr);
        }

        [Fact]
        private void DeSerialize()
        {
            var serializer = GetSerializer().Create();
            var restoredRoot = serializer.Deserialize<TestRoot>(XmlShouldBe);

            var expected = new TestRoot();

            Assert.Equal(expected, restoredRoot);
        }

        const string XmlShouldBe = "<?xml version=\"1.0\" encoding=\"utf-8\"?><SerializerDictionaryTest-TestRoot xmlns:ns1=\"clr-namespace:IX.Observable;assembly=IX.Observable\" xmlns:sys=\"https://extendedxmlserializer.github.io/system\" xmlns:exs=\"https://extendedxmlserializer.github.io/v2\" xmlns:ns2=\"clr-namespace:Diz.Core.model;assembly=Diz.Core\"><Odw AutomaticallyCaptureSubItems=\"false\" HistoryLevels=\"50\"><sys:Item Key=\"1\"><Value Text=\"Z test1\" /></sys:Item><sys:Item Key=\"2\"><Value Text=\"Z test3\" /></sys:Item></Odw><Odw2 AutomaticallyCaptureSubItems=\"false\" HistoryLevels=\"50\"><sys:Item Key=\"100\"><Value Name=\"location1\" Comment=\"c1\" /></sys:Item><sys:Item Key=\"200\"><Value Name=\"location2\" Comment=\"c2\" /></sys:Item></Odw2></SerializerDictionaryTest-TestRoot>";
    }
}
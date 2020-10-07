using System.Xml;
using Diz.Core.model;
using Diz.Core.serialization.xml_serializer;
using Diz.Core.util;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace Diz.Test
{
    public class SerializerTest
    {
        private readonly ITestOutputHelper testOutputHelper;

        public SerializerTest(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        public class TestRoot
        {
            public OdWrapper<int, string> ODW { get; set; } = new OdWrapper<int, string>() { Dict = {
                {1, "Z test1"},
                {2, "Z test3"},
            }};
            public OdWrapper<int, Label> ODW2 { get; set; } = new OdWrapper<int, Label>() { Dict = {
                {100, new Label {comment = "c1", name = "location1"}},
                {200, new Label {comment = "c2", name = "location2"}},
            }};

            #region Equality

            protected bool Equals(TestRoot other)
            {
                return Equals(ODW, other.ODW);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((TestRoot) obj);
            }

            public override int GetHashCode()
            {
                return (ODW != null ? ODW.GetHashCode() : 0);
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
                new XmlWriterSettings() {},
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

        private readonly TestRoot testRootElementGood = new TestRoot();

        string xmlShouldBe = "<?xml version=\"1.0" +
                     "\" encoding=\"utf-8" +
                     "\"?><SerializerTest-" +
                     "TestRoot xmlns:ns1=" +
                     "\"clr-namespace:Diz." +
                     "Core.util;assembly=D" +
                     "iz.Core\" xmlns:exs=" +
                     "\"https://extendedxm" +
                     "lserializer.github.i" +
                     "o/v2\" xmlns:sys=\"h" +
                     "ttps://extendedxmlse" +
                     "rializer.github.io/s" +
                     "ystem\" xmlns:ns2=\"" +
                     "clr-namespace:Diz.Co" +
                     "re.model;assembly=Di" +
                     "z.Core\"><ODW><DictT" +
                     "oSave exs:type=\"sys" +
                     ":Dictionary[sys:int," +
                     "sys:string]\"><sys:I" +
                     "tem Key=\"1\" Value=" +
                     "\"Z test1\" /><sys:I" +
                     "tem Key=\"2\" Value=" +
                     "\"Z test3\" /></Dict" +
                     "ToSave></ODW><ODW2><" +
                     "DictToSave exs:type=" +
                     "\"sys:Dictionary[sys" +
                     ":int,ns2:Label]\"><s" +
                     "ys:Item Key=\"100\">" +
                     "<Value name=\"locati" +
                     "on1\" comment=\"c1\"" +
                     " /></sys:Item><sys:I" +
                     "tem Key=\"200\"><Val" +
                     "ue name=\"location2" +
                     "\" comment=\"c2\" />" +
                     "</sys:Item></DictToS" +
                     "ave></ODW2></Seriali" +
                     "zerTest-TestRoot>";
    }
}
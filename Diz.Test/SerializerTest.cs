using System.Xml;
using Diz.Core.model;
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

        public class Root
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

            protected bool Equals(Root other)
            {
                return Equals(ODW, other.ODW);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Root) obj);
            }

            public override int GetHashCode()
            {
                return (ODW != null ? ODW.GetHashCode() : 0);
            }

            #endregion
        }

        private static IExtendedXmlSerializer GetSerializer()
        {
            return new ConfigurationContainer()
                .UseOptimizedNamespaces()
                .EnableImplicitlyDefinedDefaultValues()

                .EnableMemberExceptionHandling() // debug only

                .ApplyAllOdWrapperConfigurations() // the important one for ODWrapper

                .Create();
        }

        [Fact]
        private void Serializer()
        {
            var serializer = GetSerializer();

            var xmlStr = serializer.Serialize(
                new XmlWriterSettings() {},
                rootElementGood);

            testOutputHelper.WriteLine(xmlStr);

            Assert.Equal(xmlShouldBe, xmlStr);
        }

        [Fact]
        private void DeSerialize()
        {
            var serializer = GetSerializer();
            var restoredRoot = serializer.Deserialize<Root>(xmlShouldBe);

            Assert.Equal(rootElementGood, restoredRoot);
        }

        private readonly Root rootElementGood = new Root();

        private const string xmlShouldBe = "<?xml version=\"1.0" + "\" encoding=\"utf-8" + "\"?><SerializerTest-" + "Root xmlns:ns1=\"clr" + "-namespace:Diz.Core." + "core.util;assembly=D" + "iz.Core\" xmlns:exs=" + "\"https://extendedxm" + "lserializer.github.i" + "o/v2\" xmlns:sys=\"h" + "ttps://extendedxmlse" + "rializer.github.io/s" + "ystem\" xmlns:ns2=\"" + "clr-namespace:Diztin" + "GUIsh;assembly=Diz.C" + "ore\" xmlns=\"clr-na" + "mespace:Diz.Test;ass" + "embly=Diz.Test\"><OD" + "W><DictToSave exs:ty" + "pe=\"sys:Dictionary[" + "sys:int,sys:string]" + "\"><sys:Item><Key>1<" + "/Key><Value>Z test1<" + "/Value></sys:Item><s" + "ys:Item><Key>2</Key>" + "<Value>Z test3</Valu" + "e></sys:Item></DictT" + "oSave></ODW><ODW2><D" + "ictToSave exs:type=" + "\"sys:Dictionary[sys" + ":int,ns2:Label]\"><s" + "ys:Item><Key>100</Ke" + "y><Value><name>locat" + "ion1</name><comment>" + "c1</comment></Value>" + "</sys:Item><sys:Item" + "><Key>200</Key><Valu" + "e><name>location2</n" + "ame><comment>c2</com" + "ment></Value></sys:I" + "tem></DictToSave></O" + "DW2></SerializerTest" + "-Root>";
    }
}
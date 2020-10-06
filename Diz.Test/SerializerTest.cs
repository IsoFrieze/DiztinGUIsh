using System.Xml;
using DiztinGUIsh.core.util;
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
            public OdWrapper<int, string> ODW { get; set; } = new OdWrapper<int, string>()
            {
                ObservableDict =
                {
                    {1, "Xtest1"},
                    {2, "Xtest3"},
                }
            };

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
                .EnableImplicitlyDefinedDefaultValues()
                .EnableMemberExceptionHandling() // debug only
                .AppendDisablingType<int, string>()
                .UseOptimizedNamespaces()
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

        readonly Root rootElementGood = new Root();
        private const string xmlShouldBe = "<?xml version=\"1.0\" encoding=\"utf-8\"?><SerializerTest-Root xmlns:ns1=\"clr-namespace:DiztinGUIsh.core.util;assembly=Diz.Core\" xmlns:exs=\"https://extendedxmlserializer.github.io/v2\" xmlns:sys=\"https://extendedxmlserializer.github.io/system\" xmlns=\"clr-namespace:Diz.Test;assembly=Diz.Test\"><ODW><DictToSave exs:type=\"sys:Dictionary[sys:int,sys:string]\"><sys:Item><Key>1</Key><Value>Xtest1</Value></sys:Item><sys:Item><Key>2</Key><Value>Xtest3</Value></sys:Item></DictToSave></ODW></SerializerTest-Root>";
    }
}
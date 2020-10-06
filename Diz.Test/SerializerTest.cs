using System.Xml;
using Diz.Core.core.util;
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
                Dict = {
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

                // .ApplyAllOdWrapperConfigurations() // the important one for ODWrapper

                .AppendDisablingType<int,string>()

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
        string xmlShouldBe = "<?xml version=\"1.0" +
                             "\" encoding=\"utf-8" +
                             "\"?><SerializerTest-" +
                             "Root xmlns:ns1=\"clr" +
                             "-namespace:Diz.Core." +
                             "core.util;assembly=D" +
                             "iz.Core\" xmlns:exs=" +
                             "\"https://extendedxm" +
                             "lserializer.github.i" +
                             "o/v2\" xmlns:sys=\"h" +
                             "ttps://extendedxmlse" +
                             "rializer.github.io/s" +
                             "ystem\" xmlns=\"clr-" +
                             "namespace:Diz.Test;a" +
                             "ssembly=Diz.Test\"><" +
                             "ODW><DictToSave exs:" +
                             "type=\"sys:Dictionar" +
                             "y[sys:int,sys:string" +
                             "]\"><sys:Item><Key>1" +
                             "</Key><Value>Xtest1<" +
                             "/Value></sys:Item><s" +
                             "ys:Item><Key>2</Key>" +
                             "<Value>Xtest3</Value" +
                             "></sys:Item></DictTo" +
                             "Save></ODW></Seriali" +
                             "zerTest-Root>";
    }
}
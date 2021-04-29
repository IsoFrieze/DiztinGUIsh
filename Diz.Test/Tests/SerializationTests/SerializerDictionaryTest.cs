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

namespace Diz.Test.Tests.SerializationTests
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
            public Dictionary<int, Comment> Odw { get; } = new();
            public Dictionary<int, Label> Odw2 { get; } = new();

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
                if (obj.GetType() != GetType()) return false;
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

        private static TestRoot GetSerializeData()
        {
            return new()
            {
                Odw =
                {
                    {1, new Comment {Text = "Z test1"}},
                    {2, new Comment {Text = "Z test3"}},
                },
                Odw2 =
                {
                    {100, new Label {Comment = "c1", Name = "location1"}},
                    {200, new Label {Comment = "c2", Name = "location2"}},
                }
            };
        }

        [Theory]
        [EmbeddedResourceData("Diz.Test/Resources/serialize-dictionary-test.xml")]
        private void Serializer(string expectedXml)
        {
            var expectedCleanedXml = SortaCleanupXml(expectedXml);
            
            var serializer = GetSerializer().Create();
            var toSerialize = GetSerializeData();

            var xmlStr = serializer.Serialize(
                new XmlWriterSettings(),
                toSerialize);

            testOutputHelper.WriteLine(xmlStr);

            Assert.Equal(expectedCleanedXml, xmlStr);
        }

        private static string SortaCleanupXml(string expectedXml)
        {
            return expectedXml.Replace("\r\n", "");
        }

        [Theory]
        [EmbeddedResourceData("Diz.Test/Resources/serialize-dictionary-test.xml")]
        private void DeSerialize(string inputXml)
        {
            var inputCleanedXml = SortaCleanupXml(inputXml);

            var expectedObj = GetSerializeData();

            var serializer = GetSerializer().Create();
            var actuallyDeserialized = serializer.Deserialize<TestRoot>(inputCleanedXml);

            Assert.Equal(expectedObj, actuallyDeserialized);
        }
    }
}
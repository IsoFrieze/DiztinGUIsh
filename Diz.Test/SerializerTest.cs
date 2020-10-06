using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using DiztinGUIsh.loadsave.xml_serializer;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using IX.Observable;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

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

        public class OdWrapper<TKey, TValue>
        {
            public ObservableDictionary<TKey, TValue> ObservableDict { get; set; } 
                = new ObservableDictionary<TKey, TValue>();

            public IDictionary DictToSave
            {
                get => new Dictionary<TKey, TValue>(ObservableDict);
                set
                {
                    ObservableDict.Clear();
                    foreach (DictionaryEntry item in value)
                    {
                        ObservableDict.Add((TKey)item.Key, (TValue)item.Value);
                    }
                }
            }

            #region Equality

            protected bool Equals(OdWrapper<TKey, TValue> other)
            {
                return ObservableDict.SequenceEqual(other.ObservableDict);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((OdWrapper<TKey, TValue>) obj);
            }

            public override int GetHashCode()
            {
                return (ObservableDict != null ? ObservableDict.GetHashCode() : 0);
            }

            #endregion
        }

        private static IExtendedXmlSerializer GetSerializer()
        {
            return new ConfigurationContainer()
                .EnableImplicitlyDefinedDefaultValues()
                .EnableMemberExceptionHandling() // debug only
                .Type<OdWrapper<int, string>>()
                .Member(x => x.ObservableDict)
                .Ignore()
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
        const string xmlShouldBe = @"<?xml version=""1.0"" encoding=""utf-8""?><SerializerTest-Root xmlns:exs=""https://extendedxmlserializer.github.io/v2"" xmlns:sys=""https://extendedxmlserializer.github.io/system"" xmlns=""clr-namespace:Diz.Test;assembly=Diz.Test""><ODW><DictToSave exs:type=""sys:Dictionary[sys:int,sys:string]""><sys:Item><Key>1</Key><Value>Xtest1</Value></sys:Item><sys:Item><Key>2</Key><Value>Xtest3</Value></sys:Item></DictToSave></ODW></SerializerTest-Root>";
    }
}
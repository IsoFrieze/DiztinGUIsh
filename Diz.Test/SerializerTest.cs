using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
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
                    foreach (var item in value)
                    {
                        ObservableDict.Add((KeyValuePair<TKey, TValue>)(item));
                    }
                }
            }
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

            var rootElement = new Root();

            var xmlStr = serializer.Serialize(
                new XmlWriterSettings() {},
                rootElement);

            testOutputHelper.WriteLine(xmlStr);
            const string expected = @"<?xml version=""1.0"" encoding=""utf-8""?><SerializerTest-Root xmlns:exs=""https://extendedxmlserializer.github.io/v2"" xmlns:sys=""https://extendedxmlserializer.github.io/system"" xmlns=""clr-namespace:Diz.Test;assembly=Diz.Test""><ODW><DictToSave exs:type=""sys:Dictionary[sys:int,sys:string]""><sys:Item><Key>1</Key><Value>Xtest1</Value></sys:Item><sys:Item><Key>2</Key><Value>Xtest3</Value></sys:Item></DictToSave></ODW></SerializerTest-Root>";

            Assert.Equal(expected, xmlStr);
        }
    }
}
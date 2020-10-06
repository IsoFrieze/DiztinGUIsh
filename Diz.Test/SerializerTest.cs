using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using ExtendedXmlSerializer.ContentModel;
using ExtendedXmlSerializer.ContentModel.Content;
using ExtendedXmlSerializer.ContentModel.Format;
using ExtendedXmlSerializer.Core.Specifications;
using ExtendedXmlSerializer.ExtensionModel;
using ExtendedXmlSerializer.ExtensionModel.Instances;
using IX.Observable;
using IX.Undoable;
using Xunit;

namespace Diz.Test
{
    public class SerializerTest
    {
        public class Root
        {
            public OdWrapper<int, string> ODW { get; set; } = new OdWrapper<int, string>()
            {
                ObservableDict =
                {
                    {1, "test1"},
                    {2, "test3"},
                }
            };
        }

        public class OdWrapper<TKey, TValue>
        {
            public ObservableDictionary<TKey, TValue> ObservableDict { get; set; } 
                = new ObservableDictionary<TKey, TValue>();

            public Dictionary<TKey, TValue> DictToSave
            {
                get => new Dictionary<TKey, TValue>(ObservableDict);
                set
                {
                    ObservableDict.Clear();
                    foreach (var item in value)
                    {
                        ObservableDict.Add(item);
                    }
                }
            }
        }

        private static IExtendedXmlSerializer GetSerializer()
        {
            return new ConfigurationContainer()
                .EnableImplicitlyDefinedDefaultValues()
                .EnableMemberExceptionHandling() // debug only
                .Type<OdWrapper<int,string>>()
                .Member(x=>x.ObservableDict)
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
                new XmlWriterSettings {Indent = true},
                rootElement);

            // Console.WriteLine(xmlStr);
            Assert.Equal($@"<?xml version=""1.0"" encoding=""utf-8""?>
<SerializerTest-Root xmlns:sys=""https://extendedxmlserializer.github.io/system"" xmlns:exs=""https://extendedxmlserializer.github.io/v2"" xmlns=""clr-namespace:Diz.Test;assembly=Diz.Test"">
  <ODW>
    <DictToSave>
      <sys:Item>
        <Key>1</Key>
        <Value>test1</Value>
      </sys:Item>
      <sys:Item>
        <Key>2</Key>
        <Value>test3</Value>
      </sys:Item>
    </DictToSave>
  </ODW>
</SerializerTest-Root>", xmlStr);
        }
    }
}
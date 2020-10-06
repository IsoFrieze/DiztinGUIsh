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
            // public ODWrapper<int, string> wrap { get; set; } = new ODWrapper<int, string>
            public ODWrapper<int, string> ODW { get; set; } = new ODWrapper<int, string>()
            {
                ObservableDict =
                {
                    {1, "test1"},
                    {2, "test3"},
                }
            };
        }

        public class ODWrapper<TKey, TValue>//  : ICollection<KeyValuePair<TKey, TValue>>
        {
            private ObservableDictionary<TKey, TValue> dict = new ObservableDictionary<TKey, TValue>();


            public ObservableDictionary<TKey, TValue> ObservableDict
            {
                get => dict;
                set => dict = value;
            }

            public Dictionary<TKey, TValue> DictToSave
            {
                /*get
                {
                    return dict;
                }
                set
                {
                    dict = value;
                }*/
                get => new Dictionary<TKey, TValue>(dict);
                set
                {
                    dict.Clear();
                    foreach (var item in value)
                    {
                        dict.Add(item);
                    }
                }
            }
        }

        private static IExtendedXmlSerializer GetSerializer()
        {
            return new ConfigurationContainer()
                .EnableImplicitlyDefinedDefaultValues()
                .EnableMemberExceptionHandling() // debug only
                .Type<ODWrapper<int,string>>()
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
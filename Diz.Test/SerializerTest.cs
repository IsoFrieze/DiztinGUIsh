using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using IX.Observable;
using IX.Undoable;
using Xunit;

namespace Diz.Test
{
    public class SerializerTest
    {
        internal class Root
        {
            private ODWrapper<int, string> wrap { get; set; } = new ODWrapper<int, string>
            {
                OD = {
                    [3] = "Test3",
                    [4] = "Test4"
                }
            };
}
        internal class ODWrapper<TKey, TValue> : ICollection<KeyValuePair<TKey, TValue>>
        {
            //public ObservableDictionaryAdaptor<int, string> dict { get; set; }
            //    = new ObservableDictionaryAdaptor<int, string>();

            public ObservableDictionary<TKey, TValue> OD { get; set; } = new ObservableDictionary<TKey, TValue>();

            public Dictionary<TKey,TValue> Dict
            {
                get => new Dictionary<TKey, TValue>(OD);
                set
                {
                    OD.Clear();
                    foreach (var item in value)
                    {
                        OD.Add(item);
                    }
                }
            }

            public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
            {
                return OD.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void Add(KeyValuePair<TKey, TValue> item)
            {
                OD.Add(item.Key, item.Value);
            }

            public void Clear()
            {
                OD.Clear();
            }

            public bool Contains(KeyValuePair<TKey, TValue> item)
            {
                return OD.Contains(item);
            }

            public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
            {
                OD.CopyTo(array, arrayIndex);
            }

            public bool Remove(KeyValuePair<TKey, TValue> item)
            {
                return OD.Remove(item);
            }

            public int Count => OD.Count;
            public bool IsReadOnly => OD.IsReadOnly;
        }

        private static IExtendedXmlSerializer GetSerializer()
        {
            return new ConfigurationContainer()
                // .UseAutoFormatting()
                .EnableImplicitTyping(typeof(Root))
                .EnableImplicitTyping(typeof(ODWrapper<int,string>))
                .Type<ODWrapper<int,string>>()
                .Member(x=>x.OD).Ignore()
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

            // NOT WORKING. doesn't output the right text, skipping all elements
            Console.WriteLine(xmlStr);
            Assert.Equal("asdf", xmlStr);
        }
    }
}
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

            public ODWrapper()
            {

            }

            //public ObservableDictionaryAdaptor<int, string> dict { get; set; }
            //    = new ObservableDictionaryAdaptor<int, string>();

            // public ObservableDictionary<TKey, TValue> OD { get; set; } = new ObservableDictionary<TKey, TValue>();

            /*public Dictionary<TKey,TValue> Dict
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
            public bool IsReadOnly => OD.IsReadOnly;*/
        }

        private static IExtendedXmlSerializer GetSerializer()
        {
            // THIS WORKS. makes a copy but it works.
            //var od = new ObservableDictionary<int, string>() {{1,"Asdf"}};
            //var dd = new Dictionary<int, string>(od);
            //var id = (IDictionary) dd;

            return new ConfigurationContainer()
                .EnableImplicitlyDefinedDefaultValues()
                .EnableMemberExceptionHandling() // debug only
                .Type<ODWrapper<int,string>>()
                .Member(x=>x.ObservableDict)
                .Ignore()
                /* // .UseAutoFormatting()
                
                
                .EnableImplicitTyping(typeof(ObservableDictionary<int, string>))
                // .EnableImplicitTyping(typeof(ODWrapper<int,string>))
                //.Type<ODWrapper<int,string>>()
                .Member(x=>x.OD).CustomSerializer(ser, deser)
                .Type<ObservableDictionary<int, string>>()
                .WithInterceptor(new Interc())
                .Type<IDictionary>()
                .WithInterceptor(new Interc())
                .Type<IDictionary<int,string>>()
                .WithInterceptor(new Interc())
                .Type<ObservableDictionary<int,string>>()
                                .WithInterceptor(new Interc2())
                .Extend(new ObservableDictionaryExtension<int, string>())
                // .Type<ObservableDictionary<int,string>>().Register().Serializer().Using(new ObsSerializer())
                // .Type<Root>().WithInterceptor(new Interc())
                // .Type<ODWrapper<int,string>>().CustomSerializer(ser, deser) // WORKS
                */
                .UseOptimizedNamespaces()
                .Create();
        }

        private static ODWrapper<int, string> deser(XElement arg)
        {
            throw new NotImplementedException();
        }

        private static void ser(XmlWriter arg1, ODWrapper<int, string> arg2)
        {
            
            // throw new NotImplementedException();
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

    public class Interc2 : SerializationActivator
    {
        public override object Activating(Type instanceType)
        {
            // processor should be retrieved from IoC container, but created manually for simplicity of test 
            //var processor = new Processor(new Service());
            //return processor;
            throw new NotImplementedException();
        }
    }

    internal class ObservableDictionaryExtension<TKey, TValue> : ISerializerExtension
    {
        public IServiceRepository Get(IServiceRepository parameter)
        {
            // idea.    return parameter.Decorate<IActivatingTypeSpecification>(Register).Decorate<IActivators>(Register).Decorate<IContents>(Register);
            // parameter.Decorate(ISpecification<ObservableDictionaryExtension<TKey, TValue>>)(Register);
            // return parameter
            return null;
        }

        public void Execute(IServices parameter)
        {
            throw new NotImplementedException();
        }
    }

    internal class Interc : ISerializationInterceptor
    {
        public object Serializing(IFormatWriter writer, object instance)
        {
            throw new NotImplementedException();
        }

        public object Activating(Type instanceType)
        {
            throw new NotImplementedException();
        }

        public object Deserialized(IFormatReader reader, object instance)
        {
            throw new NotImplementedException();
        }
    }

    internal class ObsSerializer : ISerializer<ObservableDictionary<int, string>>
    {
        public ObservableDictionary<int, string> Get(IFormatReader parameter)
        {
            int x = 3;
            return null;
        }

        public void Write(IFormatWriter writer, ObservableDictionary<int, string> instance)
        {
            int x = 3;
        }
    }
}
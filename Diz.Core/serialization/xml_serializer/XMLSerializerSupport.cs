using System;
using System.Collections.Generic;
using System.Diagnostics;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.model.snes;
using Diz.Core.serialization.xml_serializer.xml_migrations;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using ExtendedXmlSerializer.ContentModel.Format;
using ExtendedXmlSerializer.ExtensionModel.Instances;

namespace Diz.Core.serialization.xml_serializer
{

    /*public sealed class HexIntConverter : IConverter<int>
    {
        public static HexIntConverter Default { get; } = new();
        HexIntConverter() { }

        public bool IsSatisfiedBy(TypeInfo parameter) => typeof(int).GetTypeInfo()
            .IsAssignableFrom(parameter);

        public int Parse(string data)
        {
            return Util.ParseHexOrBase10String(data);
        }

        public string Format(int instance)
        {
            return instance.ToString("X");
        }
    }



    public class HexKVPSerializer : IExtendedXmlCustomSerializer<KeyValuePair<int, string>>, IExtendedXmlCustomSerializer
    {
        public void Serializer(XmlWriter xmlWriter, KeyValuePair<int, string> kvp)
        {
            var (key, value) = kvp;
            xmlWriter.WriteElementString("Key", key.ToString("X"));
            xmlWriter.WriteElementString("Value", value);
        }

        KeyValuePair<int, string> IExtendedXmlCustomSerializer<KeyValuePair<int, string>>.Deserialize(XElement xElement)
        {
            var xElementKey = xElement.Member("Key");
            var xElementValue = xElement.Member("Value");

            if (xElementKey == null || xElementValue == null)
                throw new InvalidOperationException("Invalid xml for class TestClassWithSerializer");

            var strValue = xElement.Value;

            var intValue = Util.ParseHexOrBase10String(xElementKey.Value);
            return new KeyValuePair<int, string>(intValue, strValue);
        }
    }*/
    
    /*
    sealed class Monitor : ISerializationMonitor<string>
    {
        // readonly List<string> _store;

        // public Monitor(List<string> store) => _store = store;

        public void OnSerializing(IFormatWriter writer, string instance)
        {
            Trace.WriteLine(instance);
        }

        public void OnSerialized(IFormatWriter writer, string instance)
        {
            Trace.WriteLine(instance);
            // _store.Add(instance);
        }

        public void OnDeserializing(IFormatReader reader, Type instanceType)
        {
            Trace.WriteLine(instanceType.ToString());
        }

        public void OnActivating(IFormatReader reader, Type instanceType)
        {
            Trace.WriteLine(instanceType.ToString());
        }

        public void OnActivated(string instance)
        {
            Trace.WriteLine(instance);
        }

        public void OnDeserialized(IFormatReader reader, string instance)
        {
            Trace.WriteLine(instance);
        }
    }*/

    public static class XmlSerializerSupport
    {
        public static IConfigurationContainer GetSerializer()
        {
            // This configuration changes how parts of the data structures are serialized back/forth to XML.
            // This is using the ExtendedXmlSerializer library, which has a zillion config options and is 
            // awesome.
            //
            // TODO: would be cool if these were stored as attributes on the classes themselves
            return new ConfigurationContainer()

                .Type<Project>()
                // .WithMonitor(new Monitor())
                .Member(x => x.UnsavedChanges).Ignore()
                .Member(x => x.ProjectFileName).Ignore()

                .Type<ByteSource>()
                .Register().Serializer().Using(RomBytesSerializer.Default)

                .Type<Data>()
                // tmp. eventually, we do need to serialize this stuff.
                .Member(x => x.SnesAddressSpace).Ignore()
                .Member(x => x.RomByteSource).Ignore()
                // .Member(x => x.RomBytes).Ignore()

                // .Member(x=>x.Comments)
                // TODO: trying to get a converter up and running. not working yet....
                // .Register().Converter(HexIntConverter.Default)
                // .Member(x => x.Comments.Keys).Register().Converter().)
                // .CustomSerializer(new HexKVPSerializer())// cant get it working!!!
                // .AddMigration(new DizProjectMigrations())
                
                .UseOptimizedNamespaces()
                .UseAutoFormatting()

                .EnableImplicitTyping(typeof(Data))
                .EnableImplicitTyping(typeof(Label));
        }
    }
}
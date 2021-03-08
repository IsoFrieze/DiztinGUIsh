using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Diz.Core.model;
using Diz.Core.util;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using ExtendedXmlSerializer.ContentModel.Conversion;
using ExtendedXmlSerializer.ExtensionModel.Xml;

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
                .Member(x => x.UnsavedChanges).Ignore()
                .Member(x => x.ProjectFileName).Ignore()
                // .Member(x => x.CurrentViewOffset).Ignore()

                .Type<RomBytes>()
                .Register().Serializer().Using(RomBytesSerializer.Default)
                
                .Type<Data>()// .Register().Converter(HexIntConverter.Default)
                // .Member(x => x.Comments.Keys).Register().Converter().)
                .Member(x=>x.Comments)
                // .CustomSerializer(new HexKVPSerializer())// cant get it working!!!
                .UseOptimizedNamespaces()
                .UseAutoFormatting()

                .EnableImplicitTyping(typeof(Data))
                .EnableImplicitTyping(typeof(Label));
        }
    }
}
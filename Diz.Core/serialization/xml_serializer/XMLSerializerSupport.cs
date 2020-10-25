using Diz.Core.model;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;

namespace Diz.Core.serialization.xml_serializer
{
    public static class XmlSerializerSupport
    {
        public static IConfigurationContainer GetSerializer()
        {
            // This configuration changes how parts of the data structures are serialized back/forth to XML.
            // This is using the ExtendedXmlSerializer library, which has a zillion config options and is 
            // awesome.
            return new ConfigurationContainer()
                
                .Type<Project>()
                .Member(x => x.UnsavedChanges).Ignore()
                
                .Type<RomBytes>()
                .Register().Serializer().Using(RomBytesSerializer.Default)
                
                .Type<Data>()
                .UseOptimizedNamespaces()
                .UseAutoFormatting()
                
                .EnableImplicitTyping(typeof(Data))
                .EnableImplicitTyping(typeof(Label));
        }
    }
}
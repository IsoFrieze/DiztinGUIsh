using Diz.Core.model;
using Diz.Core.util;
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

            // TODO: doesn't work for saving ObservableDictionary related stuff yet. we are working
            // around it with a wrapper, but, it's clumsy.  Somewhere in these options is a way to fix it,
            // just gotta figure it out.

            return new ConfigurationContainer()
                .Type<Project>()
                .Member(x => x.UnsavedChanges).Ignore()
                .Type<RomBytes>()
                .Register().Serializer().Using(RomBytesSerializer.Default)
                .Type<Data>()

                .ApplyAllOdWrapperConfigurations() // important for ODWrapper to serialize correctly.

                .UseOptimizedNamespaces()
                .UseAutoFormatting()
                .EnableImplicitTyping(typeof(Data))
                .EnableImplicitTyping(typeof(Label));
        }
    }
}
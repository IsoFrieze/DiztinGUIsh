using System;
using Diz.Core.model;
using Diz.Core.model.snes;
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
            //
            // TODO: would be cool if these were stored as attributes on the classes themselves
            return new ConfigurationContainer()

                .Type<Project>()
                .Member(x => x.ProjectFileName).Ignore()

                .Type<RomBytes>()
                .Register().Serializer().Using(RomBytesSerializer.Default)

                .Type<Data>()
                .Member(x => x.LabelsSerialization)
                .Name("Labels")
                .UseOptimizedNamespaces()
                .UseAutoFormatting()
                .EnableReferences()
                .EnableImplicitTyping()

                .Type<Label>()
#if DIZ_3_BRANCH
                .Name("L")
                .Member(x => x.Comment).Name("Cmt").EmitWhen(text => !string.IsNullOrEmpty(text))
                .Member(x => x.Name).Name("V").EmitWhen(text => !string.IsNullOrEmpty(text))
#endif
                .EnableImplicitTyping();
        }
    }
}
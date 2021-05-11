using System.Xml;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.model.snes;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using JetBrains.Annotations;

namespace Diz.Core.serialization.xml_serializer
{
    // WIP
    public class XmlSerializationSupport
    {
        private sealed class ByteEntryProfile : IConfigurationProfile
        {
            public static ByteEntryProfile Default { get; } = new();

            private ByteEntryProfile() {}

            // use some really short names and don't output defaults, these are going to be output a LOT in the XML file
            public IConfigurationContainer Get(IConfigurationContainer parameter)
                => parameter
                    .Type<Project>()

                    .Type<ByteEntry>()
                    .Name("B")
                    .Member(x=>x.Annotations)
                    .Name("A")
                    
                    .Type<OpcodeAnnotation>()
                    .Name("O")
                    .Member(x=>x.Arch).EmitWhen(arch => arch != default)
                    .Member(x=>x.DataBank).Name("DB").EmitWhen(db => db != default)
                    .Member(x=>x.DirectPage).Name("DP").EmitWhen(dp => dp != default)
                    .Member(x=>x.XFlag).Name("X").EmitWhen(flag=> flag != default)
                    .Member(x=>x.MFlag).Name("M").EmitWhen(flag=> flag != default)
                    .EnableImplicitTyping(typeof(OpcodeAnnotation))
                    
                    .Type<MarkAnnotation>()
                    .Name("M")
                    .Member(x=>x.TypeFlag).Name("V").EmitWhen(typeFlag => typeFlag != default)
                    .EnableImplicitTyping()
                    
                    .Type<ByteAnnotation>()
                    .Name("B")
                    .Member(x=>x.Val).Name("V").EmitWhen(db => db != default)
                    .EnableImplicitTyping()
                    
                    .Type<Comment>()
                    .Name("C")
                    .Member(x=>x.Text).Name("V").EmitWhen(text => !string.IsNullOrEmpty(text))
                    .EnableImplicitTyping()
                    
                    .Type<Label>()
                    .Name("L")
                    .Member(x=>x.Comment).Name("Cmt").EmitWhen(text => !string.IsNullOrEmpty(text))
                    .Member(x=>x.Name).Name("V").EmitWhen(text => !string.IsNullOrEmpty(text))
                    .EnableImplicitTyping()

                    .EnableReferences()
                    .UseOptimizedNamespaces()
                    .UseAutoFormatting();
        }
        
        private  sealed class AnnotationProfile : IConfigurationProfile
        {
            public static AnnotationProfile Default { get; } = new();
            private AnnotationProfile() {}
            public IConfigurationContainer Get(IConfigurationContainer parameter)
                => parameter
                    .Type<Annotation>()
                    .Member(x=>x.Parent)
                    .EnableReferences()
                    .UseOptimizedNamespaces()
                    .UseAutoFormatting();
        }
        
        private sealed class AnnotationCollectionProfile : IConfigurationProfile
        {
            public static AnnotationCollectionProfile Default { get; } = new();
            private AnnotationCollectionProfile() {}
            public IConfigurationContainer Get(IConfigurationContainer parameter)
                => parameter.Type<AnnotationCollection>()
                    .UseOptimizedNamespaces()
                    .UseAutoFormatting();
        }

        private class ByteStorageProfile : IConfigurationProfile
        {
            public static ByteStorageProfile Default { get; } = new();
            private ByteStorageProfile() {}
            public IConfigurationContainer Get(IConfigurationContainer parameter)
            {
                return parameter
                    .Type<StorageSparse<ByteEntry>>()
                    .EnableParameterizedContent() // IMPORTANT! TAKE NOTE!
                    .EnableReferences()

                    .Type<StorageList<ByteEntry>>()
                    .EnableReferences();
            }
        }
        
        private sealed class DataProfile : IConfigurationProfile
        {
            public static DataProfile Default { get; } = new();
            private DataProfile() {}
            public IConfigurationContainer Get(IConfigurationContainer parameter)
                => parameter.Type<Data>()
                    .EnableParameterizedContent() // IMPORTANT
                    .UseOptimizedNamespaces()
                    .UseAutoFormatting()
                    .EnableReferences();
            
            // .AddMigration(new DizProjectMigrations())
        }

        public class MainProfile : CompositeConfigurationProfile
        {
            public static MainProfile Default { get; } = new();
            private MainProfile() : base(
                    DataProfile.Default,
                    AnnotationProfile.Default, 
                    ByteStorageProfile.Default, 
                    AnnotationCollectionProfile.Default, 
                    ByteEntryProfile.Default) 
            {}
        }

        [UsedImplicitly]
        public static IConfigurationContainer GetConfig() =>
            ConfiguredContainer.New<MainProfile>()
                .UseOptimizedNamespaces()
                .UseAutoFormatting()
                .EnableImplicitTyping(
                    typeof(OpcodeAnnotation),
                    typeof(MarkAnnotation),
                    typeof(ByteAnnotation),
                    typeof(BranchAnnotation),
                    typeof(Comment),
                    typeof(Label),
                    typeof(ByteEntry),
                    typeof(ByteSource),
                    typeof(Storage<ByteEntry>),
                    typeof(StorageList<ByteEntry>),
                    typeof(StorageSparse<ByteEntry>),
                    typeof(AnnotationCollection)
                )
                .EnableImplicitTyping()
                .Type<ByteSource>()
                .EnableReferences()
                .Type<RegionMapping>()
                .EnableReferences()
                .Type<ByteSourceMapping>()
                .EnableReferences();

        public static string Serialize(object toSerialize)
        {
            var config = GetConfig().Create();
            
            return config.Serialize(
                new XmlWriterSettings {OmitXmlDeclaration = false, Indent = true, NewLineChars = "\r\n"},
                toSerialize);
        }
        
        public static T Deserialize<T>(string input)
        {
            var config = GetConfig().Create();
            return config.Deserialize<T>(input);
        }
    }
}

using System.Xml;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.model.snes;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using JetBrains.Annotations;

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
                // .WithMonitor(new Monitor())
                .Member(x => x.UnsavedChanges).Ignore()
                .Member(x => x.ProjectFileName).Ignore()

                // TODO: we will need this. it's not quite on ByteSource. and just for reading. and just during migration
                //.Type<ByteSource>()
                //.Register().Serializer().Using(RomBytesSerializer.Default)

                .Type<Data>()
                // tmp. eventually, we do need to serialize this stuff.
                .Member(x => x.SnesAddressSpace).Ignore()
                .Member(x => x.RomByteSource).Ignore()
                // .Member(x => x.RomBytes).Ignore()

                // .AddMigration(new DizProjectMigrations())

                .UseOptimizedNamespaces()
                .UseAutoFormatting()

                .EnableImplicitTyping(typeof(Data), typeof(Label));
        }
    }
    
    // WIP
    public class XmlSerializationSupportNew
    {
        private sealed class ByteEntryProfile : IConfigurationProfile
        {
            public static ByteEntryProfile Default { get; } = new();

            private ByteEntryProfile() {}

            public IConfigurationContainer Get(IConfigurationContainer parameter)
                => parameter
                    .Type<ByteEntry>()
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
                    .Member(x=>x.Parent).Ignore()
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
                    .Member(x => x.IsReadOnly).Ignore()
                    .Member(x => x.IsSynchronized).Ignore()
                    .Member(x => x.SyncRoot).Ignore()
                    // TODO: add this eventually: .Member(x => x.ActualCount).Ignore() // because it's always just equal to# elements in the dict 
                    .EnableParameterizedContent() // IMPORTANT
                    .EnableReferences()

                    .Type<StorageList<ByteEntry>>()
                    // .Member(x => x.IsReadOnly).Ignore()
                    // .Member(x => x.IsSynchronized).Ignore()
                    // .Member(x => x.SyncRoot).Ignore()
                    .EnableReferences();
            }
        }

        public class MainProfile : CompositeConfigurationProfile
        {
            public static MainProfile Default { get; } = new();
            private MainProfile() : base(
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
                // .EnableReferences()
                //.Type<Storage<ByteEntry>>()
//                .EnableReferences()
                //.Type<ByteStorageList>()
//                .EnableReferences()
                //.Type<StorageSparse<ByteEntry>>()
//                .EnableReferences()
                .Type<RegionMapping>()
//                .EnableReferences()
                .Type<ByteSourceMapping>();
//                .EnableReferences();

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

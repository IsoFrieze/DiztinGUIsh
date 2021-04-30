using System.Reflection;
using System.Xml;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.model.snes;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using ExtendedXmlSerializer.ContentModel;
using ExtendedXmlSerializer.ContentModel.Content;
using ExtendedXmlSerializer.ContentModel.Format;
using ExtendedXmlSerializer.Core;
using ExtendedXmlSerializer.ExtensionModel;
using ISerializer = ExtendedXmlSerializer.ContentModel.ISerializer;
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
                .Type<ByteSource>()
                .Register().Serializer().Using(RomBytesSerializer.Default)

                .Type<Data>()
                // tmp. eventually, we do need to serialize this stuff.
                .Member(x => x.SnesAddressSpace).Ignore()
                .Member(x => x.RomByteSource).Ignore()
                // .Member(x => x.RomBytes).Ignore()

                // .AddMigration(new DizProjectMigrations())
                
                .UseOptimizedNamespaces()
                .UseAutoFormatting()

                .EnableImplicitTyping(typeof(Data))
                .EnableImplicitTyping(typeof(Label));
        }
    }
    
    // WIP
    public class XmlSerializationSupportNew
    {
        // .Type<Project>()
        // .Member(x => x.UnsavedChanges).Ignore()
        // .Member(x => x.ProjectFileName).Ignore()
        //
        // .Type<ByteSource>()
        // .Register().Serializer().Using(RomBytesSerializer.Default)
        //
        // .Type<Data>()
        // // tmp. eventually, we do need to serialize this stuff.
        // .Member(x => x.SnesAddressSpace).Ignore()
        // .Member(x => x.RomByteSource).Ignore()
        // // .Member(x => x.RomBytes).Ignore()
        //
        // // .Member(x=>x.Comments)
        // // TODO: trying to get a converter up and running. not working yet....
        // // .Register().Converter(HexIntConverter.Default)
        // // .Member(x => x.Comments.Keys).Register().Converter().)
        // // .CustomSerializer(new HexKVPSerializer())// cant get it working!!!
        // // .AddMigration(new DizProjectMigrations())

        private sealed class ByteEntryProfile : IConfigurationProfile
        {
            public static ByteEntryProfile Default { get; } = new();

            private ByteEntryProfile() {}

            public IConfigurationContainer Get(IConfigurationContainer parameter)
                => parameter
                    .Type<ByteEntry>()
                    
                    // this class has a ton of helper properties that access stuff in .Annotations. ignore all of that
                    // this class has a bunch of stuff about where it lives in parents, bytesources, etc. we don't need
                    // to serialize it, it should be recreated when 
                    
                    // this should work in place of all the .Ignore() stuff below... but it doesn't yet.
                    // sigh.
                    // .Member(x => x.Annotations).Include()
                    // .EmitWhenInstance()
                    
                    // set to include (whitelist) ONLY the stuff configured with .Member() above
                    // .IncludeConfiguredMembers()
                    
                    // ignore all helper stuff, this'll be in the Annotations list
                    // try using [XmlIgnoreAttribute]
                    .Member(x => x.Arch).Ignore()
                    .Member(x => x.Byte).Ignore()
                    .Member(x => x.Point).Ignore()
                    .Member(x => x.DataBank).Ignore()
                    .Member(x => x.DirectPage).Ignore()
                    .Member(x => x.MFlag).Ignore()
                    .Member(x => x.XFlag).Ignore()
                    .Member(x => x.TypeFlag).Ignore()
                    
                    // ignore anything about our parent references, that'll be re-created
                    .Member(x => x.DontSetParentOnCollectionItems).Ignore()
                    .Member(x => x.Parent).Ignore()
                    .Member(x => x.ParentIndex).Ignore()
                    // TODO: re-enable // .Member(x => x.ParentByteSource).Ignore()

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
                    .Member(x=>x.DontSetParentOnCollectionItems).Ignore()
                    .Member(x=>x.EnforcePolicy).Ignore()
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

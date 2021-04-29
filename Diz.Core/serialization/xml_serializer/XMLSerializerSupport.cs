using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
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
using ExtendedXmlSerializer.ExtensionModel.Instances;
using ExtendedXmlSerializer.ExtensionModel.Xml;
using JetBrains.Annotations;
using ISerializer = ExtendedXmlSerializer.ContentModel.ISerializer;

namespace Diz.Core.serialization.xml_serializer
{
    internal sealed class StorageSparseExtension : ISerializerExtension
    {
        public static StorageSparseExtension Default { get; } = new();

        private StorageSparseExtension() {}

        public IServiceRepository Get(IServiceRepository parameter) => 
            parameter.DecorateContentsWith<Contents>().Then();

        void ICommand<IServices>.Execute(IServices parameter) {}

        private sealed class Contents : IContents
        {
            private readonly IContents previous;
            private readonly ISerializer<StorageSparse<ByteEntry>> storageSparse;

            public Contents(IContents previous)
                : this(previous, 
                    new StorageSparseSerializer(
                        // the reason this recurses is the .For() which isn't allowed to 
                        // return the same type of ISerializer<Stor<Byt>>
                        previous.Get(typeof(StorageSparse<ByteEntry>)).For<StorageSparse<ByteEntry>>())
                    ) {}

            public Contents(IContents previous, ISerializer<StorageSparse<ByteEntry>> storageSparse)
            {
                this.previous = previous;
                this.storageSparse = storageSparse;
            }

            public ISerializer Get(TypeInfo parameter)
                => parameter == typeof(StorageSparse<ByteEntry>) 
                    ? storageSparse.Adapt() 
                    : previous.Get(parameter);
        }

        private sealed class StorageSparseSerializer : ISerializer<StorageSparse<ByteEntry>>
        {
            private readonly ISerializer<StorageSparse<ByteEntry>> previous;

            public StorageSparseSerializer(ISerializer<StorageSparse<ByteEntry>> previous) => 
                this.previous = previous;

            public StorageSparse<ByteEntry> Get(IFormatReader parameter)
            {
                return previous.Get(parameter);
            }

            public void Write(IFormatWriter writer, StorageSparse<ByteEntry> instance)
            {
                previous.Write(writer, instance);
            }
        }
    }
    
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
        
        private  sealed class AnnotationCollectionProfile : IConfigurationProfile
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

        private class ByteStorageProfileRead : ByteStorageProfileBase
        {
            public static ByteStorageProfileRead Default { get; } = new();

            private ByteStorageProfileRead() : base(forReading: true) {}
        }
        
        private class ByteStorageProfileWrite : ByteStorageProfileBase
        {
            public static ByteStorageProfileWrite Default { get; } = new();

            private ByteStorageProfileWrite() : base(forReading: false) {}
        }

        private abstract class ByteStorageProfileBase : IConfigurationProfile
        {
            private readonly bool forReading;
            protected ByteStorageProfileBase(bool forReading)
            {
                this.forReading = forReading;
            }

            public IConfigurationContainer Get(IConfigurationContainer parameter)
            {
                // clunk city. this probably doesn't have to be this complex, surely doing something wrong.
                var typeConfiguration =
                #if CRAZYWAY
                    !forReading
                    ? parameter
                        .Type<StorageSparse<ByteEntry>>()
                        .Register()
                        .Serializer()
                        .Of(typeof(StorageSparseSerializerWrite))
                    : parameter
                        .Type<StorageSparse<ByteEntry>>()
                        .Register()
                        .Serializer()
                        .Composer()
                        .ByCalling(x => new StorageSparseSerializerRead(x));
                #else
                    parameter;
                #endif
                
                return typeConfiguration
                    .Type<StorageSparse<ByteEntry>>()
                    
                    // .Member(, bool isReadOnly, bool isSynchronized, object syncRoot)
                    .Member(x => x.IsReadOnly).Ignore()
                    .Member(x => x.IsSynchronized).Ignore()
                    .Member(x => x.SyncRoot).Ignore()

                    /*.Alter(sparse =>
                    {
                         return sparse; // NOP
                    }, sparse =>
                    {
                        return sparse; // NOP
                    })*/
                    
                    .EnableParameterizedContent() // YES!! this is good.
                    .EnableReferences()

                    .Type<StorageList<ByteEntry>>()
                    .EnableReferences();
                
                // .Alter(sparse =>
                // {
                //     return sparse; // NOP
                // }, sparse =>
                // {
                //     return sparse; // NOP
                // })
                    
                // .Register()
                // .Serializer()
                // .ByCalling((writer, instance) =>
                // {
                //     
                // }, null)
                // this one works // .WithInterceptor(StorageSparseInterceptor.Default)
                
                // .Member(x => x.Message)
                // .Register()
                // .Serializer()
                // .Of<StorageSparseSerializer2>()

                // .Register()
                // .Serializer()
                // .Using(typeof(StorageSparseSerializer2))

                //.Register()
                //.Serializer().Composer()
                //.ByCalling(x => new StorageSparseSerializer2(x))
            }

            public class StorageSparseInterceptor : SerializationActivator
            {
                public static StorageSparseInterceptor Default { get; } = new();
                
                // called when the deserialization system is about to create the new StorageSparse object
                // that it will then populate with data.
                public override object Activating(Type instanceType)
                {
                    var newStorage = new StorageSparse<ByteEntry>();
                    
                    // important: tell the storage that it's OK to allow growth beyond the internal 'count'.
                    newStorage.SetAllowExpanding(true);
                    
                    return newStorage;
                }

                // TODO: override some other methods here if needed.
                
            }
        }
        
        public abstract class MainProfile : CompositeConfigurationProfile
        {
            protected MainProfile(bool forReading) : 
                base(
                    AnnotationProfile.Default, 
                    forReading ? ByteStorageProfileRead.Default : ByteStorageProfileWrite.Default, 
                    AnnotationCollectionProfile.Default, 
                    ByteEntryProfile.Default) 
            {}
        }

        // this is clunky.
        [UsedImplicitly]
        public class MainProfileRead : MainProfile
        {
            public static MainProfileRead Default { get; } = new();
            private MainProfileRead() : base(forReading: true) {}
        }

        [UsedImplicitly]
        public class MainProfileWrite : MainProfile
        {
            public static MainProfileWrite Default { get; } = new();
            private MainProfileWrite() : base(forReading: false) {}
        }


        public static IConfigurationContainer GetConfig(bool forReading)
        {
            var container = forReading 
                ? ConfiguredContainer.New<MainProfileRead>() 
                : ConfiguredContainer.New<MainProfileWrite>();

            return container
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
//                .EnableReferences()
        }
        
        public static string Serialize(object toSerialize)
        {
            var config = GetConfig(forReading: false)
                .Create();
            
            return config.Serialize(
                new XmlWriterSettings {OmitXmlDeclaration = false, Indent = true, NewLineChars = "\r\n"},
                toSerialize);
        }
        
        public static T Deserialize<T>(string input)
        {
            var config = GetConfig(forReading: true).Create();
            return config.Deserialize<T>(input);
        }
    }
    
    // class AdornedImageSerializer : ISerializer<StorageSparse<ByteEntry>>
    // {
    //     public AdornedImage Get(IFormatReader parameter)
    //     {
    //         return new AdornedImage();
    //     }
    //
    //     public void Write(IFormatWriter writer, AdornedImage instance)
    //     {
    //
    //     }
    // }
    
    /*sealed class ActivatedSerializer : ISerializer<string>
    {
        readonly ISerializers _serializers;

        public ActivatedSerializer(ISerializers serializers) => _serializers = serializers;

        public string Get(IFormatReader parameter) => "Hello World from ActivatedSerializer";

        public void Write(IFormatWriter writer, string instance)
        {
            _serializers.Get(typeof(string))
                .Write(writer, $"Hello world! {instance}");
        }
    }*/

    internal sealed class StorageSparseSerializerWrite : ISerializer<StorageSparse<ByteEntry>>
    {
        private readonly ISerializers serializers;

        public StorageSparseSerializerWrite(ISerializers serializers)
        {
            this.serializers = serializers;
        }

        public StorageSparse<ByteEntry> Get(IFormatReader parameter)
        {
            // there's probably some way to make both read and write work in the same class....
            throw new InvalidOperationException("this serializer only supports reading");
        }

        public void Write(IFormatWriter writer, StorageSparse<ByteEntry> instance)
        {
            // TODO: I give up.... :) definitely doing this wrong.
            // these DO write into the XMl but, only as isolated elements. we need to turn them into 
            // attributes instead.
            // however, I think it'll just be easier to do this via an interface now.
            
            serializers.Get(instance.GetType()).Write(writer, instance);
            
            // actually ok but, doesn't output as attributes, so doesn't really fulfill what we need.
            // serializers.Get(typeof(int)).Write(writer, instance.Count);
            // serializers.Get(typeof(int)).Write(writer, instance.ActualCount);
            // serializers.Get(typeof(SortedDictionary<int, ByteEntry>)).Write(writer, instance.BytesDict);
        }
    }
    
    internal sealed class StorageSparseSerializerRead : ISerializer<StorageSparse<ByteEntry>>
    {
        private readonly ISerializer<StorageSparse<ByteEntry>> existingSerializer;

        public StorageSparseSerializerRead(ISerializer<StorageSparse<ByteEntry>> existingSerializer)
        {
            this.existingSerializer = existingSerializer;
        }

        public StorageSparse<ByteEntry> Get(IFormatReader parameter)
        {
            // var newStorage = new StorageSparse<ByteEntry>();
            // var t = parameter.GetType();
            // var serializer = serializers.Get(t);
            //
            // return (StorageSparse<ByteEntry>) serializer.Get(parameter);
            // var x = parameter.Get(parameter.Content());
            // var x = parameter.Content();
            var y = existingSerializer.Get(parameter);
            return y;
        }

        public void Write(IFormatWriter writer, StorageSparse<ByteEntry> instance)
        {
            // there's probably some way to make both read and write work in the same class....
            throw new InvalidOperationException("this serializer only supports reading");
        }
    }
}


// junk below



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
    
    /* old-style way of doing this
     public class ByteSparseCustomSerializer3 : IExtendedXmlCustomSerializer<StorageSparse<ByteEntry>>
        {
            public void Serializer(XmlWriter xmlWriter, StorageSparse<ByteEntry> entry)
            {
                if (entry == null)
                    return;
                
                // this.
    
                // var (key, value) = kvp;
                // xmlWriter.WriteElementString("Key", key.ToString("X"));
                // xmlWriter.WriteElementString("Value", value);
                
            }
    
            StorageSparse<ByteEntry> IExtendedXmlCustomSerializer<StorageSparse<ByteEntry>>.Deserialize(XElement xElement)
            {
                // var xElementKey = xElement.Member("Key");
                // var xElementValue = xElement.Member("Value");
                //
                // if (xElementKey == null || xElementValue == null)
                //     throw new InvalidOperationException("Invalid xml for class TestClassWithSerializer");
                //
                // var strValue = xElement.Value;
                //
                // var intValue = Util.ParseHexOrBase10String(xElementKey.Value);
                // return new KeyValuePair<int, string>(intValue, strValue);
    
                
            }
    
            // public static ByteListSerializer Default => new();
        }*/

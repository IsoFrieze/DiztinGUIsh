using System;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.Core.util;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using ExtendedXmlSerializer.ContentModel.Format;
using ExtendedXmlSerializer.ExtensionModel.Instances;
using LightInject;

namespace Diz.Core.serialization.xml_serializer;

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

            .WithDefaultMonitor(new SerializationMonitor())

            .Type<Project>()
            .Member(x => x.ProjectFileName).Ignore()

            .Type<RomBytes>()
            .Register().Serializer().Using(RomBytesSerializer.Default)

            .Type<Data>()
            .WithInterceptor(SnesDataInterceptor.Default)
            .Member(x => x.LabelsSerialization)

            .Name("Labels")
            .UseOptimizedNamespaces()
            .UseAutoFormatting()

#if DIZ_3_BRANCH
                .EnableReferences()
#endif

            .EnableImplicitTyping(typeof(Data))

            .Type<Label>()

#if DIZ_3_BRANCH
                .Name("L")
                .Member(x => x.Comment).Name("Cmt").EmitWhen(text => !string.IsNullOrEmpty(text))
                .Member(x => x.Name).Name("V").EmitWhen(text => !string.IsNullOrEmpty(text))
#endif
            .EnableImplicitTyping()

            .Type<IAnnotationLabel>()
            .WithInterceptor(AnnotationLabelInterceptor.Default);
    }

    /// <summary>
    /// Generic serialization monitor. Use this to hook into key events, debug, report progress, etc.
    /// </summary>
    private class SerializationMonitor : ISerializationMonitor
    {
        public void OnSerializing(IFormatWriter writer, object instance)
        {
                
        }

        public void OnSerialized(IFormatWriter writer, object instance)
        {
                
        }

        public void OnDeserializing(IFormatReader reader, Type instanceType)
        {
                
        }

        public void OnActivating(IFormatReader reader, Type instanceType)
        {
                
        }

        public void OnActivated(object instance)
        {
                
        }

        public void OnDeserialized(IFormatReader reader, object instance)
        {
                
        }
    }

    public abstract class GenericInterceptor<T> : ISerializationInterceptor<T>
    {
        public virtual T Serializing(IFormatWriter writer, T instance) => instance;
        public virtual T Deserialized(IFormatReader reader, T instance) => instance;
        public abstract T Activating(Type instanceType);
    }


    /// <summary>
    /// Important migration.  Label was changed to IAnnotationLabel, and existing serialized data
    /// doesn't know to create Labels when it sees IAnnotationLabel (because "exs:type" attribute is omitted).
    ///
    /// If this is hit, it means we need to manually step in and specify the type of Label, or else it'll crash.
    /// </summary>
    public sealed class AnnotationLabelInterceptor : GenericInterceptor<IAnnotationLabel>
    {
        public static AnnotationLabelInterceptor Default { get; } = new();

        // critical note:
        // activate type of Label anytime we see IAnnotationLabel.
        public override IAnnotationLabel Activating(Type instanceType) => new Label();
    }
        
    public sealed class SnesDataInterceptor : GenericInterceptor<Data>
    {
        public static SnesDataInterceptor Default { get; } = new();

        // TODO: eventually make this IData not Data
        public override Data Activating(Type instanceType) => 
            Service.Container.GetInstance<Data>();
    }
}
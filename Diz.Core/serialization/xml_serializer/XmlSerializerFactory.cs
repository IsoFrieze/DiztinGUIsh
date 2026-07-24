using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.model.snes;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using ExtendedXmlSerializer.ContentModel.Conversion;
using ExtendedXmlSerializer.ContentModel.Format;
using ExtendedXmlSerializer.ExtensionModel.Instances;
using JetBrains.Annotations;

namespace Diz.Core.serialization.xml_serializer;

public class InvalidCharStrippingConverter : IConverter<string>
{
    public string Parse(string data) => data; // Handle reading - no changes needed

    public string Format(string instance)
    {
        if (string.IsNullOrEmpty(instance)) return instance;
        
        // Remove all invalid XML 1.0 characters
        // Valid XML 1.0 characters are:
        // #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD] | [#x10000-#x10FFFF]
        var result = new StringBuilder(instance.Length);
        foreach (var c in instance.Where(IsValidXmlChar)) {
            result.Append(c);
        }
        
        return result.ToString();
    }
    
    private static bool IsValidXmlChar(char c)
    {
        return c == 0x09 ||          // Tab
               c == 0x0A ||          // Line Feed
               c == 0x0D ||          // Carriage Return
               (c >= 0x20 && c <= 0xD7FF) ||      // Basic Multilingual Plane
               (c >= 0xE000 && c <= 0xFFFD);      // Private Use Area and others
    }

    public bool IsSatisfiedBy(TypeInfo parameter)
    {
        return parameter.AsType() == typeof(string);
    }
}

public class XmlSerializerFactory(
    IDataFactory dataFactory,
    Func<IDataFactory, XmlSerializerFactory.SnesDataInterceptor> snesDataInterceptor)
    : IXmlSerializerFactory
{
    public IConfigurationContainer GetSerializer([CanBeNull] RomBytesOutputFormatSettings romBytesOutputFormat)
    {
        var romBytesSerializer = new RomBytesSerializer
        {
            FormatSettings = romBytesOutputFormat
        };

        return new ConfigurationContainer()

            .WithDefaultMonitor(new SerializationMonitor())

            .Type<Project>()
            .Member(x => x.ProjectUserSettings).Ignore()
            .Member(x=>x.InternalRomGameName).Register(new InvalidCharStrippingConverter())
            

            .Type<RomBytes>()
            .Register().Serializer().Using(romBytesSerializer)
        
            .Type<Data>()
            .WithInterceptor(snesDataInterceptor(dataFactory))
            .Member(x => x.LabelsSerialization)
            .Name("Labels")
        
            // regions are the single most numerous element in a large project file, and every one of
            // them repeats its attribute names. short XML names keep the file size (and load time)
            // down. these names ONLY affect the on-disk XML -- the C# property names are unchanged.
            .Type<Region>()
            .Member(x => x.StartSnesAddress).Name("S")
            .Member(x => x.EndSnesAddress).Name("E")
            .Member(x => x.RegionName).Name("Id")
            .Member(x => x.ContextToApply).Name("Ctx")
            .Member(x => x.Priority).Name("Pri")
            .Member(x => x.ExportSeparateFile).Name("SepFile")
            .Member(x => x.ExportType).Name("Type")
            .Member(x => x.AssetType).Name("AType")
            .Member(x => x.AssetVersion).Name("AVer")
            .Member(x => x.AssetName).Name("AName")
            .Member(x => x.AssetOptions).Name("AOpts")

            .EnableImplicitTyping(typeof(ContextMapping))

            .Type<Label>()
            .EnableImplicitTyping()

            // Author/Confidence auto-serialize as attributes via implicit typing. Unlike the older
            // Name/Comment members (which emit even when empty), only emit these when actually set,
            // so the vast majority of labels -- which never get annotated -- don't each grow an
            // Author="" Confidence="" pair. Old files without these attrs load to the defaults.
            // Confidence is the free-form level string; the empty-string default is omitted.
            // (Level names like "Medium"/"VeryHigh" stored by older files load unchanged.)
            // The short on-disk attribute names ("By"/"Cf") keep the file size down (same rationale
            // as the Region short names above); the C# property names (Author/Confidence) are unchanged.
            .Type<Label>()
            .Member(x => x.Author).EmitWhen(author => !string.IsNullOrEmpty(author)).Name("By")
            .Member(x => x.Confidence).EmitWhen(confidence => !string.IsNullOrEmpty(confidence)).Name("Cf")

            .Type<IAnnotationLabel>()
            .WithInterceptor(AnnotationLabelInterceptor.Default)

            .UseOptimizedNamespaces()
            .UseAutoFormatting();
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
        private readonly IDataFactory dataFactory;
        public SnesDataInterceptor(IDataFactory dataFactory)
        {
            this.dataFactory = dataFactory;
        }

        // TODO: eventually make this IData not Data
        public override Data Activating(Type instanceType) =>
            dataFactory.Create();
    }
}
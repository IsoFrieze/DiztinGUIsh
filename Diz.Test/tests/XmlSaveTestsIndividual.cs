using System;
using System.Xml;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using Xunit;

namespace Diz.Test.tests
{
    public class XmlTestSerializationSupport
    {
        /*public class ByteEntrySerializer : IExtendedXmlCustomSerializer<ByteEntry>, IExtendedXmlCustomSerializer
        {
            public void Serializer(XmlWriter xmlWriter, ByteEntry entry)
            {
                if (entry == null)
                    return;
                
                this.

                // var (key, value) = kvp;
                // xmlWriter.WriteElementString("Key", key.ToString("X"));
                // xmlWriter.WriteElementString("Value", value);
            }

            ByteEntry IExtendedXmlCustomSerializer<ByteEntry>.Deserialize(XElement xElement)
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

            public void Serializer(XmlWriter xmlWriter, object instance)
            {
                Serializer(xmlWriter, (ByteEntry) instance);
            }

            public object Deserialize(XElement xElement)
            {
                
            }
        }*/
        
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
        
        public static IConfigurationContainer GetByteEntrySerializer()
        {
            return new ConfigurationContainer()
                // these are all helpers and we shouldn't serialize them
                .Type<ByteEntry>()
                .EnableReferences()
                // .Register().Converter(ByteEntryConverter.Default)
                // .Member(x => x.Arch).Ignore()
                // .Member(x => x.Byte).Ignore()
                // .Member(x => x.Point).Ignore()
                // .Member(x => x.DataBank).Ignore()
                // .Member(x => x.DirectPage).Ignore()
                // .Member(x => x.MFlag).Ignore()
                // .Member(x => x.XFlag).Ignore()
                // .Member(x => x.TypeFlag).Ignore()
                // .Member(x => x.DontSetParentOnCollectionItems).Ignore()

                .UseOptimizedNamespaces()
                .UseAutoFormatting();

            // .EnableImplicitTyping(typeof(Label));
        }

        public sealed class ByteEntryProfile : IConfigurationProfile
        {
            public static ByteEntryProfile Default { get; } = new();

            private ByteEntryProfile() {}

            public IConfigurationContainer Get(IConfigurationContainer parameter)
                => parameter.Type<ByteEntry>()
                    .Name("Byte")
                    .Member(x => x.Arch).Ignore()
                    .Member(x => x.Byte).Ignore()
                    .Member(x => x.Point).Ignore()
                    .Member(x => x.DataBank).Ignore()
                    .Member(x => x.DirectPage).Ignore()
                    .Member(x => x.MFlag).Ignore()
                    .Member(x => x.XFlag).Ignore()
                    .Member(x => x.TypeFlag).Ignore()
                    .Member(x => x.DontSetParentOnCollectionItems).Ignore()
                    .EnableReferences()
                    .UseOptimizedNamespaces()
                    .UseAutoFormatting();

            // .Register().Converter(ByteEntryConverter.Default)
        }
        
        public sealed class AnnotationCollectionProfile : IConfigurationProfile
        {
            public static AnnotationCollectionProfile Default { get; } = new();

            private AnnotationCollectionProfile() {}

            public IConfigurationContainer Get(IConfigurationContainer parameter)
                => parameter.Type<AnnotationCollection>()
                    .UseOptimizedNamespaces()
                    .UseAutoFormatting();
        }
        
        public sealed class MainProfile : CompositeConfigurationProfile
        {
            public static MainProfile Default { get; } = new();

            MainProfile() : 
                base(AnnotationCollectionProfile.Default, ByteEntryProfile.Default) {}
        }
    }
    
    public class XmlSaveTestsIndividual
    {
        private static ByteEntry CreateSampleEntry()
        {
            return new()
            {
                Byte = 0xCA, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100,
                Annotations = {new Label {Name = "SomeLabel"}, new Comment {Text = "This is a comment"}}
            };
        }

        [Fact]
        public void TestByteEntryEquality()
        {
            var entry2 = CreateSampleEntry();
            
            void AssertNotEqualWhenChanged(Action<ByteEntry> changeSomething)
            {
                var byteEntry1 = CreateSampleEntry();
                changeSomething(byteEntry1);
                Assert.NotEqual(entry2, byteEntry1);
            }

            var entry1 = CreateSampleEntry();
            Assert.Equal(entry1, entry2);

            AssertNotEqualWhenChanged(entry => entry.Byte = 0xFF);
            AssertNotEqualWhenChanged(entry => entry.Arch = Architecture.Apuspc700);
            AssertNotEqualWhenChanged(entry => entry.Point = InOutPoint.ReadPoint);
            AssertNotEqualWhenChanged(entry => entry.DataBank = 43);
            AssertNotEqualWhenChanged(entry => entry.DirectPage = 222);
            AssertNotEqualWhenChanged(entry => entry.Annotations.Clear());
            AssertNotEqualWhenChanged(entry => entry.MFlag = false);
            AssertNotEqualWhenChanged(entry => entry.XFlag = true);
            AssertNotEqualWhenChanged(entry => entry.TypeFlag = FlagType.Music);
            AssertNotEqualWhenChanged(entry => entry.RemoveOneAnnotationIfExists<Comment>());
            AssertNotEqualWhenChanged(entry => entry.RemoveOneAnnotationIfExists<Label>());
        }
        
        [Fact]
        public void TestSerializeAnnotationCollection()
        {
            var entry = CreateSampleEntry();
            var serialized = Serialize(entry.Annotations);
        }

        private static string Serialize(object toSerialize)
        {
            var container = ConfiguredContainer.New<XmlTestSerializationSupport.MainProfile>();

            var serializer = container
                .EnableImplicitTyping(typeof(ByteEntry))
                .EnableImplicitTyping(typeof(ByteSource))
                .EnableImplicitTyping(typeof(ByteStorage))
                .EnableImplicitTyping(typeof(ByteList))
                .EnableImplicitTyping(typeof(SparseByteStorage))
                .EnableImplicitTyping(typeof(AnnotationCollection))
                .EnableImplicitTyping(typeof(Label))
                .EnableImplicitTyping(typeof(Comment))
                .EnableImplicitTyping(typeof(BranchAnnotation))
                .EnableImplicitTyping(typeof(ByteAnnotation))
                .EnableImplicitTyping(typeof(MarkAnnotation))
                .EnableImplicitTyping(typeof(OpcodeAnnotation))
                .UseOptimizedNamespaces()
                .UseAutoFormatting()
                .Create();
            
            return serializer.Serialize(
                new XmlWriterSettings {OmitXmlDeclaration = false, Indent = true, NewLineChars = "\r\n"},
                toSerialize);
        }


        [Fact]
        public void TestSerializeEmptyByteEntry()
        {
            var entry = CreateSampleEntry();
            var serialized = Serialize(entry);
            int x = 3;
        }
    }
}
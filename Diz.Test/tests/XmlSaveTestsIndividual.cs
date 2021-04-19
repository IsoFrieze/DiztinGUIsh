using System;
using System.Xml;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using Xunit;
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace Diz.Test.tests
{
    public class XmlTestSerializationSupport
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

        public sealed class ByteEntryProfile : IConfigurationProfile
        {
            public static ByteEntryProfile Default { get; } = new();

            private ByteEntryProfile() {}

            public IConfigurationContainer Get(IConfigurationContainer parameter)
                => parameter
                    .Type<ByteEntry>()
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
        }
        
        public sealed class AnnotationCollectionProfile : IConfigurationProfile
        {
            public static AnnotationCollectionProfile Default { get; } = new();

            private AnnotationCollectionProfile() {}

            public IConfigurationContainer Get(IConfigurationContainer parameter)
                => parameter.Type<AnnotationCollection>()
                    .Member(x=>x.DontSetParentOnCollectionItems).Ignore()
                    .Member(x=>x.EnforcePolicy).Ignore()
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
            var config = CreateConfig();
            
            return config.Serialize(
                new XmlWriterSettings {OmitXmlDeclaration = false, Indent = true, NewLineChars = "\r\n"},
                toSerialize);
        }
        
        private static T Deserialize<T>(string input)
        {
            var config = CreateConfig();
            return config.Deserialize<T>(input);
        }

        private static IExtendedXmlSerializer CreateConfig()
        {
            var container = ConfiguredContainer.New<XmlTestSerializationSupport.MainProfile>();
            
            return container
                .UseOptimizedNamespaces()
                .UseAutoFormatting()
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
                .Type<ByteSource>()
                .EnableReferences()
                .Type<ByteStorage>()
                .EnableReferences()
                .Type<ByteList>()
                .EnableReferences()
                .Type<SparseByteStorage>()
                .EnableReferences()
                .Type<RegionMapping>()
                .EnableReferences()
                .Type<ByteSourceMapping>()
                .EnableReferences()
                .Create();
        }

        public void XmlFullCycle<T>(T objToCycle, T expectedEqual)
        {
            var xmlToCycle = Serialize(objToCycle);
            var deserialized = Deserialize<T>(xmlToCycle);
            
            Assert.Equal(expectedEqual, deserialized);
        }


        public void XmlFullCycleTwoCopies(Func<object> createFn)
        {
            var obj1 = createFn(); 
            var objCopy = createFn();
            XmlFullCycle(obj1, objCopy);
        }

        public static TheoryData<Func<object>> SimpleCycleObjects => new() {
            () => new MarkAnnotation {TypeFlag = FlagType.Graphics},
            () => new AnnotationCollection {
                new MarkAnnotation {TypeFlag = FlagType.Graphics}, new Comment {Text = "asdf"}
            },
            () => new ByteEntryBase(),
        };
        
        public static TheoryData<Func<object>> MoreComplexCycleObjects => new() {
            () => SampleRomCreator1.CreateBaseRom().RomByteSource.Bytes,
            () => SampleRomCreator1.CreateBaseRom().RomByteSource,
            () => SampleRomCreator1.CreateBaseRom().SnesAddressSpace,
        };

        [Theory]
        [MemberData(nameof(SimpleCycleObjects))]
        public void TestCycleObjsSimple(Func<object> createFn) => XmlFullCycleTwoCopies(createFn);
        
        [Theory(Skip="not quite yet working")]
        [MemberData(nameof(MoreComplexCycleObjects))]
        public void TestCycleObjsComplex(Func<object> createFn) => XmlFullCycleTwoCopies(createFn);
    }
}
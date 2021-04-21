using System;
using System.Collections.Generic;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Test.Utils;
using ExtendedXmlSerializer.ContentModel.Format;
using ExtendedXmlSerializer.ExtensionModel.Instances;
using Xunit;
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace Diz.Test.tests
{
    public class XmlSaveTestsIndividual
    {
        private static ByteEntry CreateSampleEntry()
        {
            return new()
            {
                Byte = 0xCA, MFlag = true, DataBank = 0x80, DirectPage = 0x2100,
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
        
        // public class ByteListInterceptor : SerializationInterceptor<ByteList>
        public class ByteListInterceptor : SerializationActivator
        {
            // ByteList ISerializationInterceptor<ByteList>.Activating(Type instanceType)
            // {
            //     // processor should be retrieved from IoC container, but created manually for simplicity of test
            //     // var processor = new ByteList(new Service());
            //     return new ByteList();
            // }
            public override object Activating(Type instanceType)
            {
                return new StorageList<ByteEntry>();
            }
            
            public override object Serializing(IFormatWriter writer, object instance)
            {
                return base.Serializing(writer, instance);
            }

            public override object Deserialized(IFormatReader reader, object instance)
            {
                return base.Deserialized(reader, instance);
            }
        }

        public static TheoryData<Func<object>> SimpleCycleObjects => new() {
            () => new MarkAnnotation {TypeFlag = FlagType.Graphics},
            () => new AnnotationCollection {
                new MarkAnnotation {TypeFlag = FlagType.Graphics}, new Comment {Text = "asdf"}
            },
            () => new ByteEntry(),
        };

        public static TheoryData<Func<object>> MoreComplexCycleObjects => new() {
            () => new ByteSource(),
            () => SampleRomCreator1.CreateBaseRom().RomByteSource.Bytes[0],
            () => SampleRomCreator1.CreateBaseRom().RomByteSource,
            // () => SampleRomCreator1.CreateBaseRom().SnesAddressSpace, // next up, not working yet.
        };
        
        public static StorageList<ByteEntry> CreateSampleByteList()
        {
            var sample2 = CreateSampleEntry();
            sample2.Annotations.Add(new BranchAnnotation {Point = InOutPoint.OutPoint});
            sample2.Annotations.Add(new MarkAnnotation {TypeFlag = FlagType.Opcode});

            return new StorageList<ByteEntry>(new List<ByteEntry> {CreateSampleEntry(), sample2});
        }

        public static StorageList<ByteEntry> CreateSampleEmptyByteList()
        {
            return new();
        }

        [Fact]
        public void XmlFullCycleEmptyByteStorage()
        {
            XmlTestUtils.RunFullCycle(CreateSampleEmptyByteList, out var unchanged, out var cycled);
            Assert.Equal(unchanged, cycled);
        }
        

        [Theory]
        [MemberData(nameof(SimpleCycleObjects))]
        [MemberData(nameof(MoreComplexCycleObjects))]
        public void XmlFullCycleTwoCopies(Func<object> createFn)
        {
            XmlTestUtils.RunFullCycle(createFn, out var unchanged, out var cycled);
            Assert.Equal(unchanged, cycled);
        }

        [Fact]
        public void XmlFullCycleByteStorage()
        {
            XmlTestUtils.RunFullCycle(CreateSampleByteList, out var unchanged, out var cycled);
            
            Assert.Equal(unchanged.Count, cycled.Count);
            Assert.Equal(unchanged, cycled);
        }
    }
}
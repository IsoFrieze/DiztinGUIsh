using System;
using System.Collections.Generic;
using System.Xml;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.serialization.xml_serializer;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
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

        private static T XmlFullCycle<T>(T objToCycle)
        {
            var xmlToCycle = XmlSerializationSupportNew.Serialize(objToCycle);
            var deserialized = XmlSerializationSupportNew.Deserialize<T>(xmlToCycle);
            return deserialized;
        }

        public static TheoryData<Func<object>> SimpleCycleObjects => new() {
            () => new MarkAnnotation {TypeFlag = FlagType.Graphics},
            () => new AnnotationCollection {
                new MarkAnnotation {TypeFlag = FlagType.Graphics}, new Comment {Text = "asdf"}
            },
            () => new ByteEntryBase(),
        };

        public static TheoryData<Func<object>> MoreComplexCycleObjects => new() {
            () => new ByteSource(),
            () => SampleRomCreator1.CreateBaseRom().RomByteSource.Bytes[0],
            () => SampleRomCreator1.CreateBaseRom().RomByteSource,
            () => SampleRomCreator1.CreateBaseRom().SnesAddressSpace,
        };

        public static ByteList CreateSampleByteList()
        {
            var sample2 = CreateSampleEntry();
            sample2.DataBank = 95;
            sample2.Point = InOutPoint.ReadPoint;

            return new ByteList(new List<ByteEntry> {CreateSampleEntry(), sample2});
        }
        
        [Fact]
        public void XmlFullCycleByteStorage()
        {
            RunFullCycle(CreateSampleByteList, out var unchanged, out var cycled);
            
            Assert.Equal(unchanged.Count, cycled.Count);
            Assert.Equal(unchanged, cycled);
        }

        [Theory]
        [MemberData(nameof(SimpleCycleObjects))]
        [MemberData(nameof(MoreComplexCycleObjects))]
        public void XmlFullCycleTwoCopies(Func<object> createFn)
        {
            RunFullCycle(createFn, out var unchanged, out var cycled);
            Assert.Equal(unchanged, cycled);
        }

        private static void RunFullCycle<T>(Func<T> createFn, out T expectedCopy, out T deserializedObj)
        {
            RunFullCycleObj(() => createFn(), out var expectedObjCopy, out var deserializedObjCopy);

            expectedCopy = (T)expectedObjCopy;
            deserializedObj = (T) deserializedObjCopy;
        }

        private static void RunFullCycleObj(Func<object> createFn, out object expectedCopy, out object deserializedObj)
        {
            var objToCycle = createFn();
            expectedCopy = createFn();
            
            deserializedObj = XmlFullCycle(objToCycle);
        }
    }
}
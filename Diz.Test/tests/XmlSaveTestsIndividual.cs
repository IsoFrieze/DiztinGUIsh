using System;
using System.Collections.Generic;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Test.Utils;
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
            
            // tmp disabled. this WORKS technically but, at current, we generate a gigantic XML file for it so it takes forever
            // what we need to do instead is to modify the serialization to output the sparse version, then this is fine.
            // () => SampleRomCreator1.CreateBaseRom().SnesAddressSpace,
        };
        
        public static StorageList<ByteEntry> CreateSampleByteList()
        {
            var sample2 = CreateSampleEntry();
            sample2.Annotations.Add(new BranchAnnotation {Point = InOutPoint.OutPoint});
            sample2.Annotations.Add(new MarkAnnotation {TypeFlag = FlagType.Opcode});

            return new StorageList<ByteEntry>(new List<ByteEntry> {CreateSampleEntry(), sample2});
        }

        public static StorageList<ByteEntry> CreateSampleEmptyByteList() => new();
        public static StorageSparse<ByteEntry> CreateSampleEmptyByteSparse() => new();

        [Fact]
        public void XmlFullCycleEmptyByteStorage()
        {
            XmlTestUtils.RunFullCycle(CreateSampleEmptyByteList, out var unchanged, out var cycled);
            Assert.Equal(unchanged, cycled);
        }


        [Theory]
        [MemberData(nameof(SimpleCycleObjects))]
        public void XmlFullCycleTwoCopiesSimple(Func<object> createFn) => RunFullCycle(createFn);
        
        [Theory]
        [MemberData(nameof(MoreComplexCycleObjects))]
        public void XmlFullCycleTwoCopiesComplex(Func<object> createFn) => RunFullCycle(createFn);

        private static void RunFullCycle(Func<object> createFn)
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
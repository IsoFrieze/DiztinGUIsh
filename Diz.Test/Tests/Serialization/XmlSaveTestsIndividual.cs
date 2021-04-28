using System;
using System.Collections.Generic;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Test.TestData;
using Diz.Test.Utils;
using Xunit;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace Diz.Test.Tests.Serialization
{
    public static class XmlSampleData
    {
        public static Core.model.byteSources.ByteEntry CreateSampleEntry()
        {
            return new()
            {
                Byte = 0xCA, MFlag = true, DataBank = 0x80, DirectPage = 0x2100,
                Annotations = {new Label {Name = "SomeLabel"}, new Comment {Text = "This is a comment"}}
            };
        }

        public static StorageList<Core.model.byteSources.ByteEntry> CreateSampleByteList()
        {
            var sample2 = CreateSampleEntry();
            sample2.Annotations.Add(new BranchAnnotation {Point = InOutPoint.OutPoint});
            sample2.Annotations.Add(new MarkAnnotation {TypeFlag = FlagType.Opcode});

            return new StorageList<Core.model.byteSources.ByteEntry>(new List<Core.model.byteSources.ByteEntry>
                {CreateSampleEntry(), sample2});
        }

        public static StorageList<Core.model.byteSources.ByteEntry> CreateSampleEmptyByteList() => new();
        public static StorageSparse<Core.model.byteSources.ByteEntry> CreateSampleEmptyByteSparse() => new();
    }
    
    public class XmlSaveTestsIndividual
    {
        
        public static TheoryData<Func<object>> SimpleCycleObjects => new()
        {
            () => new MarkAnnotation {TypeFlag = FlagType.Graphics},
            () => new Core.model.byteSources.AnnotationCollection
            {
                new MarkAnnotation {TypeFlag = FlagType.Graphics}, new Comment {Text = "asdf"}
            },
            () => new Core.model.byteSources.ByteEntry(),
        };

        public static TheoryData<Func<object>> MoreComplexCycleObjects => new()
        {
            () => new Core.model.byteSources.ByteSource(),
            () => TinyHiRomCreator.CreateBaseRom().RomByteSource.Bytes[0],
            () => TinyHiRomCreator.CreateBaseRom().RomByteSource,

            // tmp disabled. this WORKS technically but, at current, we generate a gigantic XML file for it so it takes forever
            // what we need to do instead is to modify the serialization to output the sparse version, then this is fine.
            // () => SampleRomCreator1.CreateBaseRom().SnesAddressSpace,
        };
        
        [Fact]
        public void XmlFullCycleEmptyByteStorage()
        {
            XmlTestUtils.RunFullCycle(XmlSampleData.CreateSampleEmptyByteList, out var unchanged, out var cycled);
            Assert.Equal(unchanged, cycled);
        }

        [Theory]
        [MemberData(nameof(SimpleCycleObjects))]
        public void XmlFullCycleTwoCopiesSimple(Func<object> createFn) => XmlTestUtils.RunFullCycle(createFn);
        
        [Theory]
        [MemberData(nameof(MoreComplexCycleObjects))]
        public void XmlFullCycleTwoCopiesComplex(Func<object> createFn) => XmlTestUtils.RunFullCycle(createFn);

        [Fact]
        public void XmlFullCycleByteStorage()
        {
            XmlTestUtils.RunFullCycle(XmlSampleData.CreateSampleByteList, out var unchanged, out var cycled);
            
            Assert.Equal(unchanged.Count, cycled.Count);
            Assert.Equal(unchanged, cycled);
        }
    }
}
using System;
using System.Collections.Generic;
using Diz.Core.export;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.util;
using Diz.Test.TestData;
using Diz.Test.Utils;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace Diz.Test.Tests.SerializationTests
{
    public class XmlSampleData
    {
        public static ByteEntry CreateSampleEntry()
        {
            return new()
            {
                Byte = 0xCA, MFlag = true, DataBank = 0x80, DirectPage = 0x2100,
                Annotations = {new Label {Name = "SomeLabel"}, new Comment {Text = "This is a comment"}}
            };
        }

        public static StorageList<ByteEntry> CreateSampleByteList()
        {
            var sample2 = CreateSampleEntry();
            sample2.Annotations.Add(new BranchAnnotation {Point = InOutPoint.OutPoint});
            sample2.Annotations.Add(new MarkAnnotation {TypeFlag = FlagType.Opcode});

            return new StorageList<ByteEntry>(new List<ByteEntry>
                {CreateSampleEntry(), sample2});
        }

        public static StorageList<ByteEntry> CreateSampleEmptyByteList() => new();
        public static StorageSparse<ByteEntry> CreateSampleEmptyByteSparse() => new();
    }
    
    public class XmlSaveTestsIndividual : XmlTestUtilBase
    {
        
        public static TheoryData<Func<object>> SimpleCycleObjects => new()
        {
            () => new MarkAnnotation {TypeFlag = FlagType.Graphics},
            () => new AnnotationCollection
            {
                new MarkAnnotation {TypeFlag = FlagType.Graphics}, new Comment {Text = "asdf"}
            },
            () => new ByteEntry(),
        };

        public static TheoryData<Func<object>> MoreComplexCycleObjects => new()
        {
            () => new ByteSource(),
            () => TinyHiRomCreator.CreateBaseRom().RomByteSource.Bytes[0],
            () => TinyHiRomCreator.CreateBaseRom().RomByteSource,
            () => TinyHiRomCreator.CreateBaseRom().SnesAddressSpace,
            TinyHiRomCreator.CreateBaseRom,
        };
        
        public static TheoryData<Func<object>> ThisIsJustGettingRidiculousNow => new()
        {
            () => SampleRomData.CreateSampleData().Data
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
        
        [Theory]
        [MemberData(nameof(ThisIsJustGettingRidiculousNow))]
        public void XmlFullCycleTwoCopiesMadness(Func<object> createFn) => XmlTestUtils.RunFullCycle(createFn);

        [Fact]
        public void XmlFullCycleByteStorage()
        {
            XmlTestUtils.RunFullCycle(XmlSampleData.CreateSampleByteList, out var unchanged, out var cycled);
            
            Assert.Equal(unchanged.Count, cycled.Count);
            Assert.Equal(unchanged, cycled);
        }

        [Fact]
        private void XmlFullCycleAnnotationCollectionSimple2()
        {
            Func<AnnotationCollection> fnCreate = () => new AnnotationCollection
            {
                new MarkAnnotation {TypeFlag = FlagType.Graphics}, new Comment {Text = "asdf"}
            };

            {
                var ac1 = fnCreate();
                var ac2 = fnCreate();
                Assert.Equal(ac1, ac2);
                ac1.Should().Equal(ac2);
            }

            {
                var ac1 = new AnnotationCollection();
                var ac2 = new AnnotationCollection();
                ((object) ac1).Should().Be(ac2);
            }
        }
        
        [Fact]
        public void XmlFullCycleProject()
        {
            XmlTestUtils.RunFullCycle(CreateMostlyEmptyProject, out var unchanged, out var cycled);
            cycled.Should().Be(unchanged);

            unchanged?.Session?.UnsavedChanges
                .Should().BeTrue("we set it that way");

            cycled?.Session?.UnsavedChanges
                .Should().BeFalse("we marked this as ignored for XML serialization");
        }

        private static Project CreateMostlyEmptyProject()
        {
            var project = new Project
            {
                Data = null, // let's test everything except Data
                AttachedRomFilename = "dac.smc",
                InternalCheckSum = 0x4242FFFF,
                InternalRomGameName = "Cyber Space Bros 2",
                LogWriterSettings = new LogWriterSettings(),
            };

            project.Session = new ProjectSession(project)
            {
                UnsavedChanges = true,
                ProjectFileName = "sega_genesis___wait_hang_on.diz",
            };
            
            return project;
        }

        public XmlSaveTestsIndividual(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }
    }
}
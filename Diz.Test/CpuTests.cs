using System.Collections.Generic;
using Diz.Core.export;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.model.snes;
using Diz.Core.util;
using Diz.Test.Utils;
using Xunit;
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace Diz.Test
{
    public static class SampleRomCreator1
    {
        public static List<ByteEntry> CreateByteOffsetData() =>
            new()
            {
                // starts at PC=0, which is SNES=0xC00000

                // STA.W SNES_VMADDL
                // OR (equivalent)
                // STA.W $2116
                new()
                {
                    Byte = 0x8D, Annotations = new AnnotationCollection
                    {
                        new OpcodeAnnotation {MFlag = true, XFlag = true, DataBank = 0x00, DirectPage = 0},
                        new MarkAnnotation {TypeFlag = FlagType.Opcode}
                    }
                },
                new()
                {
                    Byte = 0x16, Annotations = new AnnotationCollection
                    {
                        new MarkAnnotation {TypeFlag = FlagType.Operand},
                        new Comment {Text = "unused"} // 0xC00001
                    }
                },
                new()
                {
                    Byte = 0x21, Annotations = new AnnotationCollection
                    {
                        new MarkAnnotation {TypeFlag = FlagType.Operand}
                    }
                },
            };

        public static Data CreateSampleRomByteSource(IReadOnlyCollection<ByteEntry> srcData)
        {
            var romByteSource = new ByteSource
            {
                Name = "Snes Rom",
                Bytes = new ByteList(srcData)
            };

            var data = new Data();
            data.PopulateFromRom(romByteSource,RomMapMode.HiRom, RomSpeed.FastRom);
            return data;
        }

        public static (List<ByteEntry>, Data) CreateSampleRomByteSourceElements()
        {
            var byteOffsetData = CreateByteOffsetData();
            return (byteOffsetData, CreateSampleRomByteSource(byteOffsetData));
        }
        
        public static Data CreateBaseRom()
        {
            var (_, newData) = CreateSampleRomByteSourceElements();
            return newData;
        }
    }
    
    public static class CpuTests
    {
        private static Data TinyHiRom => SampleRomCreator1.CreateBaseRom();

        private static Data TinyHiRomWithExtraLabel
        {
            get
            {
                var data = SampleRomCreator1.CreateBaseRom();

                data.Labels.AddLabel(
                    0x002116, new Label {Name = "SNES_VMADDL", Comment = "SNES hardware register example."}
                );

                return data;
            }
        }

        public static TheoryData<AssemblyPipelineTester> PipelineTesters => new()
        {
            AssemblyPipelineTester.SetupFromResource(TinyHiRomWithExtraLabel, "Diz.Test/Resources/asartestrun.asm")
        };

        /*public static IReadOnlyList<byte> AssemblyRom => AsarRunner.AssembleToRom(@"
            hirom

            SNES_VMADDL = $002116
            ; SNES_VMADDL = $7E2116

            ORG $C00000

            STA.W SNES_VMADDL"
        );*/

        [Fact]
        public static void ConvertSnesToPcHiRom()
        {
            var romSize = RomUtil.GetBankSize(RomMapMode.HiRom) * 64;
            Assert.Equal(-1, RomUtil.ConvertSnesToPc(0x202000, RomMapMode.HiRom, romSize));
            Assert.Equal(0x01FFFF, RomUtil.ConvertSnesToPc(0x41FFFF, RomMapMode.HiRom, romSize));
            Assert.Equal(0x000123, RomUtil.ConvertSnesToPc(0xC00123, RomMapMode.HiRom, romSize));
            Assert.Equal(0x3F0123, RomUtil.ConvertSnesToPc(0xFF0123, RomMapMode.HiRom, romSize));
            Assert.Equal(-1, RomUtil.ConvertSnesToPc(0x10000000, RomMapMode.HiRom, romSize));
        }

        [Fact]
        public static void ConvertSnesToPcLoRom()
        {
            var romSize = RomUtil.GetBankSize(RomMapMode.LoRom) * 8;
            Assert.Equal(-1, RomUtil.ConvertSnesToPc(0x790000, RomMapMode.LoRom, romSize));
            Assert.Equal(0x00, RomUtil.ConvertSnesToPc(0x808000, RomMapMode.LoRom, romSize));
        }

        [Fact]
        public static void DataConvertSnesToPcHiRom()
        {
            var data = TinyHiRomWithExtraLabel;

            // note: this doesn't quite cover all the range if the offset is greater than the #bytes

            Assert.Equal(0x000002, data.ConvertSnesToPc(0xC00002));
            Assert.Equal(0xC00000, data.ConvertPCtoSnes(0x000000));
        }
        
        [Fact]
        public static void SanityTestSizingBase()
        {
            SizeCheck(TinyHiRom);
        }
        
        [Fact]
        public static void SanityTestSizing()
        {
            SizeCheck(TinyHiRomWithExtraLabel);
        }

        private static void SizeCheck(Data data)
        {
            Assert.Equal(3, data.GetRomSize());
            Assert.Equal(3, data.RomByteSource.Bytes.Count);

            Assert.Equal(0xFFFFFF, data.SnesAddressSpace.Bytes.Count);
        }

        [Fact]
        public static void TestAccess()
        {
            var data = TinyHiRom;
            Assert.Equal(3, data.GetRomSize());

            AssertRomByteEqual(0x8D, data, 0);
            AssertRomByteEqual(0x16, data, 1);
            AssertRomByteEqual(0x21, data, 2);
        }

        [Fact]
        public static void TestParentsSetupCorrectly()
        {
            var data = TinyHiRom;
            
            data.SnesAddressSpace.Bytes[0xC00000] = new ByteEntry
            {
                Byte = 0xEE,
            };
            
            Test2(0xEE, 0xC00000, data.SnesAddressSpace);
            Test2(0x8D, 0x0, data.RomByteSource);
        }

        private static void Test2(int expectedByteVal, int index, ByteSource expectedByteSource)
        {
            var byteOffsetData = expectedByteSource.Bytes[index];
            var b = byteOffsetData?.Byte;

            Assert.NotNull(b);
            Assert.NotNull(byteOffsetData);
            
            Assert.Equal(expectedByteVal, b.Value);
            Assert.Same(expectedByteSource, byteOffsetData.Container);
        }

        [Fact]
        public static void BuildBasicGraph()
        {
            var (srcData, data) = SampleRomCreator1.CreateSampleRomByteSourceElements();
            
            var snesAddress = data.ConvertPCtoSnes(0);
            var graph = ByteGraphUtil.BuildFullGraph(data.SnesAddressSpace, snesAddress);

            // ok, this is tricky, pay careful attention.
            // we got a graph back from the SNES address space that represents
            // stored in each of the 2 layers:
            // layer 1: the SNES address space
            // layer 2: the ROM
            //
            // we're using Sparse byte storage, which means that unless something needs to be stored
            // in the SNES address space (and NOT with the ROM), then that entry will be null.
            //
            // what we expect is this resulting graph:
            // - root node: ByteOffsetData from SNES address space @ offset 0xC00000.
            //               THIS *should be NULL* because there's nothing stored there.
            //   - child node 1: A ByteOffsetData from the ROM. this WILL have data because we loaded a ROM.
            //
            // remember, this is showing a graph of the underlying data, and not flattened into something useful for
            // looking at it as a condensed, flat view.
            
            Assert.NotNull(graph);
            Assert.Null(graph.ByteData);        // snes address space result
            
            Assert.NotNull(graph.Children);     // 1 child = the ROM ByteSource
            Assert.Single(graph.Children);

            var childNodeFromRom = graph.Children[0];   // get the node that represents the
                                                        // next (and only) layer down, the ROM
            Assert.NotNull(childNodeFromRom);
            Assert.Null(childNodeFromRom.Children);
            Assert.NotNull(childNodeFromRom.ByteData);
            Assert.NotNull(childNodeFromRom.ByteData.Byte);
            Assert.Equal(0x8D, childNodeFromRom.ByteData.Byte.Value);
            
            Assert.Same(data.RomByteSource, childNodeFromRom.ByteData.Container);
            Assert.Same(srcData[0], childNodeFromRom.ByteData);
        }
        
         [Fact]
        public static void TraverseChildren()
        {
            var (srcData, data) = SampleRomCreator1.CreateSampleRomByteSourceElements();
            
            var snesAddress = data.ConvertPCtoSnes(0);
            var graph = ByteGraphUtil.BuildFullGraph(data.SnesAddressSpace, snesAddress);

            var flattenedNode = ByteGraphUtil.BuildFlatDataFrom(graph);
            
            Assert.NotNull(flattenedNode);
            Assert.NotNull(flattenedNode.Byte);
            Assert.Equal(0x8D, flattenedNode.Byte.Value);
            Assert.Equal(2, flattenedNode.Annotations.Count);
        }

        private static void AssertRomByteEqual(byte expectedByteVal, Data data, int pcOffset)
        {
            // test via all three access methods
            AssertSnesByteIs(expectedByteVal, data, pcOffset);
            AssertRomByteIs(expectedByteVal, data, pcOffset);
            AssertRomByteIsViaHelper(expectedByteVal, data, pcOffset);
        }

        // access via older helper interface
        private static void AssertRomByteIsViaHelper(byte expectedByteVal, Data data, int pcOffset)
        {
            Assert.Equal(expectedByteVal, data.GetRomByte(pcOffset));
        }

        // access via snes interface
        private static void AssertSnesByteIs(byte expectedByteVal, Data data, int pcOffset)
        {
            var snesAddress = data.ConvertPCtoSnes(pcOffset);
            Assert.NotEqual(-1, snesAddress);
            var rByte = ByteGraphUtil.BuildFlatDataFrom(data.SnesAddressSpace, snesAddress)
                .Byte; // TODO refactor: Make this be ByteSource.GetByte()
            Assert.NotNull(rByte);
            Assert.Equal(expectedByteVal, rByte.Value);
        }

        // access via Rom mapping interface
        private static void AssertRomByteIs(byte expectedByteVal, Data data, int pcOffset)
        {
            var rByte = ByteGraphUtil.BuildFlatDataFrom(data.RomByteSource, pcOffset).Byte;
            Assert.NotNull(rByte);
            Assert.Equal(expectedByteVal, rByte.Value);
        }

        [Fact]
        public static void TestLabels()
        {
            var data = TinyHiRomWithExtraLabel;
            Assert.Equal("SNES_VMADDL", data.Labels.GetLabelName(0x2116));
            Assert.Equal("", data.Labels.GetLabelName(0x2119)); // bogus address
            // Assert.Equal("SNES_VMADDL", data.GetLabelName(0x7E2116)); // later, we need this to ALSO work
        }

        [Fact]
        public static void IA1()
        {
            var data = TinyHiRomWithExtraLabel;
            Assert.Equal(0x002116, data.GetIntermediateAddressOrPointer(0));
        }

        [Fact]
        public static void IA2()
        {
            var data = TinyHiRomWithExtraLabel;
            data.RomByteSource.Bytes[0].DataBank = 0x7E;
            Assert.Equal(0x7E2116, data.GetIntermediateAddressOrPointer(0));
        }

        [Theory(Skip = "temp disabled til log exporter is less busted. TODO")]
        [MemberData(nameof(PipelineTesters))]
        public static void TestRom2(AssemblyPipelineTester romTester)
        {
            romTester.Test();
        }

        // TODO: FIXME: wont work til we fix the assembly export generation
        [Fact(Skip = "not yet working")]
        // [Fact]
        public static void RunTestRom()
        {
            // C# ROM -> Assembly Text 
            var exportAssembly = LogWriterHelper.ExportAssembly(TinyHiRomWithExtraLabel).OutputStr;

            // Assembly Text -> Asar -> SFC file
            var bytes = AsarRunner.AssembleToRom(exportAssembly);

            Assert.Equal(3, bytes.Count);

            Assert.Equal(0x8D, bytes[0]);
            Assert.Equal(0x16, bytes[1]);
            Assert.Equal(0x21, bytes[2]);
        }
    }
}
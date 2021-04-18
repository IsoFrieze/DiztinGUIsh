using System.Collections.Generic;
using Diz.Core;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.model.snes;
using Diz.Core.util;
using Diz.Test.Utils;
using Xunit;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace Diz.Test.tests
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
                
                new ByteEntry(new AnnotationCollection {
                    new OpcodeAnnotation {MFlag = true, XFlag = true, DataBank = 0x00, DirectPage = 0},
                    new MarkAnnotation {TypeFlag = FlagType.Opcode}
                })
                {
                    Byte = 0x8D
                },
                new(new AnnotationCollection {
                    new MarkAnnotation {TypeFlag = FlagType.Operand},
                    new Comment {Text = "unused"} // 0xC00001
                })
                {
                    Byte = 0x16
                },
                
                // sidenote: demonstrates another way to create Byte's, identical to above
                new(new AnnotationCollection {
                    new MarkAnnotation {TypeFlag = FlagType.Operand},
                    new ByteAnnotation {Byte = 0x21}
                })
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

            Assert.Equal(0x1000000, data.SnesAddressSpace.Bytes.Count);
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
            
            TestParentByteSourceRefs(0xEE, 0xC00000, data.SnesAddressSpace);
            TestParentByteSourceRefs(0x8D, 0x0, data.RomByteSource);
        }

        public static void TestParentByteSourceRefs(int expectedByteVal, int index, ByteSource expectedByteSource)
        {
            var byteOffsetData = expectedByteSource.Bytes[index];
            var b = byteOffsetData?.Byte;

            Assert.NotNull(b);
            Assert.NotNull(byteOffsetData);
            
            Assert.Equal(expectedByteVal, b.Value);
            Assert.Same(expectedByteSource, byteOffsetData.ParentByteSource);
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
            // Assert.Equal("SNES_VMADDL", data.Labels.GetLabelName(0x7E2116)); // later, we need mirrors like this to ALSO work for WRAM
        }

        [Fact]
        public static void IA1()
        {
            var data = TinyHiRomWithExtraLabel;
            Assert.Equal(0x002116, data.GetIntermediateAddressOrPointer(0));
        }

        [Fact]
        public static void TestWhenNoIAPresent()
        {
            var sampleData = SampleRomData.SampleData;
            const int offset = 0x1C1F;
            var result = sampleData.GetIntermediateAddressOrPointer(offset);
            Assert.Equal(result, -1);
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
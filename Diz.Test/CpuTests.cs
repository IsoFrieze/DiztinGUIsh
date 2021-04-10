using System.Collections.Generic;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.util;
using Diz.Test.Utils;
using Xunit;
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace Diz.Test
{
    public static class CpuTests
    {
        private static Data TinyHiRomData
        {
            get
            {
                var data = new Data(new ByteSource(new List<ByteOffsetData>
                {
                    // starts at PC=0, which is SNES=0xC00000
                    // STA.W SNES_VMADDL
                    // OR (equivalent)
                    // STA.W $2116
                    new()
                    {
                        Byte = 0x8D, Annotations = new List<Annotation>
                        {
                            new OpcodeAnnotation {MFlag = true, XFlag = true, DataBank = 0x00, DirectPage = 0},
                            new MarkAnnotation {TypeFlag = FlagType.Opcode}
                        }

                    },
                    new()
                    {
                        Byte = 0x16, Annotations = new List<Annotation>
                        {
                            new MarkAnnotation {TypeFlag = FlagType.Operand},
                            new Comment {Text = "unused"} // 0xC00001
                        }
                    },
                    new()
                    {
                        Byte = 0x21, Annotations = new List<Annotation>
                        {
                            new MarkAnnotation {TypeFlag = FlagType.Operand}
                        }
                    },
                }) {Name = "Snes Rom"}, RomMapMode.HiRom, RomSpeed.FastRom);
            
                data.LabelProvider.AddLabel(
                    0x002116, new Label {Name = "SNES_VMADDL", Comment = "SNES hardware register example."}
                );

                return data;
            }
        }
        
        public static TheoryData<AssemblyPipelineTester> PipelineTesters => new() {
            AssemblyPipelineTester.SetupFromResource(TinyHiRomData, "Diz.Test/Resources/asartestrun.asm")
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
            var data = TinyHiRomData;
            
            // note: this doesn't quite cover all the range if the offset is greater than the #bytes
            
            Assert.Equal(0x000002, data.ConvertSnesToPc(0xC00002));
            Assert.Equal(0xC00000, data.ConvertPCtoSnes(0x000000));
        }

        [Fact]
        public static void SanityTest()
        {
            var data = TinyHiRomData;
            Assert.Equal(3, data.GetRomSize());
            
            AssertRomByteEqual(0x8D, data, 0);
            AssertRomByteEqual(0x16, data, 1);
            AssertRomByteEqual(0x21, data, 2);
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
            var rByte = data.SnesAddressSpace.CompileAllChildDataFrom(snesAddress).Byte; // TODO refactor: Make this be ByteSource.GetByte()
            Assert.NotNull(rByte);
            Assert.Equal(expectedByteVal, rByte.Value);
        }
        
        // access via Rom mapping interface
        private static void AssertRomByteIs(byte expectedByteVal, Data data, int pcOffset)
        {
            var rByte = data.RomByteSource.CompileAllChildDataFrom(pcOffset).Byte;
            Assert.NotNull(rByte);
            Assert.Equal(expectedByteVal, rByte.Value);
        }

        [Fact]
        public static void TestLabels()
        {
            var data = TinyHiRomData;
            Assert.Equal("SNES_VMADDL", data.LabelProvider.GetLabelName(0x2116));
            Assert.Equal("", data.LabelProvider.GetLabelName(0x2119)); // bogus address
            // Assert.Equal("SNES_VMADDL", data.GetLabelName(0x7E2116)); // later, we need this to ALSO work
        }

        [Fact]
        public static void IA1()
        {
            var data = TinyHiRomData;
            Assert.Equal(0x002116, data.GetIntermediateAddressOrPointer(0));
        }

        [Fact]
        public static void IA2()
        {
            var data = TinyHiRomData;
            data.RomByteSource.Bytes[0].DataBank = 0x7E;
            Assert.Equal(0x7E2116, data.GetIntermediateAddressOrPointer(0));
        }

        [Theory]
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
            var exportAssembly = LogWriterHelper.ExportAssembly(TinyHiRomData).OutputStr;

            // Assembly Text -> Asar -> SFC file
            var bytes = AsarRunner.AssembleToRom(exportAssembly);

            Assert.Equal(3, bytes.Count);
            
            Assert.Equal(0x8D, bytes[0]);
            Assert.Equal(0x16, bytes[1]);
            Assert.Equal(0x21, bytes[2]);
        }
    }
}

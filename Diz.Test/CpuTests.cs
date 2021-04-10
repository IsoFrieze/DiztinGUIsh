using System.Collections.Generic;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.util;
using Diz.Test.Utils;
using Xunit;

namespace Diz.Test
{
    public static class CpuTests
    {
        private static Data GetSampleHiromFastRomData()
        {
            var romByteSource = new ByteSource(new List<ByteOffsetData>
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
            });

            var data = new Data(romByteSource, RomMapMode.HiRom, RomSpeed.FastRom);
            
            data.LabelProvider.AddLabel(
                0x002116, new Label {Name = "SNES_VMADDL", Comment = "SNES hardware register example."}
                );

            return data;
        }

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
            var data = GetSampleHiromFastRomData();
            
            // note: this doesn't quite cover all the range if the offset is greater than the #bytes
            
            Assert.Equal(0x000002, data.ConvertSnesToPc(0xC00002));
            Assert.Equal(0xC00000, data.ConvertPCtoSnes(0x000000));
        }

        [Fact]
        public static void SanityTest()
        {
            var data = GetSampleHiromFastRomData();
            Assert.Equal(3, data.GetRomSize());
            
            Assert.Equal(0x8D, data.GetRomByte(0));
            Assert.Equal(0x16, data.GetRomByte(1));
            Assert.Equal(0x21, data.GetRomByte(2));
        }

        [Fact]
        public static void TestLabels()
        {
            var data = GetSampleHiromFastRomData();
            Assert.Equal("SNES_VMADDL", data.LabelProvider.GetLabelName(0x2116));
            Assert.Equal("", data.LabelProvider.GetLabelName(0x2119)); // bogus address
            // Assert.Equal("SNES_VMADDL", data.GetLabelName(0x7E2116)); // later, we need this to ALSO work
        }

        [Fact]
        public static void IA1()
        {
            var data = GetSampleHiromFastRomData();
            Assert.Equal(0x002116, data.GetIntermediateAddressOrPointer(0));
        }

        [Fact]
        public static void IA2()
        {
            var data = GetSampleHiromFastRomData();
            data.RomByteSource.Bytes[0].DataBank = 0x7E;
            Assert.Equal(0x7E2116, data.GetIntermediateAddressOrPointer(0));
        }
        
        [Theory]
        [EmbeddedResourceData("Diz.Test/Resources/asartestrun.asm")]
        public static void TestRomAsmOutput(string expectedOutputAsm)
        {
            LogWriterHelper.AssertAssemblyOutputEquals(expectedOutputAsm, LogWriterHelper.ExportAssembly(GetSampleHiromFastRomData()));
        }

        // [Fact(Skip = "Relies on external tool that isn't yet setup")]
        [Fact]
        public static void RunTestRom()
        {
            // C# ROM -> Assembly Text 
            var exportAssembly = LogWriterHelper.ExportAssembly(GetSampleHiromFastRomData()).OutputStr;

            // Assembly Text -> Asar -> SFC file
            var bytes = AsarRunner.AssembleToRom(exportAssembly);

            Assert.Equal(3, bytes.Count);
            
            Assert.Equal(0x8D, bytes[0]);
            Assert.Equal(0x16, bytes[1]);
            Assert.Equal(0x21, bytes[2]);
        }
    }
}
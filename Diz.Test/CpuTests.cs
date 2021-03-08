using System.Collections.Generic;
using Diz.Core.model;
using Diz.Core.util;
using Diz.Test.Utils;
using IX.Observable;
using Xunit;

namespace Diz.Test
{
    public static class CpuTests
    {
        private static Data GetSampleData() => new Data
        {
            RomMapMode = RomMapMode.HiRom,
            RomSpeed = RomSpeed.FastRom,
            RomBytes = new RomBytes
            {
                // starts at PC=0, which is SNES=0xC00000
                // STA.W SNES_VMADDL
                // OR (equivalent)
                // STA.W $2116
                new RomByte
                {
                    Rom = 0x8D, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true, DataBank = 0x00,
                    DirectPage = 0,
                },
                new RomByte {Rom = 0x16, TypeFlag = FlagType.Operand},
                new RomByte {Rom = 0x21, TypeFlag = FlagType.Operand},
            },
            Comments = new ObservableDictionary<int, string>()
            {
                {0xC00001, "unused"},
            },
            Labels = new ObservableDictionary<int, Label>()
            {
                {0x002116, new Label {Name = "SNES_VMADDL", Comment = "SNES hardware register example."}},
            },
        };
        
        public static IReadOnlyList<byte> AssemblyRom => AsarRunner.AssembleToRom(@"
            hirom

            SNES_VMADDL = $002116
            ; SNES_VMADDL = $7E2116

            ORG $C00000

            STA.W SNES_VMADDL"
        );

        [Fact]
        public static void SanityTest()
        {
            var data = GetSampleData();
            Assert.Equal(0x8D, data.GetRomByte(0));
            Assert.Equal(0x16, data.GetRomByte(1));
            Assert.Equal(0x21, data.GetRomByte(2));
            Assert.Equal(3, data.GetRomSize());

            Assert.Equal("SNES_VMADDL", data.GetLabelName(0x2116));
            Assert.Equal("", data.GetLabelName(0x2119)); // bogus address
            // Assert.Equal("SNES_VMADDL", data.GetLabelName(0x7E2116)); // later, we need this to ALSO work
        }

        [Fact]
        public static void IA1()
        {
            var data = GetSampleData();
            Assert.Equal(0x002116, data.GetIntermediateAddressOrPointer(0));
        }

        [Fact]
        public static void IA2()
        {
            var data = GetSampleData();
            data.RomBytes[0].DataBank = 0x7E;
            Assert.Equal(0x7E2116, data.GetIntermediateAddressOrPointer(0));
        }

        [Fact(Skip = "Relies on external tool that isn't yet setup")]
        public static void RunTestRom()
        {
            // C# ROM -> Assembly Text 
            var exportAssembly = LogWriterHelper.ExportAssembly(GetSampleData()).OutputStr;
            
            // Assembly Text -> Asar -> SFC file
            var bytes = AsarRunner.AssembleToRom(exportAssembly);
            
            Assert.Equal(0x8D, bytes[0]);
            Assert.Equal(0x16, bytes[1]);
            Assert.Equal(0x21, bytes[2]);
            Assert.Equal(3, bytes.Count);
        } 
    }
}
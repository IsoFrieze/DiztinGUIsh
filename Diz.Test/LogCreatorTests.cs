using System.Collections.Generic;
using System.Linq;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.model.snes;
using Diz.Core.util;
using Diz.Test.Utils;
using Xunit;

namespace Diz.Test
{
    public sealed class LogCreatorTests
    {
        private const string ExpectedRaw =
            //          label:       instructions                         ;PC    |rawbytes|ia
            "                        lorom                                ;      |        |      ;  \r\n" +
            "                                                             ;      |        |      ;  \r\n" +
            "                                                             ;      |        |      ;  \r\n" +
            "                        ORG $808000                          ;      |        |      ;  \r\n" +
            "                                                             ;      |        |      ;  \r\n" +
            "           CODE_808000: LDA.W Test_Data,X                    ;808000|BD5B80  |80805B;  \r\n" +
            "                        STA.W $0100,X                        ;808003|9D0001  |800100;  \r\n" +
            "           Test22:      DEX                                  ;808006|CA      |      ;  \r\n" +
            "                        BPL CODE_808000                      ;808007|10F7    |808000;  \r\n" +
            "                                                             ;      |        |      ;  \r\n" +
            "                        Test_Data = $80805B                  ;      |        |      ;  \r\n";

        private Data InputRom
        {
            get
            {
                var bytes = new List<ByteEntry>
                {
                    // --------------------------
                    // highlighting a particular section here
                    // we will use this for unit tests as well.

                    // SNES address: 808000
                    // instruction: LDA.W Test_Data,X
                    new()
                    {
                        Byte = 0xBD, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.InPoint,
                        DataBank = 0x80, DirectPage = 0x2100
                    },
                    new() {Byte = 0x5B, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100}, // Test_Data
                    new() {Byte = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100}, // Test_Data

                    // SNES address: 808003
                    // instruction: STA.W $0100,X
                    new() {Byte = 0x9D, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
                    new() {Byte = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new() {Byte = 0x01, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},

                    // SNES address: 808006
                    // instruction: DEX
                    new()
                    {
                        Byte = 0xCA, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100,
                        Annotations = {new Label {Name = "Test22"}}
                    },

                    // SNES address: 808007
                    // instruction: BPL CODE_808000
                    new()
                    {
                        Byte = 0x10, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.OutPoint,
                        DataBank = 0x80, DirectPage = 0x2100
                    },
                    new() {Byte = 0xF7, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                };

                var actualRomBytes = new ByteSource {
                    Name = "Space Cats 2: Rise of Lopsy Dumpwell",
                    Bytes = new ByteList(bytes)
                };
                
                var data = new Data().PopulateFromRom(actualRomBytes, RomMapMode.LoRom, RomSpeed.FastRom);
                
                // another way to add comments, adds it to the SNES address space instead of the ROM.
                // retrievals should be unaffected.
                data.Labels.AddLabel(0x808000 + 0x5B, new Label {Name = "Test_Data", Comment = "Pretty cool huh?"});

                return data;
            }
        }

        [Fact]
        public void TestAFewLines()
        {
            LogWriterHelper.AssertAssemblyOutputEquals(ExpectedRaw, LogWriterHelper.ExportAssembly(InputRom));
        }

        [Fact]
        public void TestLabelAccess()
        {
            var actual = InputRom.SnesAddressSpace
                .GetAnnotationsIncludingChildrenEnumerator<Label>()
                .ToDictionary(pair => pair.Key);

            Assert.Equal(2, actual.Count);

            var l1 = actual.GetValueOrDefault(0x808006);
            Assert.NotEqual(default, l1);
            Assert.Equal("Test22", l1.Value.Name);
            
            var l2 = actual.GetValueOrDefault(0x80805B);
            Assert.NotEqual(default, l2);
            Assert.Equal("Test_Data", l2.Value.Name);

            Assert.Equal(default, actual.GetValueOrDefault(0x808008));
        }

        
        [Theory]
        [EmbeddedResourceData("Diz.Test/Resources/emptyrom.asm")]
        public void TestEmptyRom(string expectedAsm)
        {
            var result = LogWriterHelper.ExportAssembly(new Data());
            LogWriterHelper.AssertAssemblyOutputEquals(expectedAsm, result);
        }
    }
}
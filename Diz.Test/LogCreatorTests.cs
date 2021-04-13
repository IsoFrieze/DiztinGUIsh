using System.Collections.Generic;
using Diz.Core.model;
using Diz.Core.model.byteSources;
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

                    // CODE_808000: LDA.W Test_Data,X
                    new()
                    {
                        Byte = 0xBD, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.InPoint,
                        DataBank = 0x80, DirectPage = 0x2100
                    },
                    new() {Byte = 0x5B, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100}, // Test_Data
                    new() {Byte = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100}, // Test_Data

                    // STA.W $0100,X
                    new() {Byte = 0x9D, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
                    new() {Byte = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new() {Byte = 0x01, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},

                    // DEX
                    new()
                    {
                        Byte = 0xCA, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100,
                        Annotations = {new Label {Name = "Test22"}}
                    },

                    // BPL CODE_808000
                    new()
                    {
                        Byte = 0x10, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.OutPoint,
                        DataBank = 0x80, DirectPage = 0x2100
                    },
                    new() {Byte = 0xF7, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},

                    // ------------------------------------
                };

                var data = new Data(
                    new ByteSource {
                    Name = "Super Matador Brothers 2, Now you're power with playing",
                    Bytes = new ByteList(bytes)
                }, RomMapMode.LoRom, RomSpeed.FastRom);
                
                // another way to add comments, adds it to the SNES address space instead of the ROM.
                // retrievals should be unaffected.
                data.Labels.AddLabel(0x808000 + 0x5B, new Label {Name = "Test_Data", Comment = "Pretty cool huh?"});

                return data;
            }
        }

        [Fact(Skip="currently busted til we fix the log exporter, disabling for now")]
        public void TestAFewLines()
        {
            LogWriterHelper.AssertAssemblyOutputEquals(ExpectedRaw, LogWriterHelper.ExportAssembly(InputRom));
        }

        [Fact]
        public void TestLeftAlign()
        {
            Assert.Equal("xyz  ", Util.LeftAlign(5, "xyz"));
            Assert.Equal("xyz", Util.LeftAlign(3, "xyz"));
            Assert.Equal("{0,-22}",Util.GetLeftAlignFormatStr(22));
            Assert.Equal("{0,22}",Util.GetLeftAlignFormatStr(-22));
        }
    }
}
using System.Collections.Generic;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.model.snes;
using Diz.Core.util;

namespace Diz.Test.TestData
{
    public sealed class SpaceCatsRom
    {
        public static Data CreateInputRom()
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
                new()
                {
                    Byte = 0x9D, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100,
                    Annotations = {new Label {Name = "Fn_go1", Comment = "Store some stuff"}}
                },
                new() {Byte = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x01, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},

                // SNES address: 808006
                // instruction: DEX
                new()
                {
                    Byte = 0xCA, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100,
                    Annotations = {new Label {Name = "Test22", Comment="LabelComment"}, new Comment {Text = "LineComment"}}
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

            var actualRomBytes = new ByteSource
            {
                Name = "Space Cats 2: Rise of Lopsy Dumpwell",
                Bytes = new StorageList<ByteEntry>(bytes)
            };

            // var data = new TestData()
            var data = new Data()
                .PopulateFromRom(actualRomBytes, RomMapMode.LoRom, RomSpeed.FastRom);

            // another way to add comments, adds it to the SNES address space instead of the ROM.
            // retrievals should be unaffected.
            data.Labels.AddLabel(0x808000 + 0x5B, new Label {Name = "Test_Data", Comment = "Pretty cool huh?"});
            data.AddComment(0x808000 + 0x5C, "XYZ");

            return data;
        }
    }
}
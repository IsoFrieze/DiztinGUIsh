using Diz.Core.core;
using Diz.Core.core.util;
using DiztinGUIsh.core;

namespace DiztinGUIsh
{
    public class SampleRomData : Data
    {
        public static SampleRomData SampleData
        {
            get
            {
                // one-time: take our sample data from below and bolt some extra stuff on top of it.
                // then, cache all this in a static read-only property

                if (finalSampleData != null)
                    return finalSampleData;

                while (BaseSampleData.RomBytes.Count < 0x8000)
                    BaseSampleData.RomBytes.Add(new ROMByte());

                finalSampleData = BaseSampleData;
                return BaseSampleData;
            }
        }

        private static SampleRomData finalSampleData;

        private static readonly SampleRomData BaseSampleData = new SampleRomData
        {
            // random sample code I made up; hopefully it shows a little bit of
            // everything so you can see how the settings will effect the output
            RomBytes = new RomBytes {
                new ROMByte {Rom = 0x78, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true, Point = InOutPoint.InPoint},
                new ROMByte {Rom = 0xA9, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true},
                new ROMByte {Rom = 0x01, TypeFlag = FlagType.Operand},
                new ROMByte {Rom = 0x8D, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true},
                new ROMByte {Rom = 0x0D, TypeFlag = FlagType.Operand},
                new ROMByte {Rom = 0x42, TypeFlag = FlagType.Operand},
                new ROMByte {Rom = 0x5C, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true, Point = InOutPoint.EndPoint},
                new ROMByte {Rom = 0x0A, TypeFlag = FlagType.Operand},
                new ROMByte {Rom = 0x80, TypeFlag = FlagType.Operand},
                new ROMByte {Rom = 0x80, TypeFlag = FlagType.Operand},
                new ROMByte {Rom = 0xC2, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true, Point = InOutPoint.InPoint},
                new ROMByte {Rom = 0x30, TypeFlag = FlagType.Operand},
                new ROMByte {Rom = 0xA9, TypeFlag = FlagType.Opcode},
                new ROMByte {Rom = 0x00, TypeFlag = FlagType.Operand},
                new ROMByte {Rom = 0x21, TypeFlag = FlagType.Operand},
                new ROMByte {Rom = 0x5B, TypeFlag = FlagType.Opcode},
                new ROMByte {Rom = 0x4B, TypeFlag = FlagType.Opcode, DirectPage = 0x2100},
                new ROMByte {Rom = 0xAB, TypeFlag = FlagType.Opcode, DirectPage = 0x2100},
                new ROMByte {Rom = 0xA2, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x07, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xBF, TypeFlag = FlagType.Opcode, Point = InOutPoint.InPoint, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x32, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x9F, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x7E, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xCA, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xCA, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x10, TypeFlag = FlagType.Opcode, Point = InOutPoint.OutPoint, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xF4, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x64, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x40, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x64, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x41, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x64, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x42, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x64, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x43, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xAE, TypeFlag = FlagType.Opcode, Point = InOutPoint.InPoint, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xFC, TypeFlag = FlagType.Opcode, Point = InOutPoint.OutPoint, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x3A, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x4C, TypeFlag = FlagType.Opcode, Point = InOutPoint.EndPoint, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xC0, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x00, TypeFlag = FlagType.Data16Bit, Point = InOutPoint.ReadPoint, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x00, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x08, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x00, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x10, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x00, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x20, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x00, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x44, TypeFlag = FlagType.Pointer16Bit, Point = InOutPoint.ReadPoint, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x80, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x7B, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x80, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x44, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x81, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xC4, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x81, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x0A, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x82, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x08, TypeFlag = FlagType.Opcode, Point = InOutPoint.InPoint, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x8B, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x4B, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xAB, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xE2, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x20, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xC2, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x10, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xA2, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x1F, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xBD, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.InPoint, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x5B, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x9D, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x01, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xCA, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x10, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.OutPoint, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xF7, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xAB, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x28, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x60, TypeFlag = FlagType.Opcode, Point = InOutPoint.EndPoint, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x45, TypeFlag = FlagType.Data8Bit, Point = InOutPoint.ReadPoint, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x8D, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x69, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x83, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xB2, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x99, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x00, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x23, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x01, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xA3, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xF8, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x52, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x08, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xBB, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x29, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x5C, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x32, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xE7, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x88, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x3C, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x30, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x18, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x9A, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xB0, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x34, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x8C, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xDD, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x05, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0xB7, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x83, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x34, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new ROMByte {Rom = 0x6D, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            },
            Comments = new OdWrapper<int, string>()
            {
                Dict = {
                    {0x03, "this sets FastROM"},
                    {0x0F, "direct page = $2100"},
                    {0x21, "clear APU regs"},
                    {0x44, "this routine copies Test_Data to $7E0100"}
                }
            },
            Labels = new OdWrapper<int, Label>()
            {
                Dict = {
                    {0x00, new Label {name = "Emulation_RESET", comment = "Sample emulation reset location"}},
                    {0x0A, new Label {name = "FastRESET", comment = "Sample label"}},
                    {0x32, new Label {name = "Test_Indices"}},
                    {0x3A, new Label {name = "Pointer_Table"}},
                    {0x44, new Label {name = "First_Routine"}},
                    {0x5B, new Label {name = "Test_Data", comment = "Pretty cool huh?"}}
                }
            },
            RomMapMode = ROMMapMode.LoROM,
            RomSpeed = ROMSpeed.FastROM,
        };
    }
}

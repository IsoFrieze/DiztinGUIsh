using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Diz.Core.model;
using Diz.Core.util;
using IX.Observable;

namespace Diz.Core
{
    public class SampleRomData : Data
    {
        public static string GetSampleUtf8CartridgeTitle() => "｢ﾎ｣ abcｦｧｨ TEST123"; // don't pad here

        // input can be any length, and will be padded, using spaces, to the right size for SNES header
        private void SetCartridgeTitle(string utf8CartridgeTitle)
        {
            var rawShiftJisBytes = ByteUtil.GetRawShiftJisBytesFromStr(utf8CartridgeTitle);
            var paddedShiftJisBytes = ByteUtil.PadCartridgeTitleBytes(rawShiftJisBytes);

            // the BYTES need to be 21 in length. this is NOT the string length (which can be different because of multibyte chars)
            Debug.Assert(paddedShiftJisBytes.Length == RomUtil.LengthOfTitleName);
            
            RomBytes.SetBytesFrom(paddedShiftJisBytes, CartridgeTitleStartingOffset);
        }

        // debug only: use this for the sample rom data to populate it's own bytes.
        // NORMALLY on a project deserialized, we'd open the ROM file on disk and pt the bytes in RomBytes.
        // for this, there is no ROM on disk, so we cheat and load our own bytes back in there.
        // this method is super-wasteful and inefficient but it's ok because it's only for testing.
        public override byte[]? GetOverriddenRomBytes() => 
            CreateSampleData().RomBytes.Select(rb =>
            {
                Debug.Assert(rb != null);
                return rb.Rom;
            }).ToArray();

        public int OriginalRomSizeBeforePadding { get; set; }

        public static SampleRomData CreateSampleData()
        {
            var partialSampleData = new SampleRomData
            {
                // random sample code I made up; hopefully it shows a little bit of
                // everything so you can see how the settings will effect the output
                RomMapMode = RomMapMode.LoRom,
                RomSpeed = RomSpeed.FastRom,
                RomBytes = new RomBytes {
                    new RomByte {Rom = 0x78, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true, Point = InOutPoint.InPoint},
                    new RomByte {Rom = 0xA9, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true},
                    new RomByte {Rom = 0x01, TypeFlag = FlagType.Operand},
                    new RomByte {Rom = 0x8D, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true},
                    new RomByte {Rom = 0x0D, TypeFlag = FlagType.Operand},
                    new RomByte {Rom = 0x42, TypeFlag = FlagType.Operand},
                    new RomByte {Rom = 0x5C, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true, Point = InOutPoint.EndPoint},
                    new RomByte {Rom = 0x0A, TypeFlag = FlagType.Operand},
                    new RomByte {Rom = 0x80, TypeFlag = FlagType.Operand},
                    new RomByte {Rom = 0x80, TypeFlag = FlagType.Operand},
                    new RomByte {Rom = 0xC2, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true, Point = InOutPoint.InPoint},
                    new RomByte {Rom = 0x30, TypeFlag = FlagType.Operand},
                    new RomByte {Rom = 0xA9, TypeFlag = FlagType.Opcode},
                    new RomByte {Rom = 0x00, TypeFlag = FlagType.Operand},
                    new RomByte {Rom = 0x21, TypeFlag = FlagType.Operand},
                    new RomByte {Rom = 0x5B, TypeFlag = FlagType.Opcode},
                    new RomByte {Rom = 0x4B, TypeFlag = FlagType.Opcode, DirectPage = 0x2100},
                    new RomByte {Rom = 0xAB, TypeFlag = FlagType.Opcode, DirectPage = 0x2100},
                    new RomByte {Rom = 0xA2, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x07, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xBF, TypeFlag = FlagType.Opcode, Point = InOutPoint.InPoint, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x32, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x9F, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x7E, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xCA, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xCA, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x10, TypeFlag = FlagType.Opcode, Point = InOutPoint.OutPoint, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xF4, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x64, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x40, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x64, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x41, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x64, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x42, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x64, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x43, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xAE, TypeFlag = FlagType.Opcode, Point = InOutPoint.InPoint, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xFC, TypeFlag = FlagType.Opcode, Point = InOutPoint.OutPoint, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x3A, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x4C, TypeFlag = FlagType.Opcode, Point = InOutPoint.EndPoint, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xC0, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x00, TypeFlag = FlagType.Data16Bit, Point = InOutPoint.ReadPoint, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x00, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x08, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x00, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x10, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x00, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x20, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x00, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x44, TypeFlag = FlagType.Pointer16Bit, Point = InOutPoint.ReadPoint, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x80, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x7B, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x80, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x44, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x81, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xC4, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x81, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x0A, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x82, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x08, TypeFlag = FlagType.Opcode, Point = InOutPoint.InPoint, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x8B, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x4B, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xAB, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xE2, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x20, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xC2, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x10, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xA2, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x1F, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},

                    // --------------------------
                    // highlighting a particular section here
                    // we will use this for unit tests as well.

                    // LDA.W Test_Data,X
                    new RomByte {Rom = 0xBD, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.InPoint, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x5B, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100}, // Test_Data
                    new RomByte {Rom = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100}, // Test_Data
                    
                    // STA.W $0100,X
                    new RomByte {Rom = 0x9D, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x01, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    
                    // DEX
                    new RomByte {Rom = 0xCA, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},

                    // BPL CODE_80804F
                    new RomByte {Rom = 0x10, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.OutPoint, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xF7, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                    
                    // ------------------------------------

                    new RomByte {Rom = 0xAB, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x28, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x60, TypeFlag = FlagType.Opcode, Point = InOutPoint.EndPoint, DataBank = 0x80, DirectPage = 0x2100},
                    
                    // --------------------------
                    
                    new RomByte {Rom = 0x45, TypeFlag = FlagType.Data8Bit, Point = InOutPoint.ReadPoint, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x8D, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x69, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x83, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xB2, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x99, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x00, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x23, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x01, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xA3, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xF8, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x52, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x08, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xBB, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x29, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x5C, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x32, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xE7, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x88, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x3C, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x30, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x18, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x9A, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xB0, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x34, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x8C, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xDD, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x05, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0xB7, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x83, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x34, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                    new RomByte {Rom = 0x6D, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                },
                Comments = new ObservableDictionary<int, string>()
                {
                    {0x808000 + 0x03, "this sets FastROM"},
                    {0x808000 + 0x0F, "direct page = $2100"},
                    {0x808000 + 0x21, "clear APU regs"},
                    {0x808000 + 0x44, "this routine copies Test_Data to $7E0100"}
                },
                Labels = new ObservableDictionary<int, Label>()
                {
                    {0x808000 + 0x00, new Label {Name = "Emulation_RESET", Comment = "Sample emulation reset location"}},
                    {0x808000 + 0x0A, new Label {Name = "FastRESET", Comment = "Sample label"}},
                    {0x808000 + 0x32, new Label {Name = "Test_Indices"}},
                    {0x808000 + 0x3A, new Label {Name = "Pointer_Table"}},
                    {0x808000 + 0x44, new Label {Name = "First_Routine"}},
                    {0x808000 + 0x5B, new Label {Name = "Test_Data", Comment = "Pretty cool huh?"}}
                },
            };

            // tricky: this sample data can be used to populate the "sample assembly output"
            // window to demo some features. One thing we'd like to demo is showing generated
            // labels that reach into "unreached" code (i.e. labels like "UNREACH_XXXXX")
            //
            // To accomplish this, we'll pad the size of the sample ROM data to 32k, but,
            // we'll tell the assembly exporter to limit to the first couple hundred bytes by
            // only assembling bytes up to BaseSampleData.SizeOverride.
            partialSampleData.OriginalRomSizeBeforePadding = partialSampleData.RomBytes.Count;
            while (partialSampleData.RomBytes.Count < 0x8000)
                partialSampleData.RomBytes.Add(new RomByte());
                
            // inject the game name into the bytes
            // This is a UTF8 string that needs to be converted to ShiftJIS (Ascii w/some japanese chars) encoding.
            partialSampleData.SetCartridgeTitle(GetSampleUtf8CartridgeTitle());
            partialSampleData.FixChecksum();

            return partialSampleData;
        }
    }
}

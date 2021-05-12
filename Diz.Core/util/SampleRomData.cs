using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.Core.serialization;

namespace Diz.Core.util
{
    public class SampleData : Data
    {
        // debug only: use this for the sample rom data to populate it's own bytes.
        // NORMALLY on a project deserialized, we'd open the ROM file on disk and pt the bytes in RomBytes.
        // for this, there is no ROM on disk, so we cheat and load our own bytes back in there.
        // performance: this method is super-wasteful and inefficient but it's ok because it's only for testing.
        public override byte[]? GetOverriddenRomBytes() =>
            SampleRomByteSource.Create().byteSource.Bytes.Select(entry => entry.Byte ?? 0).ToArray();
    }
    
    public class SampleRomData
    {
        public int OriginalRomSizeBeforePadding { get; init; }
        public static string GetSampleUtf8CartridgeTitle() => "｢ﾎ｣ abcｦｧｨ TEST123"; // don't pad here
        public Data Data { get; } = new();
        
        // input can be any length, and will be padded, using spaces, to the right size for SNES header
        private void SetCartridgeTitle(string utf8CartridgeTitle)
        {
            var rawShiftJisBytes = ByteUtil.GetRawShiftJisBytesFromStr(utf8CartridgeTitle);
            var paddedShiftJisBytes = ByteUtil.PadCartridgeTitleBytes(rawShiftJisBytes);

            // the BYTES need to be 21 in length. this is NOT the string length (which can be different because of multibyte chars)
            Debug.Assert(paddedShiftJisBytes.Length == RomUtil.LengthOfTitleName);
            
            Data.RomByteSource.SetBytesFrom(paddedShiftJisBytes, Data.CartridgeTitleStartingOffset);
        }

        public static SampleRomData CreateSampleData()
        {
            var (originalUnpaddedSize, sampleRomByteSource) = SampleRomByteSource.Create();
            
            // TODO: eventually, replace this padding stuff with a new layer of ROM bytes that is size 0x8000
            var sampleData = new SampleRomData
            {
                OriginalRomSizeBeforePadding = originalUnpaddedSize,
            };
            sampleData.Data.PopulateFromRom(sampleRomByteSource, RomMapMode.LoRom, RomSpeed.FastRom);
            
            // another way shown below for adding comments/labels shown below.
            // you can also directly add them to Bytes above as annotations.
            new Dictionary<int, string>
                {
                    {0x808000 + 0x03, "this sets FastROM"},
                    {0x808000 + 0x0F, "direct page = $2100"},
                    {0x808000 + 0x21, "clear APU regs"},
                    {0x808000 + 0x44, "this routine copies Test_Data to $7E0100"}
                }
                .ForEach(kvp =>
                    sampleData.Data.AddComment(kvp.Key, kvp.Value)
                );

            new Dictionary<int, Label>
                {
                    {0x808000 + 0x00, new Label {Name = "Emulation_RESET", Comment = "Sample emulation reset location"}},
                    {0x808000 + 0x0A, new Label {Name = "FastRESET", Comment = "Sample label"}},
                    {0x808000 + 0x32, new Label {Name = "Test_Indices"}},
                    {0x808000 + 0x3A, new Label {Name = "Pointer_Table"}},
                    {0x808000 + 0x44, new Label {Name = "First_Routine"}},
                    {0x808000 + 0x5B, new Label {Name = "Test_Data", Comment = "Pretty cool huh?"}}
                }
                .ForEach(kvp =>
                    sampleData.Data.Labels.AddLabel(kvp.Key, kvp.Value)
                );
            
                
            // inject the game name into the bytes
            // This is a UTF8 string that needs to be converted to
            // ShiftJIS (Ascii w/some japanese chars) encoding.
            sampleData.SetCartridgeTitle(GetSampleUtf8CartridgeTitle());
            sampleData.Data.FixChecksum();

            return sampleData;
        }

        // return a simple, minimal import settings for the Sample rom data, for use
        // with test methods. does not generate interrupt vector table stuff, nor 
        // initial labels.
        public static ImportRomSettings CreateRomImportSettingsForSampleRom()
        {
            return new()
            {
                RomFilename = "diz-sample-rom.smc", 
                RomBytes = SampleRomByteSource.CreateJustRawBytes(), 
                RomMapMode = RomMapMode.LoRom,
            };
        }

        // create a super-minimal sample project with our sample ROM loaded into it
        // there's a bunch of ways to do this now, this is just one.
        // no interrupt vectors or default labels will be added.
        public static Project CreateSampleProject()
        {
            var settings = CreateRomImportSettingsForSampleRom();
            
            var project = new Project
            {
                AttachedRomFilename = settings.RomFilename,
                Data = new Data(),
            };
            
            var mapping = RomUtil.CreateRomMappingFromRomRawBytes(
                settings.RomBytes,
                settings.RomMapMode, 
                settings.RomSpeed);
            
            project.Data.SnesAddressSpace.ChildSources.Add(mapping);
            
            return project;
        }
    }
}
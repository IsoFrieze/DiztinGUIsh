using System.Diagnostics;
using Diz.Core;
using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.Core.util;
using IX.Observable;

namespace Diz.Cpu._65816;

public class SnesSampleRomDataFactory : ISampleDataFactory
{
    private readonly IDataFactory dataFactory;
    public SnesSampleRomDataFactory(IDataFactory dataFactory)
    {
        this.dataFactory = dataFactory;
    }

    public static string GetSampleUtf8CartridgeTitle() => "｢ﾎ｣ abcｦｧｨ TEST123"; // don't pad here
    
    public Data Create()
    {
        var data = dataFactory.Create();

        data.RomMapMode = RomMapMode.LoRom;
        data.RomSpeed = RomSpeed.FastRom;

        // random sample code I made up; hopefully it shows a little bit of
        // everything so you can see how the settings will effect the output
        data.RomBytes = new RomBytes
        {
            new() { Rom = 0x78, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true, Point = InOutPoint.InPoint },
            new() { Rom = 0xA9, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true },
            new() { Rom = 0x01, TypeFlag = FlagType.Operand },
            new() { Rom = 0x8D, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true },
            new() { Rom = 0x0D, TypeFlag = FlagType.Operand },
            new() { Rom = 0x42, TypeFlag = FlagType.Operand },
            new() { Rom = 0x5C, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true, Point = InOutPoint.EndPoint },
            new() { Rom = 0x0A, TypeFlag = FlagType.Operand },
            new() { Rom = 0x80, TypeFlag = FlagType.Operand },
            new() { Rom = 0x80, TypeFlag = FlagType.Operand },
            new() { Rom = 0xC2, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true, Point = InOutPoint.InPoint },
            new() { Rom = 0x30, TypeFlag = FlagType.Operand },
            new() { Rom = 0xA9, TypeFlag = FlagType.Opcode },
            new() { Rom = 0x00, TypeFlag = FlagType.Operand },
            new() { Rom = 0x21, TypeFlag = FlagType.Operand },
            new() { Rom = 0x5B, TypeFlag = FlagType.Opcode },
            new() { Rom = 0x4B, TypeFlag = FlagType.Opcode, DirectPage = 0x2100 },
            new() { Rom = 0xAB, TypeFlag = FlagType.Opcode, DirectPage = 0x2100 },
            new() { Rom = 0xA2, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x07, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new()
            {
                Rom = 0xBF, TypeFlag = FlagType.Opcode, Point = InOutPoint.InPoint, DataBank = 0x80, DirectPage = 0x2100
            },
            new() { Rom = 0x32, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x9F, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x7E, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0xCA, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0xCA, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100 },
            new()
            {
                Rom = 0x10, TypeFlag = FlagType.Opcode, Point = InOutPoint.OutPoint, DataBank = 0x80,
                DirectPage = 0x2100
            },
            new() { Rom = 0xF4, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x64, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x40, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x64, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x41, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x64, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x42, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x64, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x43, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new()
            {
                Rom = 0xAE, TypeFlag = FlagType.Opcode, Point = InOutPoint.InPoint, DataBank = 0x80, DirectPage = 0x2100
            },
            new() { Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new()
            {
                Rom = 0xFC, TypeFlag = FlagType.Opcode, Point = InOutPoint.OutPoint, DataBank = 0x80,
                DirectPage = 0x2100
            },
            new() { Rom = 0x3A, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new()
            {
                Rom = 0x4C, TypeFlag = FlagType.Opcode, Point = InOutPoint.EndPoint, DataBank = 0x80,
                DirectPage = 0x2100
            },
            new() { Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0xC0, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new()
            {
                Rom = 0x00, TypeFlag = FlagType.Data16Bit, Point = InOutPoint.ReadPoint, DataBank = 0x80,
                DirectPage = 0x2100
            },
            new() { Rom = 0x00, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x08, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x00, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x10, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x00, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x20, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x00, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new()
            {
                Rom = 0x44, TypeFlag = FlagType.Pointer16Bit, Point = InOutPoint.ReadPoint, DataBank = 0x80,
                DirectPage = 0x2100
            },
            new() { Rom = 0x80, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x7B, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x80, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x44, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x81, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0xC4, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x81, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x0A, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x82, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new()
            {
                Rom = 0x08, TypeFlag = FlagType.Opcode, Point = InOutPoint.InPoint, DataBank = 0x80, DirectPage = 0x2100
            },
            new() { Rom = 0x8B, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x4B, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0xAB, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0xE2, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x20, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0xC2, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x10, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0xA2, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x1F, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },

            // --------------------------
            // highlighting a particular section here
            // we will use this for unit tests as well.

            // LDA.W Test_Data,X
            new()
            {
                Rom = 0xBD, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.InPoint, DataBank = 0x80,
                DirectPage = 0x2100
            },
            new() { Rom = 0x5B, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 }, // Test_Data
            new() { Rom = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 }, // Test_Data

            // STA.W $0100,X
            new() { Rom = 0x9D, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x01, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },

            // DEX
            new() { Rom = 0xCA, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100 },

            // BPL CODE_80804F
            new()
            {
                Rom = 0x10, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.OutPoint, DataBank = 0x80,
                DirectPage = 0x2100
            },
            new() { Rom = 0xF7, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100 },

            // ------------------------------------

            new() { Rom = 0xAB, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x28, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100 },
            new()
            {
                Rom = 0x60, TypeFlag = FlagType.Opcode, Point = InOutPoint.EndPoint, DataBank = 0x80,
                DirectPage = 0x2100
            },

            // --------------------------

            new()
            {
                Rom = 0x45, TypeFlag = FlagType.Data8Bit, Point = InOutPoint.ReadPoint, DataBank = 0x80,
                DirectPage = 0x2100
            },
            new() { Rom = 0x8D, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x69, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x83, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0xB2, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x99, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x00, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x23, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x01, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0xA3, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0xF8, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x52, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x08, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0xBB, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x29, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x5C, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x32, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0xE7, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x88, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x3C, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x30, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x18, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x9A, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0xB0, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x34, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x8C, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0xDD, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x05, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0xB7, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x83, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x34, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
            new() { Rom = 0x6D, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100 },
        };
        data.Comments = new ObservableDictionary<int, string>
        {
            { 0x808000 + 0x03, "this sets FastROM" },
            { 0x808000 + 0x0F, "direct page = $2100" },
            { 0x808000 + 0x21, "clear APU regs" },
            { 0x808000 + 0x44, "this routine copies Test_Data to $7E0100" }
        };

        new Dictionary<int, Label>
            {
                {
                    0x808000 + 0x00, new Label { Name = "Emulation_RESET", Comment = "Sample emulation reset location" }
                },
                { 0x808000 + 0x0A, new Label { Name = "FastRESET", Comment = "Sample label" } },
                { 0x808000 + 0x32, new Label { Name = "Test_Indices" } },
                { 0x808000 + 0x3A, new Label { Name = "Pointer_Table" } },
                { 0x808000 + 0x44, new Label { Name = "First_Routine" } },
                { 0x808000 + 0x5B, new Label { Name = "Test_Data", Comment = "Pretty cool huh?" } }
            }
            .ForEach(kvp =>
                data.Labels.AddLabel(kvp.Key, kvp.Value)
            );

        PostProcess(data);
        return data;
    }

    private static int PadRomBytesUpTo(Data data, int numBytesToPadUpTo)
    {
        var originalRomSizeBeforePadding = data.RomBytes.Count;
        while (data.RomBytes.Count < numBytesToPadUpTo)
            data.RomBytes.Add(new RomByte());

        return originalRomSizeBeforePadding;
    }

    private static void Pad(Data data)
    {
        // tricky: this sample data can be used to populate the "sample assembly output"
        // window to demo some features. One thing we'd like to demo is showing generated
        // labels that reach into "unreached" code (i.e. labels like "UNREACH_XXXXX")
        //
        // To accomplish this, we'll pad the size of the sample ROM data to 32k, but,
        // we'll tell the assembly exporter to limit to the first couple hundred bytes by
        // only assembling bytes up to the original amount (not the padded amount)
        const int numBytesToPadUpTo = 0x8000;
        var originalRomSizeBeforePadding = PadRomBytesUpTo(data, numBytesToPadUpTo);
        
        data.Tags.AddIfDoesntExist(new SampleDataGenerationTag
        {
            OriginalRomSizeBeforePadding = originalRomSizeBeforePadding
        });
    }

    private static void PostProcess(Data data)
    {
        Pad(data);
        
        var snesApi = data.GetSnesApi();
        Debug.Assert(snesApi != null);

        // inject the game name into the bytes
        // This is a UTF8 string that needs to be converted to ShiftJIS (Ascii w/some japanese chars) encoding.
        snesApi.SetCartridgeTitle(GetSampleUtf8CartridgeTitle());

        // initialize some SNES header stuff (this is not complete, feel free to add things that are useful)
        Debug.Assert(snesApi.RomMapMode == RomMapMode.LoRom);
        var romSettingsOffset = RomUtil.GetRomSettingOffset(RomMapMode.LoRom);
        data.RomBytes[romSettingsOffset].Rom = 0x20;
        // TODO: set a few other useful things like ROM size etc.
        // we could probably steal some more of this code out of asar
        // maybe like:
        //    org $00FFD7
        //    db $0A ; mark the rom as 512 kb
        
        // do this LAST after all modifications to the ROM bytes have been completed 
        snesApi.FixChecksum();

        // NORMALLY when a project is loaded, we have to open the ROM file on disk and read the bytes on disk into RomBytes
        // for this sample data, there is no ROM on disk, so we tell Diz we already took care of it
        data.RomBytesLoaded = true;
    }
}
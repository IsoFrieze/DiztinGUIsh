using System.Collections.Generic;
using System.Linq;
using Diz.Core.model;
using Diz.Core.model.byteSources;

namespace Diz.Core.util
{
    public static class SampleRomByteSource
    {
        public static string GetSampleUtf8CartridgeTitle() => "｢ﾎ｣ abcｦｧｨ TEST123"; // don't pad this here, it'll happen later

        public static (int originalUnpaddedSize, ByteSource byteSource) Create(int padToSize = 0x8000)
        {
            var byteSource = new ByteSource
            {
                Bytes = CreateRawStorageSampleBytes(),
                Name = "Diz Sample Snes Rom"
            };

            var unpaddedSize = byteSource.Bytes.Count;

            // tricky: this sample data can be used to populate the "sample assembly output"
            // window to demo some features. One thing we'd like to demo is showing generated
            // labels that reach into "unreached" code (i.e. labels like "UNREACH_XXXXX")
            //
            // To accomplish this, we'll pad the size of the sample ROM data to 32k, but,
            // we'll tell the assembly exporter to limit to the first couple hundred bytes by
            // only assembling bytes up to BaseSampleData.SizeOverride.
            while (byteSource.Bytes.Count < padToSize)
                byteSource.Bytes.Add(new ByteEntry {Byte = 0x00});

            // perf: this is going to be pretty slow, i don't think we care much since it's mostly for testing.
            byteSource.Bytes.AddRange(
                Enumerable.Repeat(
                        new ByteEntry {Byte = 0x00}, padToSize - unpaddedSize)
                    .ToArray());

            return (unpaddedSize, byteSource);
        }

        private static Storage<ByteEntry> CreateRawStorageSampleBytes()
        {
            // random sample code I made up; hopefully it shows a little bit of
            // everything so you can see how the settings will effect the output
            // (^^ said Alex)
            return new StorageList<ByteEntry>(new List<ByteEntry>
            {
                new() {Byte = 0x78, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true, Point = InOutPoint.InPoint},
                new() {Byte = 0xA9, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true},
                new() {Byte = 0x01, TypeFlag = FlagType.Operand},
                new() {Byte = 0x8D, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true},
                new() {Byte = 0x0D, TypeFlag = FlagType.Operand},
                new() {Byte = 0x42, TypeFlag = FlagType.Operand},
                new() {Byte = 0x5C, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true, Point = InOutPoint.EndPoint},
                new() {Byte = 0x0A, TypeFlag = FlagType.Operand},
                new() {Byte = 0x80, TypeFlag = FlagType.Operand},
                new() {Byte = 0x80, TypeFlag = FlagType.Operand},
                new() {Byte = 0xC2, TypeFlag = FlagType.Opcode, MFlag = true, XFlag = true, Point = InOutPoint.InPoint},
                new() {Byte = 0x30, TypeFlag = FlagType.Operand},
                new() {Byte = 0xA9, TypeFlag = FlagType.Opcode},
                new() {Byte = 0x00, TypeFlag = FlagType.Operand},
                new() {Byte = 0x21, TypeFlag = FlagType.Operand},
                new() {Byte = 0x5B, TypeFlag = FlagType.Opcode},
                new() {Byte = 0x4B, TypeFlag = FlagType.Opcode, DirectPage = 0x2100},
                new() {Byte = 0xAB, TypeFlag = FlagType.Opcode, DirectPage = 0x2100},
                new() {Byte = 0xA2, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x07, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new()
                {
                    Byte = 0xBF, TypeFlag = FlagType.Opcode, Point = InOutPoint.InPoint, DataBank = 0x80,
                    DirectPage = 0x2100
                },
                new() {Byte = 0x32, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x9F, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x7E, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0xCA, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0xCA, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new()
                {
                    Byte = 0x10, TypeFlag = FlagType.Opcode, Point = InOutPoint.OutPoint, DataBank = 0x80,
                    DirectPage = 0x2100
                },
                new() {Byte = 0xF4, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x64, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x40, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x64, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x41, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x64, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x42, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x64, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x43, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new()
                {
                    Byte = 0xAE, TypeFlag = FlagType.Opcode, Point = InOutPoint.InPoint, DataBank = 0x80,
                    DirectPage = 0x2100
                },
                new() {Byte = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new()
                {
                    Byte = 0xFC, TypeFlag = FlagType.Opcode, Point = InOutPoint.OutPoint, DataBank = 0x80,
                    DirectPage = 0x2100
                },
                new() {Byte = 0x3A, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new()
                {
                    Byte = 0x4C, TypeFlag = FlagType.Opcode, Point = InOutPoint.EndPoint, DataBank = 0x80,
                    DirectPage = 0x2100
                },
                new() {Byte = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0xC0, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new()
                {
                    Byte = 0x00, TypeFlag = FlagType.Data16Bit, Point = InOutPoint.ReadPoint, DataBank = 0x80,
                    DirectPage = 0x2100
                },
                new() {Byte = 0x00, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x08, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x00, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x10, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x00, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x20, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x00, TypeFlag = FlagType.Data16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new()
                {
                    Byte = 0x44, TypeFlag = FlagType.Pointer16Bit, Point = InOutPoint.ReadPoint, DataBank = 0x80,
                    DirectPage = 0x2100
                },
                new() {Byte = 0x80, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x7B, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x80, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x44, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x81, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0xC4, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x81, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x0A, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x82, TypeFlag = FlagType.Pointer16Bit, DataBank = 0x80, DirectPage = 0x2100},
                new()
                {
                    Byte = 0x08, TypeFlag = FlagType.Opcode, Point = InOutPoint.InPoint, DataBank = 0x80,
                    DirectPage = 0x2100
                },
                new() {Byte = 0x8B, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x4B, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0xAB, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0xE2, TypeFlag = FlagType.Opcode, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x20, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0xC2, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x10, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0xA2, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x1F, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},

                // --------------------------
                // highlighting a particular section here
                // we will use this for unit tests as well.

                // LDA.W Test_Data,X
                new()
                {
                    Byte = 0xBD, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.InPoint, DataBank = 0x80,
                    DirectPage = 0x2100
                },
                new() {Byte = 0x5B, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100}, // Test_Data
                new() {Byte = 0x80, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100}, // Test_Data

                // STA.W $0100,X
                new() {Byte = 0x9D, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x00, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x01, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},

                // DEX
                new() {Byte = 0xCA, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},

                // BPL CODE_80804F
                new()
                {
                    Byte = 0x10, TypeFlag = FlagType.Opcode, MFlag = true, Point = InOutPoint.OutPoint, DataBank = 0x80,
                    DirectPage = 0x2100
                },
                new() {Byte = 0xF7, TypeFlag = FlagType.Operand, DataBank = 0x80, DirectPage = 0x2100},

                // ------------------------------------

                new() {Byte = 0xAB, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x28, TypeFlag = FlagType.Opcode, MFlag = true, DataBank = 0x80, DirectPage = 0x2100},
                new()
                {
                    Byte = 0x60, TypeFlag = FlagType.Opcode, Point = InOutPoint.EndPoint, DataBank = 0x80,
                    DirectPage = 0x2100
                },

                // --------------------------

                new()
                {
                    Byte = 0x45, TypeFlag = FlagType.Data8Bit, Point = InOutPoint.ReadPoint, DataBank = 0x80,
                    DirectPage = 0x2100
                },
                new() {Byte = 0x8D, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x69, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x83, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0xB2, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x99, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x00, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x23, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x01, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0xA3, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0xF8, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x52, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x08, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0xBB, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x29, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x5C, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x32, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0xE7, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x88, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x3C, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x30, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x18, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x9A, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0xB0, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x34, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x8C, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0xDD, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x05, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0xB7, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x83, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x34, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
                new() {Byte = 0x6D, TypeFlag = FlagType.Data8Bit, DataBank = 0x80, DirectPage = 0x2100},
            });
        }
    }
}
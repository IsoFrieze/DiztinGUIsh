using System;
using System.Linq;
using System.Text;
using System.Xml;
using Diz.Core;
using Diz.Core.model;
using Diz.Core.serialization.xml_serializer;
using Diz.Core.util;
using Diz.Test.Utils;
using Diz.Test.Utils.SuperFamiCheckUtil;
using ExtendedXmlSerializer;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests
{
    public static class CartNameTests
    {
        // Bytes for a Cart Name from a SNES header
        // "Marvelous - Mouhitotsu no Takara-jima (Japan).sfc"
        // StartOffset(h): 00007FC0, EndOffset(h): 00007FD4, Length(h): 00000015
        // 21 bytes
        private static byte[] RawRomBytes => new byte[]
        {
            0xCF, 0xB0, 0xB3, 0xDE, 0xAA, 0xD7, 0xBD, 0x20, 0x20, 0x20,
            0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
            0x20
        };

        private static string ExpectedTitleStr => "ﾏｰｳﾞｪﾗｽ".PadRight(RomUtil.LengthOfTitleName);

        // bugfix credit: @LuigiBlood and #diztinguish on Discord
        [Fact]
        public static void TestTitleFilenameConversions()
        {
            RawRomBytes.Length.Should().Be(RomUtil.LengthOfTitleName);

            // convert to UTF8 bytes
            var shiftJisEncoding = ByteUtil.ShiftJisEncoding;
            var utfBytes = Encoding.Convert(shiftJisEncoding, Encoding.UTF8, RawRomBytes);
            utfBytes.Length.Should().Be(35);

            // convert to UTF8 string
            var utfStrParsed = Encoding.UTF8.GetString(utfBytes);
            utfStrParsed.Should().Be(ExpectedTitleStr);

            // convert back to UTF8 bytes
            var utfBytesFromStr = Encoding.UTF8.GetBytes(utfStrParsed);
            utfBytesFromStr.Should().BeEquivalentTo(utfBytes);
            utfBytesFromStr.Length.Should().Be(35);

            // convert back to Shift-JIS
            var shiftJisConvertBytes = Encoding.Convert(Encoding.UTF8, shiftJisEncoding, utfBytesFromStr);
            shiftJisConvertBytes.Should().BeEquivalentTo(RawRomBytes);
        }

        [Fact]
        public static void TestTitleRead()
        {
            var fakeRom = Enumerable
                .Range(0, 0x7FC0)
                .Select(_ => (byte) 0x00)
                .Concat(RawRomBytes)
                .ToArray();

            RomUtil
                .GetCartridgeTitleFromRom(
                    fakeRom,
                    RomUtil.GetRomSettingOffset(RomMapMode.LoRom)
                ).Should().Be(ExpectedTitleStr);
        }

        private class TestRoot
        {
            public string CartTitle { get; set; }
        }

        private static TestRoot Root => new TestRoot {CartTitle = ExpectedTitleStr};

        [Fact]
        public static void TestXmlCycle3()
        {
            var serializer = XmlSerializerSupport.GetSerializer().Create();

            var xmlStr = serializer.Serialize(
                new XmlWriterSettings(),
                Root
            );
            var restoredRoot = serializer.Deserialize<TestRoot>(xmlStr);

            xmlStr.Should().Contain($"CartTitle=\"{ExpectedTitleStr}\"");

            restoredRoot.CartTitle.Should().Be(ExpectedTitleStr);
        }

        [Fact]
        public static void CartNameInHeader()
        {
            // use the sample data to fake a project
            var srcProject = LoadSaveTest.BuildSampleProject2();
            var expectedTitle = SampleRomData.GetSampleUtf8CartridgeTitle();
            TestRomCartTitle(srcProject, expectedTitle);
        }
        
        internal static void TestRomCartTitle(Project project, string expectedTitle)
        {
            ByteUtil.ConvertUtf8ToShiftJisEncodedBytes(project.InternalRomGameName)
                .Should().HaveCount(RomUtil.LengthOfTitleName,
                    "SNES cart name in header must be exactly this many bytes");

            project.Data.CartridgeTitleName
                .Should().Be(project.InternalRomGameName, "it should be identical to the cached name");

            var trimmedTitle = project.InternalRomGameName.TrimEnd(' ');
            trimmedTitle.Should().Be(expectedTitle, "SNES headers are padded with spaces at the end to a fixed size");
        }
        
        [Fact]
        public static void TestCartChecksumInHeader()
        {
            // use the sample data to fake a project
            var srcProject = LoadSaveTest.BuildSampleProject2();
            srcProject.Data.RomChecksum.Should().Be(srcProject.Data.ComputeChecksum(),
                "checksum bytes in the ROM should match the computed checksum");
        }

        // note: you need to put this on your local system for it to work.
        // gotta figure out how to make this portable without running into weirdness.
        const string RomFileName = @"D:\roms\SNES\ct (U) [!].smc";
        
        [FactOnlyIfFilePresent(new[]{SuperFamiCheckTool.Exe, RomFileName})]
        public static void TestFamicheckTool()
        {
            var result = SuperFamiCheckTool.Run(RomFileName);
            result.Complement.Should().Be(0x8773);
            result.Checksum.Should().Be(0x788c);

            // it's stored in the ROM file like this:
            // 73 87 8C 78
        }
        
        [FactOnlyIfFilePresent(new[]{SuperFamiCheckTool.Exe, RomFileName})]
        public static void TestInternalChecksumVsExternal()
        {
            var result = SuperFamiCheckTool.Run(RomFileName);
            result.Complement.Should().Be(0x8773);
            result.Checksum.Should().Be(0x788c);
            (result.Complement + result.Checksum).Should().Be(0xFFFF);
            
            const uint expected4ByteChecksums = 0x788C8773;
            result.AllCheckBytes.Should().Be(expected4ByteChecksums);
            
            var project = ImportUtils.ImportRomAndCreateNewProject(RomFileName);
            project.Should().NotBeNull("project should have loaded successfully");
            project.Data.GetRomByte(0xFFDC).Should().Be(0x73); // complement 1
            project.Data.GetRomByte(0xFFDD).Should().Be(0x87); // complement 2
            project.Data.GetRomByte(0xFFDE).Should().Be(0x8C); // checksum 1
            project.Data.GetRomByte(0xFFDF).Should().Be(0x78); // checksum 2

            var complement = project.Data.GetRomWord(0xFFDC);
            complement.Should().Be(0x8773); // complement 16bit

            var checksum = project.Data.GetRomWord(0xFFDE);
            checksum.Should().Be(0x788c); // checksum 16bit
            
            (complement + checksum).Should().Be(0xFFFF);
            
            project.Data.GetRomDoubleWord(0xFFDC).Should().Be((int) expected4ByteChecksums); // complement 16bit
            project.Data.RomCheckSumsFromRomBytes.Should().Be(expected4ByteChecksums);
            project.InternalCheckSum.Should().Be(expected4ByteChecksums);

            result.Complement.Should().Be((uint) complement);
            result.Checksum.Should().Be((uint) checksum);

            project.Data.ComputeChecksum().Should().Be((ushort)checksum);
            project.Data.ComputeIsChecksumValid().Should().Be(true);

            var firstByte = project.Data.RomBytes[0x00].Rom;
            firstByte.Should().NotBe(0);
            project.Data.RomBytes[0x00].Rom = 0;
            project.Data.ComputeIsChecksumValid().Should().Be(false);
            project.Data.FixChecksum();
            project.Data.ComputeIsChecksumValid().Should().Be(true);
            
            project.Data.RomBytes[0x00].Rom = firstByte;
            project.Data.ComputeIsChecksumValid().Should().Be(false);
            project.Data.FixChecksum();
            project.Data.ComputeIsChecksumValid().Should().Be(true);
            
            // SNES docs dictate:
            // 15. Complement Check (0xFFDC, 0xFFDD)
            // 16. Check Sum (0xFFDE, 0xFFDF)

            // in the actual ROM file, it appears like this (remember: little endian for SNES)
            // complement   checksum
            // 73 87        8C 78
        }
    }
}
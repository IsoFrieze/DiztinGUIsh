using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Diz.Core;
using Diz.Core.model;
using Diz.Core.serialization.xml_serializer;
using Diz.Core.util;
using ExtendedXmlSerializer;
using FluentAssertions;
using IX.StandardExtensions;
using Xunit;

namespace Diz.Test
{
    public static class CartNameTests
    {
        // Bytes for a Cart Name from a SNES header
        // "Marvelous - Mouhitotsu no Takara-jima (Japan).sfc"
        // StartOffset(h): 00007FC0, EndOffset(h): 00007FD4, Length(h): 00000015 
        // 21 bytes
        private static byte[] RawRomBytes => new byte[] {
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
            var shiftJisEncoding = Encoding.GetEncoding(932);
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
                .Select(x => (byte)0x00)
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

        public static void TestRomCartTitle(Project project, string expectedTitle)
        {
            ByteUtil.ConvertUtf8ToShiftJisEncodedBytes(project.InternalRomGameName)
                .Should().HaveCount(RomUtil.LengthOfTitleName,
                    "SNES cart name in header must be exactly this many bytes");

            project.Data.CartridgeTitleName
                .Should().Be(project.InternalRomGameName, "it should be identical to the cached name");

            var trimmedTitle = project.InternalRomGameName.TrimEnd(' ');
            trimmedTitle.Should().Be(expectedTitle, "SNES headers are padded with spaces at the end to a fixed size");
        }
    }
}
using System.Collections.Generic;
using System.IO;
using System.Text;
using Diz.Core;
using Diz.Core.export;
using Diz.Core.model;
using Diz.Core.util;
using ExtendedXmlSerializer;
using Moq;
using Moq.Protected;
using Xunit;

namespace Diz.Test
{
    public sealed class LogCreatorTests
    {
        [Fact]
        public void TestEqualsButNotCompareByte()
        {
            var settings = new LogWriterSettings();
            settings.SetDefaults();
            settings.structure = LogCreator.FormatStructure.SingleFile;

            var expectedLabels = new Dictionary<int, string>()
            {
                {0, "Emulation_RESET"},
                {10, "FastRESET"},
                {50, "}Test_Indices"},
                {58, "Pointer_Table"},
                {68, "First_Routine"},
                {91, "Test_Data"},
                {32767, "}UNREACH_80FFFF"},
                {21, "CODE_808015"},
                {16384, "}UNREACH_80C000"},
                {123, "UNREACH_80807B"},
                {324, "UNREACH_808144"},
                {452, "UNREACH_8081C4"},
                {522, "UNREACH_80820A"},
                {79, "CODE_80804F"}
            };

            var mock = new Mock<LogCreator>();
            mock.Protected()
                .Setup("RestoreUnderlyingDataLabels")
                .Callback(() =>
                {
                    // int x = 3;
                });

            var result = RomUtil.GetSampleAssemblyOutput(settings);
        }
    }
}
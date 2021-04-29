using Diz.Core.model.byteSources;
using Diz.Core.model.snes;
using Diz.Core.util;
using Diz.Test.TestData;
using Diz.Test.Utils;
using Xunit;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace Diz.Test.Tests.RomInterfaceTests
{
    public static class AccessTests
    {
        [Fact]
        public static void DataConvertSnesToPcHiRom()
        {
            var data = TinyHiRomSample.TinyHiRomWithExtraLabel;

            // note: this doesn't quite cover all the range if the offset is greater than the #bytes
            Assert.Equal(0x000002, data.ConvertSnesToPc(0xC00002));
            Assert.Equal(0xC00000, data.ConvertPCtoSnes(0x000000));
        }
        
        [Fact]
        public static void SanityTestSizingBase()
        {
            SizeCheck(TinyHiRomSample.TinyHiRom);
        }
        
        [Fact]
        public static void SanityTestSizing()
        {
            SizeCheck(TinyHiRomSample.TinyHiRomWithExtraLabel);
        }

        private static void SizeCheck(Data data)
        {
            Assert.Equal(3, data.GetRomSize());
            Assert.Equal(3, data.RomByteSource.Bytes.Count);

            Assert.Equal(0x1000000, data.SnesAddressSpace.Bytes.Count);
        }

        [Fact]
        public static void TestAccess()
        {
            var data = TinyHiRomSample.TinyHiRom;
            Assert.Equal(3, data.GetRomSize());

            AssertRomByteEqual(0x8D, data, 0);
            AssertRomByteEqual(0x16, data, 1);
            AssertRomByteEqual(0x21, data, 2);
        }

        [Fact]
        public static void TestParentsSetupCorrectly()
        {
            var data = TinyHiRomSample.TinyHiRom;
            
            data.SnesAddressSpace.Bytes[0xC00000] = new ByteEntry
            {
                Byte = 0xEE,
            };
            
            TestParentByteSourceRefs(0xEE, 0xC00000, data.SnesAddressSpace);
            TestParentByteSourceRefs(0x8D, 0x0, data.RomByteSource);
        }

        private static void TestParentByteSourceRefs(int expectedByteVal, int index, ByteSource expectedByteSource)
        {
            var byteOffsetData = expectedByteSource.Bytes[index];
            var b = byteOffsetData?.Byte;

            Assert.NotNull(b);
            Assert.NotNull(byteOffsetData);
            
            Assert.Equal(expectedByteVal, b.Value);
            // TODO // Assert.Same(expectedByteSource.Bytes, byteOffsetData.ParentByteSource.Bytes);
        }
        
        private static void AssertRomByteEqual(byte expectedByteVal, Data data, int pcOffset)
        {
            // test via all three access methods
            AssertSnesByteIs(expectedByteVal, data, pcOffset);
            AssertRomByteIs(expectedByteVal, data, pcOffset);
            AssertRomByteIsViaHelper(expectedByteVal, data, pcOffset);
        }

        // access via older helper interface
        private static void AssertRomByteIsViaHelper(byte expectedByteVal, Data data, int pcOffset)
        {
            Assert.Equal(expectedByteVal, data.GetRomByte(pcOffset));
        }

        // access via snes interface
        private static void AssertSnesByteIs(byte expectedByteVal, Data data, int pcOffset)
        {
            var snesAddress = data.ConvertPCtoSnes(pcOffset);
            Assert.NotEqual(-1, snesAddress);
            var rByte = ByteGraphUtil.BuildFlatDataFrom(data.SnesAddressSpace, snesAddress)
                .Byte; // TODO refactor: Make this be ByteSource.GetByte()
            Assert.NotNull(rByte);
            Assert.Equal(expectedByteVal, rByte.Value);
        }

        // access via Rom mapping interface
        private static void AssertRomByteIs(byte expectedByteVal, Data data, int pcOffset)
        {
            var rByte = ByteGraphUtil.BuildFlatDataFrom(data.RomByteSource, pcOffset).Byte;
            Assert.NotNull(rByte);
            Assert.Equal(expectedByteVal, rByte.Value);
        }

        [Fact]
        public static void TestLabels()
        {
            var data = TinyHiRomSample.TinyHiRomWithExtraLabel;
            Assert.Equal("SNES_VMADDL", data.Labels.GetLabelName(0x2116));
            Assert.Equal("", data.Labels.GetLabelName(0x2119)); // bogus address
            // Assert.Equal("SNES_VMADDL", data.Labels.GetLabelName(0x7E2116)); // later, we need mirrors like this to ALSO work for WRAM
        }

        [Fact]
        public static void IndirectAddress1()
        {
            var data = TinyHiRomSample.TinyHiRomWithExtraLabel;
            Assert.Equal(0x002116, data.GetIntermediateAddressOrPointer(0));
        }

        [Fact]
        public static void TestWhenNoIaPresent()
        {
            var sampleData = SampleRomData.SampleData;
            const int offset = 0x1C1F;
            var result = sampleData.GetIntermediateAddressOrPointer(offset);
            Assert.Equal(result, -1);
        }

        [Fact]
        public static void IntermediateAddress2()
        {
            var data = TinyHiRomSample.TinyHiRomWithExtraLabel;
            data.RomByteSource.Bytes[0].DataBank = 0x7E;
            Assert.Equal(0x7E2116, data.GetIntermediateAddressOrPointer(0));
        }

        // TODO: FIXME: wont work til we fix the assembly export generation
        [Fact(Skip = "not yet working")]
        // [Fact]
        public static void RunTestRom()
        {
            // C# ROM -> Assembly Text 
            var exportAssembly = LogWriterHelper.ExportAssembly(TinyHiRomSample.TinyHiRomWithExtraLabel).OutputStr;

            // Assembly Text -> Asar -> SFC file
            var bytes = AsarRunner.AssembleToRom(exportAssembly);

            Assert.Equal(3, bytes.Count);

            Assert.Equal(0x8D, bytes[0]);
            Assert.Equal(0x16, bytes[1]);
            Assert.Equal(0x21, bytes[2]);
        }
    }
}
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.util;
using Diz.Test.TestData;
using Xunit;

namespace Diz.Test.Tests.ByteGraphTests
{
    public static class ByteGraphTests
    {
        [Fact]
        public static void BuildBasicGraph()
        {
            var (srcData, data) = TinyHiRomCreator.CreateSampleRomByteSourceElements();
            
            var snesAddress = data.ConvertPCtoSnes(0);
            var graph = ByteGraphUtil.BuildFullGraph(data.SnesAddressSpace, snesAddress);

            // ok, this is tricky, pay careful attention.
            // we got a graph back from the SNES address space that represents
            // stored in each of the 2 layers:
            // layer 1: the SNES address space
            // layer 2: the ROM
            //
            // we're using Sparse byte storage, which means that unless something needs to be stored
            // in the SNES address space (and NOT with the ROM), then that entry will be null.
            //
            // what we expect is this resulting graph:
            // - root node: ByteOffsetData from SNES address space @ offset 0xC00000.
            //               THIS *should be NULL* because there's nothing stored there.
            //   - child node 1: A ByteOffsetData from the ROM. this WILL have data because we loaded a ROM.
            //
            // remember, this is showing a graph of the underlying data, and not flattened into something useful for
            // looking at it as a condensed, flat view.
            
            Assert.NotNull(graph);
            Assert.Null(graph.ByteEntry);        // snes address space result
            
            Assert.NotNull(graph.Children);     // 1 child = the ROM ByteSource
            Assert.Single(graph.Children);

            var childNodeFromRom = graph.Children[0];   // get the node that represents the
                                                        // next (and only) layer down, the ROM
            Assert.NotNull(childNodeFromRom);
            Assert.Null(childNodeFromRom.Children);
            Assert.NotNull(childNodeFromRom.ByteEntry);
            Assert.NotNull(childNodeFromRom.ByteEntry.Byte);
            Assert.Equal(0x8D, childNodeFromRom.ByteEntry.Byte.Value);
            
            // TODO // Assert.Same(data.RomByteSource.Bytes, childNodeFromRom.ByteEntry.ParentByteSource.Bytes);
            Assert.Same(srcData[0], childNodeFromRom.ByteEntry);
        }
        
         [Fact]
        public static void TraverseChildren()
        {
            var (_, data) = TinyHiRomCreator.CreateSampleRomByteSourceElements();
            
            var snesAddress = data.ConvertPCtoSnes(0);

            var snesByte = data.SnesAddressSpace.Bytes[snesAddress];
            var romByte = data.RomByteSource.Bytes[0];
            
            Assert.Null(snesByte);
            Assert.NotNull(romByte);
            
            Assert.NotNull(romByte.Annotations.Parent);
            Assert.Equal(3, romByte.Annotations.Count);
            var opcodeAnnotation = romByte.GetOneAnnotation<OpcodeAnnotation>();
            Assert.NotNull(opcodeAnnotation);
            Assert.NotNull(opcodeAnnotation.Parent);
            Assert.Equal(opcodeAnnotation.Parent, romByte);
            
            var graph = ByteGraphUtil.BuildFullGraph(data.SnesAddressSpace, snesAddress);

            var flattenedNode = ByteGraphUtil.BuildFlatDataFrom(graph);
            
            Assert.NotNull(flattenedNode);
            Assert.NotNull(flattenedNode.Byte);
            Assert.Equal(0x8D, flattenedNode.Byte.Value);
            Assert.Equal(3, flattenedNode.Annotations.Count);
            
            // make sure the parent hasn't changed after we built our flattened node
            Assert.Equal(opcodeAnnotation.Parent, romByte);
        }

        [Fact]
        public static void TestGetFlatByteNonPadded()
        {
            // Get a byte from the sample data that is a real (i.e. non-padded) byte
            
            var sampleData = SampleRomData.SampleData;
            const int romOffset = 0x0A;
            const int snesAddress = 0x808000 + romOffset;
            
            var flatByte = ByteGraphUtil.BuildFlatDataFrom(sampleData.SnesAddressSpace, snesAddress);
            Assert.NotNull(flatByte);
            Assert.NotNull(flatByte.Byte);
            Assert.Equal(0xC2, flatByte.Byte.Value);
        }
        
        [Fact]
        public static void TestGetFlatByteInRange()
        {
            // Get a byte from the sample data that is a padded (i.e. for sample ROMs we can create them with a different
            // size than their source data. in this one, we pad the ROM from a few hundred bytes and add zero'd bytes
            // until we reach 32k bytes). This test is mostly testing that we built the sample data correctly, in real
            // world scenarios, this would never fail because we're not doing padding.
            
            var sampleData = SampleRomData.SampleData;
            const int romOffset = 0xEB;
            const int snesAddress = 0x808000 + romOffset;

            Assert.True(romOffset >= sampleData.OriginalRomSizeBeforePadding);
            
            var flatByte = ByteGraphUtil.BuildFlatDataFrom(sampleData.SnesAddressSpace, snesAddress);
            Assert.NotNull(flatByte);
            Assert.NotNull(flatByte.Byte);
            Assert.Equal(0x00, flatByte.Byte.Value);
        }
    }
}
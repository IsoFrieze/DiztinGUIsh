using Diz.Core.util;

namespace Diz.Core.model
{
    public class ByteSourceMapping
    {
        // how this byte source maps into its parent region
        // example: Parent region is SNES address space, ByteSource is ROM.  Map bytesource's [0x0...0xFFFFFF] offset into parent's HiRom mapping [0xC00000 + 0xFFFFFF]
        public RegionMapping RegionMapping { get; init; } = new RegionMappingDirect();

        public ByteSource ByteSource { get; init; }
    }

    public abstract class RegionMapping
    {
        public abstract int ConvertSourceToDestination(int source, ByteSource byteSource);
        public abstract int ConvertDestinationToSource(int destination, ByteSource byteSource);
    }

    public class RegionMappingDirect : RegionMapping
    {
        public override int ConvertSourceToDestination(int source, ByteSource byteSource) => source;
        public override int ConvertDestinationToSource(int destination, ByteSource byteSource) => destination;
    }
    
    // TODO: eventually, make this map the entire SNES address space and not just the ROM area.
    // underlying stuff in RomUtil will need an update to do that.
    public class RegionMappingSnesRom : RegionMapping
    {
        public RomMapMode RomMapMode { get; init; }
        public RomSpeed RomSpeed { get; init; }
        public override int ConvertSourceToDestination(int source, ByteSource byteSource)
        {
            return RomUtil.ConvertPCtoSnes(source, RomMapMode, RomSpeed);
        }

        public override int ConvertDestinationToSource(int destination, ByteSource byteSource)
        {
            // TODO: eventually, make this map the entire SNES address space and not just the ROM area.
            // underlying stuff in RomUtil will need an update to do that.
            return RomUtil.ConvertSnesToPc(destination, RomMapMode, byteSource.Bytes.Count);
        }
    }
}
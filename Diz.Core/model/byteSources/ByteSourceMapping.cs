using Diz.Core.util;

namespace Diz.Core.model.byteSources
{
    public class ByteSourceMapping
    {
        // how this byte source maps into its parent region
        // example: Parent region is SNES address space, ByteSource is ROM.  Map bytesource's [0x0...0xFFFFFF] offset into parent's HiRom mapping [0xC00000 + 0xFFFFFF]
        public RegionMapping RegionMapping { get; init; } = new RegionMappingDirect();

        public ByteSource ByteSource { get; init; }

        public int ConvertIndexFromParentToChild(int parentIndex)
        {
            return RegionMapping.ConvertIndexFromParentToChild(parentIndex, ByteSource);
        }

        public int ConvertIndexFromChildToParent(int childIndex)
        {
            return RegionMapping.ConvertIndexFromChildToParent(childIndex, ByteSource);
        }
    }

    public abstract class RegionMapping
    {
        public abstract int ConvertIndexFromParentToChild(int parentIndex, ByteSource byteSource);
        public abstract int ConvertIndexFromChildToParent(int childIndex, ByteSource byteSource);
    }

    public class RegionMappingDirect : RegionMapping
    {
        public override int ConvertIndexFromParentToChild(int parentIndex, ByteSource byteSource) => parentIndex;
        public override int ConvertIndexFromChildToParent(int childIndex, ByteSource byteSource) => childIndex;
    }
    
    public class RegionMappingSnesRom : RegionMapping
    {
        public RomMapMode RomMapMode { get; init; }
        public RomSpeed RomSpeed { get; init; }
        
        // convert SNES address -> ROM address. return -1 if not mapped
        public override int ConvertIndexFromParentToChild(int parentIndex, ByteSource byteSource)
        {
            return RomUtil.ConvertSnesToPc(parentIndex, RomMapMode, byteSource.Bytes.Count);
        }

        // convert ROM offset to SNES address
        public override int ConvertIndexFromChildToParent(int childIndex, ByteSource byteSource)
        {
            // given a Rom offset, convert to an address in SNES address space
            
            // TODO: eventually, make this map the entire SNES address space and not just the ROM area.
            // underlying stuff in RomUtil will need an update to do that.
            return RomUtil.ConvertPCtoSnes(childIndex, RomMapMode, RomSpeed);
        }
    }
}
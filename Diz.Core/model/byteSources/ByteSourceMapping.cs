using System;
using Diz.Core.util;
using JetBrains.Annotations;

namespace Diz.Core.model.byteSources
{
    public class ByteSourceMapping
    {
        // how this byte source maps into its parent region
        // example: Parent region is SNES address space, ByteSource is ROM.  Map bytesource's [0x0...0xFFFFFF] offset into parent's HiRom mapping [0xC00000 + 0xFFFFFF]
        public RegionMapping RegionMapping { get; init; } = new RegionMappingDirect();

        public ByteSource ByteSource { get; init; }

        [PublicAPI] public int ConvertIndexFromParentToChild(int parentIndex)
        {
            return RegionMapping.ConvertIndexFromParentToChild(parentIndex, ByteSource);
        }

        [PublicAPI] public int ConvertIndexFromChildToParent(int childIndex)
        {
            return RegionMapping.ConvertIndexFromChildToParent(childIndex, ByteSource);
        }
        
        #region Equality

        protected bool Equals(ByteSourceMapping other)
        {
            return Equals(RegionMapping, other.RegionMapping) 
                   && Equals(ByteSource, other.ByteSource);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ByteSourceMapping) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(RegionMapping, ByteSource);
        }

        #endregion
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

        // convert SNES address -> ROM address. return -1 if it doesn't have an equivalent address
        public override int ConvertIndexFromParentToChild(int parentIndex, ByteSource byteSource)
        {
            // NOTE: this function can do more than just ROM mapping, but, it might not be tested
            // for our new use of it here. if in doubt, write some unit tests
            return RomUtil.ConvertSnesToPc(parentIndex, RomMapMode, byteSource.Bytes.Count);
        }

        // convert ROM offset to SNES address
        public override int ConvertIndexFromChildToParent(int childIndex, ByteSource byteSource)
        {
            // NOTE: this function can do more than just ROM mapping, but, it might not be tested
            // for our new use of it here. if in doubt, write some unit tests
            return RomUtil.ConvertPCtoSnes(childIndex, RomMapMode, RomSpeed);
        }

        #region Equality

        protected bool Equals(RegionMappingSnesRom other)
        {
            return RomMapMode == other.RomMapMode && RomSpeed == other.RomSpeed;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RegionMappingSnesRom) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int) RomMapMode, (int) RomSpeed);
        }
        #endregion
    }
}
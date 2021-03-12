using System.Threading;
using DiztinGUIsh;

namespace Diz.Core.model
{
    // represents metadata associated with each byte of the ROM
    // RomByteData is just the data itself with as little associated fluff as possible 
    public class RomByteData : DizDataModel
    {
        // never modify directly. only go through the public fields
        private byte rom;
        private byte dataBank;
        private int directPage;
        private bool xFlag;
        private bool mFlag;
        private FlagType typeFlag = FlagType.Unreached;
        private Architecture arch = Architecture.Cpu65C816;
        private InOutPoint point = InOutPoint.None;

        // holds the original byte from the source ROM
        public byte Rom
        {
            get => rom;
            set => SetField(ref rom, value);
        } // never serialize this, read from ROM on load. (for copyright reasons)

        // everything else is metadata that describes the source byte above
        public byte DataBank
        {
            get => dataBank;
            set => SetField(ref dataBank, value);
        }

        public int DirectPage
        {
            get => directPage;
            set => SetField(ref directPage, value);
        }

        public bool XFlag
        {
            get => xFlag;
            set => SetField(ref xFlag, value);
        }

        public bool MFlag
        {
            get => mFlag;
            set => SetField(ref mFlag, value);
        }

        public FlagType TypeFlag
        {
            get => typeFlag;
            set => SetField(ref typeFlag, value);
        }

        public Architecture Arch
        {
            get => arch;
            set => SetField(ref arch, value);
        }

        public InOutPoint Point
        {
            get => point;
            set => SetField(ref point, value);
        }

        // don't serialize. cached copy of our offset in parent collection
        public int Offset { get; private set; } = -1;

        public void SetCachedOffset(int offset)
        {
            // not in love with this or that we're caching it. would be cool if we didn't
            // need Offset, or could just derive this (quickly) from the base list.
            Offset = offset;
        }


        #region Equality
        protected bool Equals(RomByte other)
        {
            return Rom == other.Rom && EqualsButNoRomByte(other);
        }

        public bool EqualsButNoRomByte(RomByte other)
        {
            return DataBank == other.DataBank && DirectPage == other.DirectPage && XFlag == other.XFlag && MFlag == other.MFlag && TypeFlag == other.TypeFlag && Arch == other.Arch && Point == other.Point;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == this.GetType() && Equals((RomByte)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Rom.GetHashCode();
                hashCode = (hashCode * 397) ^ DataBank.GetHashCode();
                hashCode = (hashCode * 397) ^ DirectPage;
                hashCode = (hashCode * 397) ^ XFlag.GetHashCode();
                hashCode = (hashCode * 397) ^ MFlag.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)TypeFlag;
                hashCode = (hashCode * 397) ^ (int)Arch;
                hashCode = (hashCode * 397) ^ (int)Point;
                return hashCode;
            }
        }
        #endregion
    }

    // wrap RomByteData with extra helper stuff like locking
    public class RomByte : RomByteData
    {
        // note: our thread safety isn't comprehensive in this project yet.
        // be careful with this if you're doing anything clever, especially writing.
        public ReaderWriterLockSlim Lock { get; protected set; } = new ReaderWriterLockSlim();
    }
}

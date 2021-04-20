namespace Diz.Core.model.byteSources
{
    /*public interface IReadableByteEntry
    {
        byte? Byte { get; }
        IReadOnlyList<Annotation> Annotations { get; }

        ByteSource ParentByteSource { get; }
        int ParentIndex { get; }
        
        byte DataBank{ get; }
        int DirectPage { get; }
        bool XFlag { get; }
        bool MFlag{ get; }
        Architecture Arch { get; }
        FlagType TypeFlag{ get; }
        InOutPoint Point { get; }
        bool Equals(object obj);
        int GetHashCode();
        
        T GetOneAnnotation<T>() where T : Annotation;
        ReaderWriterLockSlim Lock { get; }
    }*/

    // mostly, adds helper methods to ByteEntry, which is what does the heavy lifting
    public partial class ByteEntry // IReadableByteEntry
    {
        public ByteEntry() {}

        // if null, it means caller either needs to dig one level deeper in
        // parent container to find the byte value, or, there is no data
        public byte? Byte
        {
            get => GetOneAnnotation<ByteAnnotation>()?.Byte;
            set
            {
                if (value == null)
                    RemoveOneAnnotationIfExists<ByteAnnotation>();
                else
                    GetOrCreateAnnotation<ByteAnnotation>().Byte = (byte)value;
            }
        }

        public byte DataBank
        {
            get => GetOneOpcodeAnnotation()?.DataBank ?? default;
            set => GetOrCreateOpcodeAnnotation().DataBank = value;
        }

        public int DirectPage
        {
            get => GetOneOpcodeAnnotation()?.DirectPage ?? default;
            set => GetOrCreateOpcodeAnnotation().DirectPage = value;
        }

        public bool XFlag
        {
            get => GetOneOpcodeAnnotation()?.XFlag ?? default;
            set => GetOrCreateOpcodeAnnotation().XFlag = value;
        }

        public bool MFlag
        {
            get => GetOneOpcodeAnnotation()?.MFlag ?? default;
            set => GetOrCreateOpcodeAnnotation().MFlag = value;
        }

        public Architecture Arch
        {
            get => GetOneOpcodeAnnotation()?.Arch ?? default;
            set => GetOrCreateOpcodeAnnotation().Arch = value;
        }

        public FlagType TypeFlag
        {
            get => GetOneAnnotation<MarkAnnotation>()?.TypeFlag ?? default;
            set => GetOrCreateAnnotation<MarkAnnotation>().TypeFlag = value;
        }

        public InOutPoint Point
        {
            get => GetOneAnnotation<BranchAnnotation>()?.Point ?? default;
            set => GetOrCreateAnnotation<BranchAnnotation>().Point = value;
        }
        
        // IReadOnlyList<Annotation> IReadableByteEntry.Annotations => Annotations;
        
        private OpcodeAnnotation GetOneOpcodeAnnotation() => GetOneAnnotation<OpcodeAnnotation>();
        private OpcodeAnnotation GetOrCreateOpcodeAnnotation() => GetOrCreateAnnotation<OpcodeAnnotation>();
    }
}
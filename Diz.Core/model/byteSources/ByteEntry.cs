using System.Xml.Serialization;

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
        #region Helper Methods

        // if null, it means caller either needs to dig one level deeper in
        // parent container to find the byte value, or, there is no data
        [XmlIgnore]
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

        [XmlIgnore]
        public byte DataBank
        {
            get => GetOneOpcodeAnnotation()?.DataBank ?? default;
            set => GetOrCreateOpcodeAnnotation().DataBank = value;
        }

        [XmlIgnore]
        public int DirectPage
        {
            get => GetOneOpcodeAnnotation()?.DirectPage ?? default;
            set => GetOrCreateOpcodeAnnotation().DirectPage = value;
        }

        [XmlIgnore]
        public bool XFlag
        {
            get => GetOneOpcodeAnnotation()?.XFlag ?? default;
            set => GetOrCreateOpcodeAnnotation().XFlag = value;
        }

        [XmlIgnore]
        public bool MFlag
        {
            get => GetOneOpcodeAnnotation()?.MFlag ?? default;
            set => GetOrCreateOpcodeAnnotation().MFlag = value;
        }

        [XmlIgnore]
        public Architecture Arch
        {
            get => GetOneOpcodeAnnotation()?.Arch ?? default;
            set => GetOrCreateOpcodeAnnotation().Arch = value;
        }

        [XmlIgnore]
        public FlagType TypeFlag
        {
            get => GetOneAnnotation<MarkAnnotation>()?.TypeFlag ?? default;
            set => GetOrCreateAnnotation<MarkAnnotation>().TypeFlag = value;
        }

        [XmlIgnore]
        public InOutPoint Point
        {
            get => GetOneAnnotation<BranchAnnotation>()?.Point ?? default;
            set => GetOrCreateAnnotation<BranchAnnotation>().Point = value;
        }
        
        // IReadOnlyList<Annotation> IReadableByteEntry.Annotations => Annotations;
        
        private OpcodeAnnotation GetOneOpcodeAnnotation() => GetOneAnnotation<OpcodeAnnotation>();
        private OpcodeAnnotation GetOrCreateOpcodeAnnotation() => GetOrCreateAnnotation<OpcodeAnnotation>();

        #endregion
    }
}
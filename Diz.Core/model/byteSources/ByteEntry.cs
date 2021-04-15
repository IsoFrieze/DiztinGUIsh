using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Diz.Core.model.byteSources
{
    public interface IReadOnlyByteEntry
    {
        byte? Byte { get; }
        IReadOnlyList<Annotation> Annotations { get; }

        ByteSource Container { get; }
        int ContainerOffset { get; }
        
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
    }
    
    // JUST holds the data. no traversal.
    public class ByteEntry : IReadOnlyByteEntry
    {
        // if null, it means caller either needs to dig one level deeper in parent container to find the byte value, or, there is no data
        public byte? Byte { get; set; }

        // Note: don't allocate this immediately, wait for it to be used. we will have millions of them per ROM
        public AnnotationCollection Annotations
        {
            get => annotations;
            set
            {
                if (annotations != null)
                    annotations.Parent = null;
                
                annotations = value;
                
                if (annotations != null)
                    annotations.Parent = this;
            }
        }

        IReadOnlyList<Annotation> IReadOnlyByteEntry.Annotations => Annotations;

        public AnnotationCollection GetOrCreateAnnotationsList()
        {
            return Annotations ??= new AnnotationCollection();
        }

        protected internal ByteStorage ByteStorageContainer { get; internal set; }
        public ByteSource Container => ByteStorageContainer?.ParentContainer;
        public int ContainerOffset  { get; internal set; }

        // --------------------------------------------------------------------------------
        // temporary annotation access stuff. remove all this eventually after further refactoring is complete.
        // this is just helper stuff as this stuff gets migrated into Data/Annotations classes
        // -------------------------------------------------------------------------
        
        private OpcodeAnnotation GetOneOpcodeAnnotation() => GetOneAnnotation<OpcodeAnnotation>();
        private OpcodeAnnotation GetOrCreateOpcodeAnnotation() => GetOrCreateAnnotation<OpcodeAnnotation>();
        
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

        // -------------------------------------------------------------------------
        // end temporary stuff
        protected bool Equals(ByteEntry other)
        {
            return Byte == other.Byte && AnnotationsEqual(other) && Equals(Container, other.Container) && ContainerOffset == other.ContainerOffset;
        }

        protected bool AnnotationsEqual(ByteEntry other)
        {
            // considered equal if one or the other is null AND the other is non-null but zero-length
            if (Annotations == null || other?.Annotations == null)
            {
                var ourCount = Annotations?.Count ?? 0;
                var theirCount = other?.Annotations?.Count ?? 0;

                return ourCount == 0 && theirCount == 0;
            }

            return Annotations.Count == other.Annotations.Count && 
                   GetAnnotationEnumeratorForCompare()
                       .SequenceEqual(
                           other.GetAnnotationEnumeratorForCompare());
        }

        protected IEnumerable<Annotation> GetAnnotationEnumeratorForCompare()
        {
            return Annotations?.OrderBy(x => x.GetType().ToString()).ThenBy(x => x);
        }

        // helpers:
        public T GetOneAnnotation<T>() where T : Annotation
        {
            if (Annotations == null)
                return null;
            
            T ret = null;
            foreach (var annotation in Annotations)
            {
                if (annotation.GetType() != typeof(T))
                    continue;

                if (ret != null)
                    throw new InvalidDataException("Found multiple annotations when we required to find exactly 0 or 1");

                ret = (T)annotation;
            }

            return ret;
        }
        
        public T GetOrCreateAnnotation<T>() where T : Annotation, new()
        {
            var existing = GetOneAnnotation<T>();
            if (existing != null)
                return existing;

            var newItem = new T();

            AddAnnotation(newItem);
            return newItem;
        }
        
        public void AddAnnotation(Annotation newAnnotation)
        {
            GetOrCreateAnnotationsList().Add(newAnnotation);
        }
        
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ByteEntry) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Byte, Annotations, Container, ContainerOffset);
        }

        // note: our thread safety isn't comprehensive in this project yet.
        // be careful with this if you're doing anything clever, especially writing.
        // TODO: instead of doing this, see if ConcurrentBag or similar classes for the container itself would work?
        public ReaderWriterLockSlim Lock => _lock ??= new ReaderWriterLockSlim();
        private ReaderWriterLockSlim _lock;
        private AnnotationCollection annotations;
    }
}

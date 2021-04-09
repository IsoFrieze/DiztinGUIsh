using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Diz.Core.model.byteSources
{
    // JUST holds the data. no traversal.
    public class ByteOffsetData
    {
        // if null, it means caller either needs to dig one level deeper in parent container to find it, or, there is no data
        public byte? Byte { get; set; }

        private List<Annotation> annotations;
        public List<Annotation> Annotations
        {
            // optimization: create on first use only, since we may have lots of empty lists
            get => annotations ??= new List<Annotation>();
            set => annotations = value;
        }

        public ByteSource Container { get; internal set; }
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
        protected bool Equals(ByteOffsetData other)
        {
            return Byte == other.Byte && AnnotationsEqual(other.Annotations) && Equals(Container, other.Container) && ContainerOffset == other.ContainerOffset;
        }

        protected bool AnnotationsEqual(List<Annotation> otherAnnotations)
        {
            return Annotations.OrderBy(x => x.GetType().ToString()).ThenBy(x => x)
                .SequenceEqual(
               otherAnnotations.OrderBy(x => x.GetType().ToString()).ThenBy(x => x));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ByteOffsetData) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Byte, Annotations, Container, ContainerOffset);
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
            
            Annotations.Add(newItem);
            return newItem;
        }

        // note: our thread safety isn't comprehensive in this project yet.
        // be careful with this if you're doing anything clever, especially writing.
        //
        // TODO: instead of doing this, see if ConcurrentBag or similar classes would work.
        public ReaderWriterLockSlim Lock => _lock ??= new ReaderWriterLockSlim();
        private ReaderWriterLockSlim _lock;
    }
}

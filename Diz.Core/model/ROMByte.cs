using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Diz.Core.model
{
    // JUST holds the data. no traversal.
    public class ByteOffsetData
    {
        // if null, it means caller either needs to dig one level deeper in parent container to find it, or, there is no data
        public byte? Byte { get; set; }

        public List<Annotation> Annotations { get; set; }

        public ByteSource Container { get; init; }

        public int ContainerOffset { get; init; } = -1;


        // temporary stuff. remove all this eventually.

        // this is just helper sutff as this stuff gets migrated into Data/Annotations classes

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
            if (Annotations == null)
                Annotations = new List<Annotation>();
            
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

    // represent a node of a per-byte graph through the mappings of various ByteSources
    public class ByteOffsetDataNode
    {
        public ByteOffsetData Data { get; set; }
        public List<ByteOffsetDataNode> Children { get; set; }

        // Simplified graph traversal utility.
        //
        // after graph traversal has happened, collapse the graph (of which this node is the root node) into
        // one ByteOffsetData object.
        //
        // Annotations will be combined together into one list.
        // If there are multiple 'byte' at different children, then we'll pick the most recent one.
        //
        // If you need anything more advanced than this, parse it yourself.
        public ByteOffsetData ResolveToOne(ByteOffsetData dataBeingConstructed = null)
        {
            dataBeingConstructed ??= new ByteOffsetData
            {
                ContainerOffset = Data.ContainerOffset,
                Container = Data.Container,
                Annotations = new List<Annotation>()
            };

            // traverse any child nodes first.
            foreach (var childNode in Children)
            {
                childNode.ResolveToOne(dataBeingConstructed);
            }
            
            // add in our own changes AFTER children populated.
            
            // annotations are concatenated together
            dataBeingConstructed.Annotations.AddRange(Data.Annotations);
            
            // only change the byte if we're non-null and overriding something underneath
            if (Data.Byte != null)
                dataBeingConstructed.Byte = Data.Byte;

            return dataBeingConstructed;
        }
    }
}

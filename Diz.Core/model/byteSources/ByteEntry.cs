using System;
using System.Collections.Generic;
using System.Threading;

namespace Diz.Core.model.byteSources
{
    public interface IReadOnlyByteEntry
    {
        byte? Byte { get; }
        IReadOnlyList<Annotation> Annotations { get; }

        ByteSource ParentByteSource { get; }
        int ParentByteSourceIndex { get; }
        
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

    // mostly, adds helper methods to ByteEntryBase, which is what does the heavy lifting
    public class ByteEntry : ByteEntryBase //, IReadOnlyByteEntry
    {
        public ByteEntry() {}
        public ByteEntry(AnnotationCollection annotationsToAppend) : base(annotationsToAppend) {}
        
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
        
        // IReadOnlyList<Annotation> IReadOnlyByteEntry.Annotations => Annotations;
        
        private OpcodeAnnotation GetOneOpcodeAnnotation() => GetOneAnnotation<OpcodeAnnotation>();
        private OpcodeAnnotation GetOrCreateOpcodeAnnotation() => GetOrCreateAnnotation<OpcodeAnnotation>();
    }
    
    // JUST holds the data. no graph traversal.
    public class ByteEntryBase : IParentAwareItem<ByteEntry>
    {
        public ByteEntryBase()
        {
            
        }

        public ByteEntryBase(AnnotationCollection annotationsToAppend)
        {
            // during initialization, append in case other object initializers have added annotations
            // via the other properties
            AppendAnnotationsFrom(annotationsToAppend, true);
        }

        public bool DontSetParentOnCollectionItems
        {
            get => dontSetParentOnCollectionItems;
            init
            {
                dontSetParentOnCollectionItems = value;
                if (annotations != null)
                    annotations.DontSetParentOnCollectionItems = dontSetParentOnCollectionItems;
            }
        }

        // Note: don't allocate this immediately, wait for it to be used. we will have millions of them per ROM
        public AnnotationCollection Annotations => annotations;

        // note: our thread safety isn't comprehensive in this project yet.
        // be careful with this if you're doing anything clever, especially writing.
        // TODO: instead of doing this, see if ConcurrentBag or similar classes for the container itself would work?
        public ReaderWriterLockSlim Lock => @lock ??= new ReaderWriterLockSlim();
        private ReaderWriterLockSlim @lock;

        #region References to parent enclosures
        
        // helper
        public ByteSource ParentByteSource => ParentStorage?.ParentByteSource;
        protected internal ByteStorage ParentStorage { get; internal set; }
        public int ParentByteSourceIndex  { get; internal set; }
        
        int IParentAwareItem<ByteEntry>.ParentByteSourceIndex
        {
            get => ParentByteSourceIndex;
            set => ParentByteSourceIndex = value;
        }

        IStorage<ByteEntry> IParentAwareItem<ByteEntry>.ParentStorage
        {
            get => ParentStorage;
            set => ParentStorage = (ByteStorage) value;
        }
        
        
        #endregion
        
        private AnnotationCollection annotations;
        private readonly bool dontSetParentOnCollectionItems;

        public AnnotationCollection GetOrCreateAnnotationsList()
        {
            return annotations ??= new AnnotationCollection
            {
                DontSetParentOnCollectionItems = DontSetParentOnCollectionItems,
                Parent = this
            };
        }

        protected bool AnnotationsEffectivelyEqual(ByteEntryBase other) => 
            AnnotationCollection.EffectivelyEqual(Annotations, other?.Annotations);

        public T GetOneAnnotation<T>() where T : Annotation => Annotations?.GetOne<T>();
        public T GetOrCreateAnnotation<T>() where T : Annotation, new() => GetOrCreateAnnotationsList().GetOrCreateOne<T>();
        public void AddAnnotation(Annotation newAnnotation) => GetOrCreateAnnotationsList().Add(newAnnotation);
        public void RemoveOneAnnotationIfExists<T>() where T : Annotation => Annotations?.RemoveOneIfExists<T>();
        
        public void ReplaceAnnotationsWith(AnnotationCollection itemsToReplaceWith)
        {
            Annotations?.Clear();
            AppendAnnotationsFrom(itemsToReplaceWith);
        }
        
        private void AppendAnnotationsFrom(IReadOnlyList<Annotation> itemsToAppend, bool overrideParentCheck = false)
        {
            // this must be set so that any Annotation references we pick up here retain their original .Parent
            if (!DontSetParentOnCollectionItems && !overrideParentCheck)
                throw new InvalidOperationException("DontSetParentOnCollectionItems must be true to aggregate annotations");
            
            if (itemsToAppend == null)
                return;

            GetOrCreateAnnotationsList();
            Annotations.DontSetParentOnCollectionItems = overrideParentCheck || DontSetParentOnCollectionItems;
            Annotations.CombineWith(itemsToAppend);
            Annotations.DontSetParentOnCollectionItems = DontSetParentOnCollectionItems;
            Annotations.Parent = this;
        }

        // really, use this only when doing graph building stuff.  
        public void AppendAnnotationsFrom(ByteEntry lowerPriorityByteEntry)
        {
            AppendAnnotationsFrom(lowerPriorityByteEntry?.Annotations);
        }

        #region Equality

        protected bool Equals(ByteEntryBase other)
        {
            // customized code, not auto-generated.
            return AnnotationsEffectivelyEqual(other) &&
                   DontSetParentOnCollectionItems == other.DontSetParentOnCollectionItems &&
                   ReferenceEquals(ParentStorage, other.ParentStorage) && ParentByteSourceIndex == other.ParentByteSourceIndex;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ByteEntryBase) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(annotations, DontSetParentOnCollectionItems, ParentStorage, ParentByteSourceIndex);
        }

        #endregion
    }
}

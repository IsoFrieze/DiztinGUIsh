using System;
using System.Collections.Generic;
using System.Threading;

namespace Diz.Core.model.byteSources
{
    // JUST holds the data. no graph traversal.
    public partial class ByteEntry
        // IParentReferenceTo<IByteStorage>, 
        // IParentReferenceTo<Storage<ByteEntry, ByteSource, StorageWithParentByteStorage>>
        : IParentReferenceTo<Storage<ByteEntry>>
    {
        // public ByteEntry()
        // {
        //     
        // }

        public ByteEntry(AnnotationCollection annotationsToAppend)
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
        public ByteSource ParentByteSource => Parent?.Parent;

        // real stuff
        public int ParentIndex  { get; set; }
        public Storage<ByteEntry> Parent { get; set; }

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

        protected bool AnnotationsEffectivelyEqual(ByteEntry other) => 
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

        protected bool Equals(ByteEntry other)
        {
            // customized code, not auto-generated.
            return AnnotationsEffectivelyEqual(other) &&
                   DontSetParentOnCollectionItems == other.DontSetParentOnCollectionItems &&
                   ReferenceEquals(Parent, other.Parent) && ParentIndex == other.ParentIndex;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ByteEntry) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(annotations, DontSetParentOnCollectionItems, Parent, ParentIndex);
        }

        #endregion
    }
}

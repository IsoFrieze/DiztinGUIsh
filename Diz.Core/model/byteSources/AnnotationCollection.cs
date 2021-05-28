using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Diz.Core.util;

namespace Diz.Core.model.byteSources
{
    public class AnnotationCollection : ParentAwareCollection<ByteEntry, Annotation>
    {
        [XmlIgnore] public bool EnforcePolicy { get; set; } = true;
        
        public T GetOne<T>() where T : Annotation
        {
            return this.SingleOrDefaultOfType<T, Annotation>();
        }

        public T GetOrCreateOne<T>() where T : Annotation, new()
        {
            var existing = GetOne<T>();
            if (existing != null)
                return existing;

            var newItem = new T();

            Add(newItem);
            return newItem;
        }
        
        public void RemoveOneIfExists<T>() where T : Annotation
        {
            var existing = GetOne<T>();
            if (existing != null)
                Remove(existing);
        }

        #region Equality

        public bool EffectivelyEquals(AnnotationCollection other)
        {
            if (other == null)
                return Count == 0;

            return Count == other.Count && 
                   GetEnumeratorSortByAnnotationType()
                       .SequenceEqual(other.GetEnumeratorSortByAnnotationType());
        }

        // "Effectively Equal" means null list and zero-length list are equal.
        // this is not used by the normal Equals() function and must be invoked explicitly by client code
        public static bool EffectivelyEqual(AnnotationCollection item1, AnnotationCollection item2)
        {
            if (Util.BothListsNullOrContainNoItems(item1, item2))
                return true;
            
            return item1?.EffectivelyEquals(item2) ?? false;
        }

        public IEnumerable<Annotation> GetEnumeratorSortByAnnotationType()
        {
            return this.OrderBy(x => x.GetType().GUID).ThenBy(x => x);
        }
        #endregion

        public void CombineWith(IReadOnlyList<Annotation> itemsToAddIn)
        {
            if (itemsToAddIn == null)
                return;
            
            // we're going to make a copy of ourselves in order to allow no modifications in case of an exception.
            // last step will actually replace the list.
            var copyOfExistingToAddTo = this.Select(x => x).ToList();
            
            foreach (var newItem in itemsToAddIn)
            {
                AppendIfPolicyAllows(copyOfExistingToAddTo, newItem);
            }
            
            // everything is good, so go ahead and modify the original list now
            Clear();
            this.AddRange(copyOfExistingToAddTo);
        }

        private void AppendIfPolicyAllows(ICollection<Annotation> copyOfExistingToAddTo, Annotation newItem)
        {
            if (!DontSetParentOnCollectionItems)
                throw new InvalidOperationException("DontSetParentOnCollectionItems must be true to aggregate annotations");
            
            if (VerifyAllowedToAppend(copyOfExistingToAddTo, newItem))
                copyOfExistingToAddTo.Add(newItem);
        }

        public bool VerifyAllowedToAppend(IEnumerable<Annotation> copyOfExistingToAddTo, Annotation newItem)
        {
            if (newItem == null) 
                return false;
            
            return !EnforcePolicy || AnnotationCollectionPolicy.IsAppendAllowed(copyOfExistingToAddTo, newItem);
        }

        public bool VerifyAllowedToAppend(Annotation newItem) => 
            AnnotationCollectionPolicy.IsAppendAllowed(this, newItem);

        private void ThrowIfNotAllowedToAppend(IEnumerable<Annotation> copyOfExistingToAddTo, Annotation newItem)
        {
            if (!VerifyAllowedToAppend(copyOfExistingToAddTo, newItem))
                throw new InvalidDataException(
                    "Found multiple annotations of same type when we required to find exactly 0 or 1");
        }
        
        // adds
        protected override void InsertItem(int index, Annotation item)
        {
            var copy = Items.ToList();
            ThrowIfNotAllowedToAppend(copy, item);
            base.InsertItem(index, item);
        }

        // replaces
        protected override void SetItem(int index, Annotation item)
        {
            ThrowIfNotAllowedToReplace(index, item);
            base.SetItem(index, item);
        }

        private void ThrowIfNotAllowedToReplace(int index, Annotation item)
        {
            Debug.Assert(index >= 0 && index < Items.Count);
            var itemsCopy = Items.ToList();
            itemsCopy.RemoveAt(index);
            ThrowIfNotAllowedToAppend(itemsCopy, item);
        }
    }

    public static class AnnotationCollectionPolicy
    {
        public enum AnnotationAppendPolicy
        {
            Allowed,
            ShouldIgnore,
            NotAllowed,
        }

        public static bool IsAppendAllowed(IEnumerable<Annotation> copyOfExistingToAddTo, Annotation newItem)
        {
            // someday, this should go in an external class and be able to be messed with.
            
            return ComputeAppendPolicyFor(copyOfExistingToAddTo, newItem) switch
            {
                AnnotationAppendPolicy.Allowed => true,
                AnnotationAppendPolicy.ShouldIgnore => false,
                AnnotationAppendPolicy.NotAllowed => throw new InvalidDataException(
                    "Found multiple annotations of same type when we required to find exactly 0 or 1"),
                _ => throw new InvalidDataException("Unknown append policy")
            };
        }

        private static AnnotationAppendPolicy ComputeAppendPolicyFor(IEnumerable<Annotation> copyOfExistingToAddTo, Annotation newAnnotation)
        {
            var existing = (Annotation)copyOfExistingToAddTo?.SingleOrDefaultOfType(newAnnotation.GetType());
            return existing == null ? AnnotationAppendPolicy.Allowed : GetPolicyIfExisting(newAnnotation, existing);
        }
        
        private static AnnotationAppendPolicy GetPolicyIfExisting(Annotation wantToAddAnnotation, Annotation alreadyExisting)
        {
            Debug.Assert(wantToAddAnnotation.GetType() == alreadyExisting.GetType());
            
            // if our existing annotation list contains a ByteAnnotation already,
            // it's not an error BUT we will ignore this candidate byte
            if (wantToAddAnnotation is ByteAnnotation && alreadyExisting is ByteAnnotation)
                return AnnotationAppendPolicy.ShouldIgnore;

            // any other type of duplicate? right now, don't allow it.
            // (in the future, we could do smarter things or allow multiples of certain types/etc.
            //  for now, we disallow it)
            return AnnotationAppendPolicy.NotAllowed;
        }
    }
}
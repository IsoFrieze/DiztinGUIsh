using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Diz.Core.util;

namespace Diz.Core.model.byteSources
{
    public class ByteSource
    {
        public string Name { get; set; }
        
        public Storage<ByteEntry> Bytes { get; set; }
        public List<ByteSourceMapping> ChildSources { get; set; } = new();

        // helper method: return the actual byte value at an index, or, if null,
        // throw an exception. this method is accommodating an older interface and
        // new could should migrate to GetCompiledByteEntry() and check for NULL
        [Obsolete("Use BuildFlatByteEntryFor() instead and check for null")]
        public byte GetByte(int index)
        {
            var dataAtOffset = BuildFlatByteEntryFor(index);
            
            if (dataAtOffset == null)
                throw new InvalidDataException("ERROR: GetByte() no data available at that offset");

            // if you hit this, you should think about updating client code to instead call GetCompiledByteEntry() and handle null
            if (dataAtOffset.Byte == null)
                throw new InvalidDataException("ERROR: GetByte() doesn't map to a real byte");

            return (byte) dataAtOffset.Byte;
        }

        // returns null if none found
        public ByteEntry BuildFlatByteEntryFor(int index)
        {
            return ByteGraphUtil.BuildFlatDataFrom(this, index);
        }

        public bool IsValidIndex(int index)
        {
            return index >= 0 && index < Bytes?.Count;
        }

        public void RemoveAllAnnotations(Predicate<Annotation> match)
        {
            // recurse through us and all children, remove annotations matching filter
            for (var i = 0; i < Bytes.Count; ++i)
                RemoveAllAnnotationsAt(i, match);
        }

        public void RemoveAllAnnotationsAt(int index, Predicate<Annotation> match)
        {
            if (!IsValidIndex(index))
                return;

            Bytes[index]?.Annotations?.RemoveAll(match);

            foreach (var childNodes in ChildSources)
                childNodes.ByteSource.RemoveAllAnnotations(match);
        }
        
        public void AddAnnotation<T>(int index, T newAnnotation) where T : Annotation, new()
        {
            // for now, just put new annotations on us directly without looking at child bytesources
            //
            // in the future, we'll want to make it so we can intelligently choose to push these annotation down
            // to child regions (i.e. if we have mapped ROM or WRAM etc), so that annotation can live in the
            // best region. this will make dealing with weird stuff like mirroring, patches, etc much easier.

            var b = Bytes[index];
            if (b == null)
                Bytes[index] = b = new ByteEntry();

            b.AddAnnotation(newAnnotation);
        }
        
        // NOTE: recursion into the graph, careful.
        public T GetOneAnnotation<T>(int index) where T : Annotation
        {
            // PERF NOTE: this is now doing graph traversal and memory allocation, could get expensive
            // if called a lot. Keep an eye on it and do some caching if needed.
            var offsetData = ByteGraphUtil.BuildFlatDataFrom(this, index);
            return offsetData?.GetOneAnnotation<T>();
        }

        // go through every byte we contain, return all annotations found at every byte
        // WILL RETURN MIRRORED DATA. i.e. if a SNES ROM bank is mirrored 20 times,
        // there will be 20 references to the same annotation.
        //
        // the int addresses here are an index into THIS bytesource (not the one the Annotations came from).
        // you can access their underlying ByteSource (if different) via Annotation.Parent
        //
        // if you want a unique list, use another enumerator below.
        public IEnumerable<KeyValuePair<int, T>> GetEveryAnnotationEnumerator<T>() where T : Annotation 
        {
            for (var index = 0; index < Bytes.Count; ++index)
            {
                var annotation = GetOneAnnotation<T>(index);
                if (annotation == null)
                    continue;

                yield return new KeyValuePair<int, T>(index, annotation);
            }   
        }
        
        // this will search our children for annotations, and translate them into our address space.
        // returns: a unique list of annotations found in all ByteSources (including us and our children),
        // mapped into our address space.
        //
        // NOTES:
        // - this will NOT return mirrored labels, for that use GetEveryAnnotationEnumerator().
        // - to get the Annotation's index into its true containing ByteSource, use Annotation.Parent
        // - results are unordered
        public IEnumerable<KeyValuePair<int, T>> GetAnnotationsIncludingChildrenEnumerator<T>(int startIndex = 0, int count = -1) where T : Annotation
        {
            if (ChildSources == null)
                yield break;
            
            var endingIndex = GetEndingIndexForRange(startIndex, count);

            // return annotations found in children
            foreach (var child in ChildSources)
            {
                foreach (var (childIndex, childAnnotation) in child.ByteSource.GetAnnotationsIncludingChildrenEnumerator<T>())
                {
                    Debug.Assert(child.ByteSource.IsValidIndex(childIndex));
                    var ourIndex = child.ConvertIndexFromChildToParent(childIndex);
                    Debug.Assert(IsValidIndex(ourIndex));

                    if (ourIndex < startIndex || ourIndex > endingIndex)
                        continue;
                    
                    yield return new KeyValuePair<int, T>(ourIndex, childAnnotation);
                }
            }

            foreach (var annotation in GetOnlyOwnAnnotations<T>())
                yield return new KeyValuePair<int, T>(annotation.Parent.ParentIndex, annotation);
        }

        // return a list of annotations attached to our Storage<ByteEntry> (and nothing from our children)
        public IEnumerable<T> GetOnlyOwnAnnotations<T>(int startIndex = 0, int count = -1) where T : Annotation
        {
            var endingIndex = GetEndingIndexForRange(startIndex, count);
            
            // return our own annotations
            // PERFORMANCE: we use GetNativeEnumerator() which will skip any ByteEntry that doesn't really exist.
            var enumerator = Bytes.GetNativeEnumerator();
            while (enumerator.MoveNext())
            {
                var byteEntry = enumerator.Current;
                
                Debug.Assert(byteEntry != null);
                // Debug.Assert(ReferenceEquals(byteEntry.ParentByteSource, this));
                
                if (byteEntry.ParentIndex < startIndex || byteEntry.ParentIndex > endingIndex)
                    continue;
                
                var annotation = byteEntry.GetOneAnnotation<T>();
                if (annotation == null) 
                    continue;
                
                Debug.Assert(ReferenceEquals(annotation.Parent, byteEntry));
                yield return annotation;
            }
        }

        private int GetEndingIndexForRange(int startIndex, int count)
        {
            if (!IsValidIndex(startIndex))
                throw new IndexOutOfRangeException(nameof(startIndex));

            var endingIndex = startIndex + (count != -1 ? count : Bytes.Count) - 1;
            if (!IsValidIndex(endingIndex))
                throw new ArgumentOutOfRangeException(nameof(count));
            
            return endingIndex;
        }
        
        public void SetBytesFrom(IReadOnlyList<byte> copyFrom, int dstStartingOffset = 0)
        {
            for (var i = 0; i < copyFrom.Count; ++i)
                Bytes[i + dstStartingOffset].Byte = copyFrom[i];
        }

        // inefficient helper method to get a copy of any ByteAnnotation in this bytesource
        public IEnumerable<byte> GetRawBytesCopy()
        {
            return Bytes.Select(entry => entry?.Byte ?? 0x00);
        }

        public int GetAnnotationCountInAllChildren<T>() where T : Annotation => 
            GetEveryAnnotationEnumerator<T>().Select(t => t.Value).Count();
        
        #region Equality

        protected bool Equals(ByteSource other)
        {
            var bothEmptyOrEqual = 
                Util.BothListsNullOrContainNoItems(Bytes, other.Bytes) || 
                EqualsInternal(other);

            return Name == other.Name 
                   && bothEmptyOrEqual 
                   && ChildSources.SequenceEqual(other.ChildSources);
        }
        
        private bool EqualsInternal(ByteSource other)
        {
            // this method is for these specific conditions:
            Debug.Assert(Bytes != null && other?.Bytes != null);
            Debug.Assert(Bytes.Count == other.Bytes.Count);

            // do a special case if it's two particular types we know about
            // (TODO: see if we can get this to happen without having to do it specifically here)
            if (Bytes is StorageSparse<ByteEntry> sparse1 && other.Bytes is StorageSparse<ByteEntry> sparse2)
            {
                return sparse1.Equals(sparse2);
            }
            
            // fallback: original version. this works but, it uses the dumber enumerator
            // which some storage types potentially do lots of extra enumeration.
            return Bytes.SequenceEqual(other.Bytes);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ByteSource) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Bytes, Name, ChildSources);
        }

        #endregion
    }
}
using System;
using System.Collections.Generic;
using System.IO;

namespace Diz.Core.model.byteSources
{
    public class ByteSource
    {
        public string Name { get; set; }

        private readonly ByteStorage bytes;
        public ByteStorage Bytes
        {
            get => bytes;
            init
            {
                bytes = value;
                bytes.ParentContainer = this;
            }
        }

        private readonly List<ByteSourceMapping> childSources = new();
        public IReadOnlyList<ByteSourceMapping> ChildSources => childSources;

        public byte GetByte(int index)
        {
            var dataAtOffset = ByteGraphUtil.BuildFlatDataFrom(this, index); // EXPENSIVE

            if (dataAtOffset == null)
                throw new InvalidDataException("ERROR: GetByte() no data available at that offset");

            if (dataAtOffset.Byte == null)
                throw new InvalidDataException("ERROR: GetByte() doesn't map to a real byte");

            return (byte) dataAtOffset.Byte;
        }
        
        public void AddByte(ByteEntry byteOffset)
        {
            Bytes.AddByte(byteOffset);
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
            
            Bytes[index].Annotations?.RemoveAll(match);
            
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

        // forward graph walk, kind of inefficient
        // will return mirrored copies of annotations
        public IEnumerable<KeyValuePair<int, T>> GetAnnotationEnumerator<T>() where T : Annotation 
        {
            for (var index = 0; index < Bytes.Count; ++index)
            {
                var annotation = GetOneAnnotation<T>(index);
                if (annotation == null)
                    continue;

                yield return new KeyValuePair<int, T>(index, annotation);
            }   
        }
        
        // same as above, but start with the children first and bubble upwards
        /*public IEnumerable<KeyValuePair<int, T>> GetAnnotationEnumerator2<T>() where T : Annotation
        {
            // first, children
        }*/

        public void AttachChildByteSource(ByteSourceMapping childByteSourceMapping)
        {
            childSources.Add(childByteSourceMapping);
        }
    }
}
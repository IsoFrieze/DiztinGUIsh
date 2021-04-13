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
            var dataAtOffset = CompileAllChildDataFrom(index); // EXPENSIVE

            if (dataAtOffset == null)
                throw new InvalidDataException("ERROR: GetByte() no data available at that offset");

            if (dataAtOffset.Byte == null)
                throw new InvalidDataException("ERROR: GetByte() doesn't map to a real byte");

            return (byte) dataAtOffset.Byte;
        }

        // important to always go through this function when adding bytes, so we can cache some data
        public void AddByte(ByteOffsetData byteOffset)
        {
            Bytes.AddByte(byteOffset);
        }

        // return a directed graph with all possible values for this offset including all child regions
        public ByteOffsetDataNode BuildFullGraph(int sourceIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= Bytes.Count)
                return null;

            var node = new ByteOffsetDataNode(this, sourceIndex);

            BuildChildGraph(sourceIndex, ref node);
            
            return node;
        }

        // caution: recursion
        private void BuildChildGraph(int sourceIndex, ref ByteOffsetDataNode nodeToPopulate)
        {
            foreach (var childSourceToTraverse in ChildSources)
            {
                var childIndex = childSourceToTraverse.RegionMapping
                    .ConvertSourceToDestination(sourceIndex, childSourceToTraverse.ByteSource);
                
                var newChildNode = childSourceToTraverse.ByteSource.BuildFullGraph(childIndex);
                if (newChildNode == null) 
                    continue;
                
                nodeToPopulate.AttachChildNode(newChildNode);
            }
        }

        // special function. attempts to recurse the graph and collapse all the nodes into
        // one ByteDataOffset which represents all of the information about our offset.
        //
        // this works in simple cases where you'd only expect i.e. one offset, or bytes are ok 
        // to be overwritten.  This doesn't work for more complex stuff like mirrored offsets/etc.
        // In those cases, you should more manually walk the graph node yourself in whatever manner
        // is appropriate for the calling code.
        public ByteOffsetData CompileAllChildDataFrom(int index)
        {
            if (!IsValidIndex(index))
                return null;
            
            var node = BuildFullGraph(index);
            var finalData = node.CreateByteByFlatteningGraph();

            return finalData;
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
                Bytes[index] = b = new ByteOffsetData();
            
            b.GetOrCreateAnnotationsList().Add(newAnnotation);
        }
        
        // NOTE: recursion into the graph, careful.
        public T GetOneAnnotation<T>(int index) where T : Annotation
        {
            // PERF NOTE: this is now doing graph traversal and memory allocation, could get expensive
            // if called a lot. Keep an eye on it and do some caching if needed.
            var offsetData = CompileAllChildDataFrom(index);
            return offsetData?.GetOneAnnotation<T>();
        }

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

        public void AttachChildByteSource(ByteSourceMapping childByteSourceMapping)
        {
            childSources.Add(childByteSourceMapping);
        }
    }
}
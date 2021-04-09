using System;
using System.Collections.Generic;
using System.Diagnostics;
using Diz.Core.util;

namespace Diz.Core.model.byteSources
{
    public class ByteSource
    {
        public string Name { get; set; }

        protected List<ByteOffsetData> bytes = new();

        public IReadOnlyList<ByteOffsetData> Bytes => bytes;

        public List<ByteSourceMapping> ChildSources { get; init; } = new();

        public ByteSource()
        {
            
        }

        public void AddByte(ByteOffsetData byteOffset)
        {
            bytes.Add(byteOffset);
            byteOffset.ContainerOffset = Bytes.Count - 1; // IMPORTANT
            byteOffset.Container = this;
        }
        
        public ByteSource(IReadOnlyCollection<ByteOffsetData> inBytes)
        {
            bytes = new List<ByteOffsetData>(inBytes.Count);
            
            var i = 0;
            foreach (var b in inBytes)
            {
                AddByte(b);
                ++i;
            }
        }
        
        public ByteSource(IReadOnlyCollection<byte> inBytes)
        {
            bytes = new List<ByteOffsetData>(inBytes.Count);

            var i = 0;
            foreach (var b in inBytes)
            {
                AddByte(new ByteOffsetData {
                    Byte = b,
                    Container = this,
                    ContainerOffset = i
                });

                ++i;
            }
        }

        // return a directed graph with all possible values for this offset including all child regions.
        // if index is out of range, skip this.
        public ByteOffsetDataNode TraverseChildren(int sourceIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= Bytes.Count)
                return null;
            
            var node = new ByteOffsetDataNode
            {
                Data = Bytes[sourceIndex],
            };
            
            TraverseChildNodes(sourceIndex, ref node);
            
            return node;
        }

        // caution: recursion
        private void TraverseChildNodes(int sourceIndex, ref ByteOffsetDataNode nodeToPopulate)
        {
            foreach (var childSourceToTraverse in ChildSources)
            {
                var childIndex = childSourceToTraverse.RegionMapping.ConvertSourceToDestination(sourceIndex, childSourceToTraverse.ByteSource);
                
                var newChildNode = childSourceToTraverse.ByteSource.TraverseChildren(childIndex);
                if (newChildNode != null)
                {
                    if (nodeToPopulate.Children == null)
                        nodeToPopulate.Children = new List<ByteOffsetDataNode>();
                    
                    nodeToPopulate.Children.Add(newChildNode);
                }
            }
        }

        // special function. attempts to recurse the graph and collapse all the nodes into
        // one ByteDataOffset which represents all of the information about our offset.
        //
        // this works in simple cases where you'd only expect i.e. one offset, or bytes are ok 
        // to be overwritten.  This doesn't work for more complex stuff like mirrored offsets/etc.
        // In those cases, you should more manually walk the graph node yourself in whatever manner
        // is appropriate for the calling code.
        public ByteOffsetData CompileAllChildDataAt(int index)
        {
            if (!IsValidIndex(index))
                return null;
            
            var node = TraverseChildren(index);
            var finalData = node.ResolveToOne();

            return finalData;
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < bytes?.Count;
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
        
        public void AddAnnotation<T>(int snesOffset, T newAnnotation) where T : Annotation, new()
        {
            // for now, just put new annotations on us directly without looking at child bytesources
            //
            // in the future, we'll want to make it so we can intelligently choose to push these annotation down
            // to child regions (i.e. if we have mapped ROM or WRAM etc), so that annotation can live in the
            // best region. this will make dealing with weird stuff like mirroring, patches, etc much easier.
            Bytes[snesOffset].Annotations.Add(newAnnotation);
        }
        
        // recurses into the graph
        public T GetOneAnnotation<T>(int index) where T : Annotation
        {
            // PERF NOTE: this is now doing graph traversal and memory allocation, could get expensive
            // if called a lot. Keep an eye on it and do some caching if needed.
            var offsetData = CompileAllChildDataAt(index);
            return offsetData?.GetOneAnnotation<T>();
        }

        public IEnumerable<KeyValuePair<int, T>> GetAnnotationEnumerator<T>() where T : Annotation 
        {
            for (var snesAddress = 0; snesAddress < Bytes.Count; ++snesAddress)
            {
                var annotation = GetOneAnnotation<T>(snesAddress);
                if (annotation == null)
                    continue;

                yield return new KeyValuePair<int, T>(snesAddress, annotation);
            }   
        }

        public static ByteSource CreateEmpty(int size)
        {
            var emptyOffsetData = new List<ByteOffsetData>(size);
            for (var i = 0; i < size; ++i)
            {
                emptyOffsetData.Add(new ByteOffsetData());
            }

            var newByteSource = new ByteSource(emptyOffsetData);
            return newByteSource;
        }
    }
    
    public class SnesAddressSpaceByteSource : ByteSource
    {
        public SnesAddressSpaceByteSource() : base()
        {
            Debug.Assert(bytes.Count == 0);
            
            // create all addressable bytes for the SNES address space.
            // by default all .byte are null and there's no annotations here.
            const int snesAddressSpaceSizeInBytes = 0xFFFFFF;
            
            // note: potentially.... uses a lot of memory, yea.
            // we probably want to switch to a dictionary or something sparse for this class specifically
            // or, create these bytes on-demand only when needed since most will be empty.
            bytes = new List<ByteOffsetData>(snesAddressSpaceSizeInBytes);
            for (var i = 0; i < snesAddressSpaceSizeInBytes; ++i)
            {
                AddByte(new ByteOffsetData());
            }
        }
    }
}
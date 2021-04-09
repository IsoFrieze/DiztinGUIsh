using System;
using System.Collections.Generic;

namespace Diz.Core.model
{
    public class ByteSource
    {
        public string Name { get; set; }
        
        public List<ByteOffsetData> Bytes { get; protected init; } = new();

        public List<ByteSourceMapping> ChildSources { get; init; } = new();
        
        public ByteSource(IReadOnlyCollection<byte> actualRomBytes)
        {
            Bytes = new List<ByteOffsetData>(actualRomBytes.Count);

            var i = 0;
            foreach (var fileByte in actualRomBytes)
            {
                Bytes.Add(new ByteOffsetData
                    {
                        Byte = fileByte,
                        Container = this,
                        ContainerOffset = i
                    }
                );

                ++i;
            }
        }

        public ByteSource()
        {
            // int x = 3;
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
            var node = TraverseChildren(index);
            var finalData = node.ResolveToOne();

            return finalData;
        }
        
        public void RemoveAllAnnotations(Predicate<Annotation> match)
        {
            // recurse through us and all children, remove annotations matching filter
            for (var i = 0; i < Bytes.Count; ++i)
                RemoveAllAnnotationsAt(i, match);
        }

        public void RemoveAllAnnotationsAt(int index, Predicate<Annotation> match)
        {
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
    }
    
    // temp hack, "RomBytes" class needs to go away, this is just to make some refactoring easier.
    [Obsolete("RomBytes class is legacy, replace usages with ByteSource")]
    public class RomBytes : ByteSource
    {
        // deprecated constructor, remove it, or move it into ByteSource
        public RomBytes(IReadOnlyCollection<ByteOffsetData> romBytes) : base()
        {
            Bytes = new List<ByteOffsetData>(romBytes.Count);
            for (var i = 0; i < romBytes.Count; ++i)
            {
                Bytes.Add(new ByteOffsetData
                {
                    ContainerOffset = i,
                });
            }
        }
    }

    public class SnesAddressSpaceByteSource : ByteSource
    {
        public SnesAddressSpaceByteSource() : base()
        {
            // create all addressible bytes for the SNES address space.
            // by default all .byte are null and there's no annotations here.
            const int snesAddressSpaceSizeInBytes = 0xFFFFFF;
            
            // note: potentially.... uses a lot of memory, yea.
            // we probably want to use a dictionary or something sparse for this class specifically
            // or, create these bytes on-demand only when needed since most will be empty.
            Bytes = new List<ByteOffsetData>(snesAddressSpaceSizeInBytes);
            for (var i = 0; i < snesAddressSpaceSizeInBytes; ++i)
            {
                Bytes.Add(new ByteOffsetData
                {
                    ContainerOffset = i,
                });
            }
        }
    }
}
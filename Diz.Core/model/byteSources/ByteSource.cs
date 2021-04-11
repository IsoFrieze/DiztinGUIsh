using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using JetBrains.Annotations;

namespace Diz.Core.model.byteSources
{
    public abstract class ByteStorage
    {
        // eventually, we gotta kill this interface. suggest replacing with []
        public abstract IReadOnlyList<ByteOffsetData> Bytes { get; }
        
        protected ByteSource ParentContainer { get; }

        protected ByteStorage(ByteSource parent)
        {
            ParentContainer = parent;
            InitFromEmpty(0);
        }
        
        protected ByteStorage(ByteSource parent, IReadOnlyCollection<ByteOffsetData> inBytes)
        {
            ParentContainer = parent;
            InitFrom(inBytes);
        }

        protected ByteStorage(ByteSource parent, int emptyCreateSize)
        {
            ParentContainer = parent;
            InitFromEmpty(emptyCreateSize);
        }

        private void InitFromEmpty(int emptyCreateSize)
        {
            Debug.Assert(ParentContainer != null);
            Debug.Assert(emptyCreateSize >= 0);
            
            InitEmptyContainer(emptyCreateSize);
            FillEmptyContainerWithBlankBytes(emptyCreateSize);

            Debug.Assert(Count == emptyCreateSize);
        }

        protected void InitFrom(IReadOnlyCollection<ByteOffsetData> inBytes)
        {
            Debug.Assert(ParentContainer != null);
            Debug.Assert(inBytes != null);
            
            InitEmptyContainer(inBytes.Count);
            FillEmptyContainerWithBytesFrom(inBytes);
            
            Debug.Assert(Count == inBytes.Count);
        }

        public int Count => Bytes?.Count ?? 0;

        protected abstract void InitEmptyContainer(int emptyCreateSize);
        protected abstract void FillEmptyContainerWithBytesFrom(IReadOnlyCollection<ByteOffsetData> inBytes);
        protected abstract void FillEmptyContainerWithBlankBytes(int numEntries);

        public abstract void AddByte(ByteOffsetData byteOffset);

        protected void OnPreAddByteAt(int newIndex, ByteOffsetData byteOffset)
        {
            Debug.Assert(ParentContainer != null);
            
            // cache these values
            byteOffset.Container = ParentContainer;
            byteOffset.ContainerOffset = newIndex; // this will be true after the Add() call below.
        }
    }
    
    // Simple version of byte storage that stores everything as an actual list
    // This is fine for stuff like Roms, however, it's bad for mostly empty large things like SNES
    // address spaces (24bits of addressible bytes x HUGE data = slowwwww)
    public class ByteList : ByteStorage //, IByteStorage
    {
        public override IReadOnlyList<ByteOffsetData> Bytes => bytes;
        
        // only ever use AddByte() to add bytes here
        private List<ByteOffsetData> bytes = new();
        
        [UsedImplicitly] public ByteList(ByteSource parent) : base(parent) { }
        
        [UsedImplicitly] public ByteList(ByteSource parent, int emptyCreateSize) : base(parent, emptyCreateSize) { }
        
        [UsedImplicitly] public ByteList(ByteSource parent, IReadOnlyCollection<ByteOffsetData> inBytes) : base(parent, inBytes) { }

        protected override void InitEmptyContainer(int capacity)
        {
            bytes = new List<ByteOffsetData>(capacity);
        }

        protected override void FillEmptyContainerWithBytesFrom(IReadOnlyCollection<ByteOffsetData> inBytes)
        {
            Debug.Assert(inBytes != null);

            foreach (var b in inBytes)
            {
                AddByte(b);
            }
        }

        protected override void FillEmptyContainerWithBlankBytes(int numEntries)
        {
            for (var i = 0; i < numEntries; ++i)
            {
                AddByte(new ByteOffsetData());
            }
        }

        public override void AddByte(ByteOffsetData byteOffset)
        {
            Debug.Assert(bytes != null);
            
            var newIndex = Bytes.Count; // will be true once we add it 
            OnPreAddByteAt(newIndex, byteOffset);

            bytes.Add(byteOffset);
        }
    }

    /*public class SparseByteStorage : IByteStorage
    {
        public IReadOnlyList<ByteOffsetData> Bytes { get; }
        public void AddByte(ByteSource parent, ByteOffsetData byteOffset)
        {
            throw new NotImplementedException();
        }
    }*/

    public class ByteSource
    {
        [UsedImplicitly] public Type ByteStorageType { get; init; } = typeof(ByteList);

        private static T CreateByteStorage<T>(params object[] paramArray) where T : ByteStorage
        {
            return (T)Activator.CreateInstance(typeof(T), args:paramArray);
        }
        public ByteSource()
        {
            bytes = CreateByteStorage<ByteList>(this);
        }

        public ByteSource(IReadOnlyCollection<ByteOffsetData> inBytes)
        {
            bytes = CreateByteStorage<ByteList>(this, inBytes);
        }
        
        public ByteSource(int emptySize)
        {
            bytes = CreateByteStorage<ByteList>(this, emptySize);
        }

        public string Name { get; set; }

        protected ByteStorage bytes;

        public IReadOnlyList<ByteOffsetData> Bytes => bytes.Bytes;

        public List<ByteSourceMapping> ChildSources { get; init; } = new();

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
            bytes.AddByte(byteOffset);
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
                var childIndex = childSourceToTraverse.RegionMapping
                    .ConvertSourceToDestination(sourceIndex, childSourceToTraverse.ByteSource);
                
                var newChildNode = childSourceToTraverse.ByteSource.TraverseChildren(childIndex);
                if (newChildNode == null) 
                    continue;
                
                nodeToPopulate.Children ??= new List<ByteOffsetDataNode>();
                nodeToPopulate.Children.Add(newChildNode);
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
            
            var node = TraverseChildren(index);
            var finalData = node.ResolveToOne();

            return finalData;
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < bytes?.Bytes?.Count;
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
            Bytes[index].Annotations.Add(newAnnotation);
        }
        
        // recurses into the graph
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
    }
}
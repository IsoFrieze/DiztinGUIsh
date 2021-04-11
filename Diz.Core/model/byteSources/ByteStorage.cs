using System.Collections.Generic;
using System.Diagnostics;
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
}
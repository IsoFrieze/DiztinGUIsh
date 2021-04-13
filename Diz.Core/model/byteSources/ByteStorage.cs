﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace Diz.Core.model.byteSources
{
    public abstract class ByteStorage : IEnumerable<ByteEntry>
    {
        public abstract ByteEntry this[int index] { get; set; }

        public abstract int Count { get; }

        protected internal ByteSource ParentContainer { get; set; }

        protected ByteStorage()
        {
            InitFromEmpty(0);
        }
        
        protected ByteStorage(IReadOnlyCollection<ByteEntry> inBytes)
        {
            InitFrom(inBytes);
        }

        protected ByteStorage(int emptyCreateSize)
        {
            InitFromEmpty(emptyCreateSize);
        }

        private void InitFromEmpty(int emptyCreateSize)
        {
            Debug.Assert(emptyCreateSize >= 0);
            
            InitEmptyContainer(emptyCreateSize);
            FillEmptyContainerWithBlankBytes(emptyCreateSize);

            Debug.Assert(Count == emptyCreateSize);
        }

        private void InitFrom(IReadOnlyCollection<ByteEntry> inBytes)
        {
            Debug.Assert(inBytes != null);
            
            InitEmptyContainer(inBytes.Count);
            FillEmptyContainerWithBytesFrom(inBytes);
            
            Debug.Assert(Count == inBytes.Count);
        }

        protected abstract void InitEmptyContainer(int emptyCreateSize);
        protected abstract void FillEmptyContainerWithBytesFrom(IReadOnlyCollection<ByteEntry> inBytes);
        protected abstract void FillEmptyContainerWithBlankBytes(int numEntries);

        public abstract void AddByte(ByteEntry byteOffset);

        protected void OnPreAddByteAt(int newIndex, ByteEntry byteOffset)
        {
            // cache these values
            byteOffset.ByteStorageContainer = this;
            byteOffset.ContainerOffset = newIndex; // this will be true after the Add() call below.
        }

        // note: enumerators will behave differently depending on underlying storages.
        // some may produce gaps in the sequence, nulls, or skip to just the bytes actually instantiated.
        public abstract IEnumerator<ByteEntry> GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        
        protected void ImportBytes(IReadOnlyCollection<ByteEntry> inBytes)
        {
            Debug.Assert(inBytes != null);
            foreach (var b in inBytes)
            {
                AddByte(b);
            }
        }

        // iterate through a sparse ByteStorage class, if we encounter any gaps in the sequence,
        // fill them in 
        protected class GapFillingEnumerator : IEnumerator<ByteEntry>
        {
            public ByteStorage ByteStorage { get; protected set; }
            public int Position { get; set; } = -1;

            public GapFillingEnumerator(ByteStorage storage)
            {
                Debug.Assert(storage != null);
                ByteStorage = storage;
            }
            public bool MoveNext()
            {
                Position++;
                return Position < ByteStorage.Count;
            }

            public void Reset()
            {
                Position = -1;
            }

            ByteEntry IEnumerator<ByteEntry>.Current => ByteStorage[Position];
            public object Current => ByteStorage[Position];
            public void Dispose()
            {
                Position = -1;
                ByteStorage = null;
            }
        }
    }
    
    // Simple version of byte storage that stores everything as an actual list
    // This is fine for stuff like Roms, however, it's bad for mostly empty large things like SNES
    // address spaces (24bits of addressible bytes x HUGE data = slowwwww)
    public class ByteList : ByteStorage
    {
        public override ByteEntry this[int index]
        {
            get => bytes[index];
            set
            {
                OnPreAddByteAt(index, value);
                bytes[index] = value;
            }
        }

        public override int Count => bytes?.Count ?? 0;

        // only ever use AddByte() to add bytes here
        private List<ByteEntry> bytes = new();
        
        [UsedImplicitly] public ByteList() : base() { }
        
        [UsedImplicitly] public ByteList(int emptyCreateSize) : base(emptyCreateSize) { }
        
        [UsedImplicitly] public ByteList(IReadOnlyCollection<ByteEntry> inBytes) : base(inBytes) { }

        protected override void InitEmptyContainer(int capacity)
        {
            bytes = new List<ByteEntry>(capacity);
        }

        protected override void FillEmptyContainerWithBytesFrom(IReadOnlyCollection<ByteEntry> inBytes)
        {
            ImportBytes(inBytes);
        }
        
        protected override void FillEmptyContainerWithBlankBytes(int numEntries)
        {
            for (var i = 0; i < numEntries; ++i)
            {
                AddByte(new ByteEntry());
            }
        }

        public override void AddByte(ByteEntry byteOffset)
        {
            Debug.Assert(bytes != null);
            
            var newIndex = Count; // will be true once we add it 
            OnPreAddByteAt(newIndex, byteOffset);

            bytes.Add(byteOffset);
        }

        // NOTE: in this implementation, all bytes at all addresses always exist, so,
        // this will never return null or have gaps in the sequence.
        //
        // other implementations may differ.
        public override IEnumerator<ByteEntry> GetEnumerator()
        {
            return bytes.GetEnumerator();
        }
    }

    public class SparseByteStorage : ByteStorage
    {
        [UsedImplicitly] public SparseByteStorage() : base() { }
        [UsedImplicitly] public SparseByteStorage(IReadOnlyCollection<ByteEntry> inBytes) : base(inBytes) { }
        [UsedImplicitly] public SparseByteStorage(int emptyCreateSize) : base(emptyCreateSize) { }

        // keeps the keys sorted, which is what we want.
        public SortedDictionary<int, ByteEntry> bytes;

        private int GetLargestKey()
        {
            if (bytes == null || bytes.Count == 0)
                return -1;
            
            return bytes.Keys.Last();
        }

        public override ByteEntry this[int index]
        {
            get => GetByte(index);
            set => SetByte(index, value);
        }

        private void SetByte(int index, ByteEntry value)
        {
            ValidateIndex(index);
            OnPreAddByteAt(index, value);

            // will replace if it exists
            bytes[index] = value;
        }

        // we need to maintain this. it's not the # of bytes we're storing,
        // it's the max size of the sparse container. i.e. this will never change.
        private int count = 0;
        public override int Count => count;

        public int ActualCount => bytes.Count;

        private bool ValidIndex(int index) => index >= 0 && index < Count;

        private void ValidateIndex(int index)
        {
            if (!ValidIndex(index))
                throw new ArgumentOutOfRangeException($"Index {index} out of range in SparseByteStorage");   
        }

        protected ByteEntry GetByte(int index)
        {
            ValidateIndex(index);
            return bytes.GetValueOrDefault(index);
        }

        protected override void InitEmptyContainer(int emptyCreateSize)
        {
            bytes = new SortedDictionary<int, ByteEntry>();
            count = emptyCreateSize;
        }

        protected override void FillEmptyContainerWithBytesFrom(IReadOnlyCollection<ByteEntry> inBytes)
        {
            Debug.Assert(inBytes != null);
            if (inBytes.Count > count)
                throw new InvalidDataException("Asked to add a list bigger than our current capacity");
            
            Debug.Assert(bytes?.Count == 0);
            
            foreach (var b in inBytes)
            {
                AddByte(b);
                
                // each byte added will tick up the count, when in reality we're filling in an existing container.
                // correct that here.
                count--;
            }
        }

        protected override void FillEmptyContainerWithBlankBytes(int numEntries)
        {
            // for sparse version, this is mostly a NOP.
            // we don't need to do anything here since the dictionary should have
            // already been created.  in our implementation there's no such thing as "blank"
            // bytes, we'll just return null if that address isn't in our dictionary.

            count = numEntries;
        }

        public override void AddByte(ByteEntry byteOffset)
        {
            // going to be a little weird. this would normally be "append" however it's
            // arbitrary where to do that with a dictionary. we wil interpret this as taking the highest
            // index (key i.e. SNES or ROM offset) and adding one to that.

            var largestKey = GetLargestKey();
            var indexThisWillBeAddedTo = largestKey + 1; // go one higher than our biggest

            ValidateIndex(indexThisWillBeAddedTo);
            OnPreAddByteAt(indexThisWillBeAddedTo, byteOffset);
            
            bytes[indexThisWillBeAddedTo] = byteOffset;
            count++;
        }

        // for our normal enumerator facing client code, use our special enumerator
        // which will return sequential items but fill in the aps with Null.
        //
        // this is not the most efficient thing but makes the client code easier
        // to write. if performance becomes an issue, use GetSparseEnumerator()
        // which will just return the sections that have been populated.
        public override IEnumerator<ByteEntry> GetEnumerator()
        {
            return new GapFillingEnumerator(this);
        }

        // note: indices are going to be ordered, BUT there can be gaps.
        // caller should be prepared to handle this. 
        public SortedDictionary<int, ByteEntry>.Enumerator GetSparseEnumerator()
        {
            return bytes.GetEnumerator();
        }
    }
}
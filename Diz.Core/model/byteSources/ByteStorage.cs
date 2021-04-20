using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

// TODO: we can probably simplify this by replacing the parent class with ParentAwareCollection<ByteSource, ByteEntryBase>

namespace Diz.Core.model.byteSources
{
    public abstract class ByteStorage : ICollection<ByteEntry>, ICollection
    {
        public abstract ByteEntry this[int index] { get; set; }

        public abstract void Add(ByteEntry item);
        public abstract void Clear();
        public abstract bool Contains(ByteEntry item);
        public abstract void CopyTo(ByteEntry[] array, int arrayIndex);
        public abstract bool Remove(ByteEntry item);
        public abstract void CopyTo(Array array, int index);
        public abstract int Count { get; }
        
        public bool IsReadOnly => false;
        public bool IsSynchronized => false;
        public object SyncRoot => default;
        
        protected internal ByteSource ParentByteSource { get; set; }

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

        // GetEnumerator() will return an item at each index, or null if no Byte is present at that address.
        // this means, as long as clients check for null items during enumeration, the behavior will be the same
        // regardless of the internal storage type.
        //
        // This is potentially more inefficient but creates a consistent interface.
        // For performance-heavy code, or cases where you only only want non-null bytes, choose another enumerator function
        public abstract IEnumerator<ByteEntry> GetGaplessEnumerator();
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetGaplessEnumerator();
        }

        public IEnumerator<ByteEntry> GetEnumerator() => GetGaplessEnumerator();

        public abstract IEnumerator<ByteEntry> GetNativeEnumerator();
        
        protected void ImportBytes(IReadOnlyCollection<ByteEntry> inBytes)
        {
            Debug.Assert(inBytes != null);
            foreach (var b in inBytes)
            {
                Add(b);
            }
        }
        protected void OnRemoved(ByteEntry item)
        {
            ClearParentInfoFor(item);
            UpdateAllParentInfo();
        }

        // refresh/rebuild parent info in each item.
        // update (or remove) all parent info when the collection has changed
        protected abstract void UpdateAllParentInfo(bool shouldUnsetAll = false);

        protected static void ClearParentInfoFor(ByteEntry b) => SetParentInfoFor(b, -1, null);
        protected void SetParentInfoFor(ByteEntry b, int index) => SetParentInfoFor(b, index, this);
        protected static void SetParentInfoFor(ByteEntry b, int index, ByteStorage parent)
        {
            b.ParentByteSourceIndex = index;
            b.ParentStorage = parent;
        }

        protected void UpdateParentInfoFor(ByteEntry byteEntry, bool shouldUnsetAll, int newIndex)
        {
            if (shouldUnsetAll)
                ClearParentInfoFor(byteEntry);
            else
                SetParentInfoFor(byteEntry, newIndex);
        }

        protected void OnPreClear() => UpdateAllParentInfo(shouldUnsetAll: true);

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
    // address spaces (24bits of addressable bytes x HUGE data = slowwwww)
    public class ByteStorageList : ByteStorage
    {
        public override ByteEntry this[int index]
        {
            get => bytes[index];
            set
            {
                SetParentInfoFor(value, index);
                bytes[index] = value;
            }
        }
        
        public override void Clear()
        {
            OnPreClear();
            bytes?.Clear();
        }

        public override bool Contains(ByteEntry item) => bytes?.Contains(item) ?? false;
        public override void CopyTo(ByteEntry[] array, int arrayIndex) => bytes.CopyTo(array, arrayIndex);
        public override bool Remove(ByteEntry item)
        {
            if (bytes == null || !bytes.Remove(item))
                return false;

            OnRemoved(item);
            return true;
        }
        
        protected override void UpdateAllParentInfo(bool shouldUnsetAll = false)
        {
            for (var i = 0; i < bytes.Count; ++i)
            {
                UpdateParentInfoFor(bytes[i], shouldUnsetAll, i);
            }
        }

        public override void CopyTo(Array array, int index) => bytes.CopyTo((ByteEntry[]) array, index);

        public override int Count => bytes?.Count ?? 0;

        // only ever use Add() to add bytes here
        private List<ByteEntry> bytes;
        
        [UsedImplicitly] public ByteStorageList() : base(0) { }
        
        public ByteStorageList(int emptyCreateSize) : base(emptyCreateSize) { }
        
        public ByteStorageList(IReadOnlyCollection<ByteEntry> inBytes) : base(inBytes) { }

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
                Add(new ByteEntry());
        }

        public override void Add(ByteEntry byteOffset)
        {
            Debug.Assert(bytes != null);
            
            var newIndex = Count; // will be true once we add it 
            SetParentInfoFor(byteOffset, newIndex);

            bytes.Add(byteOffset);
        }
        
        public override IEnumerator<ByteEntry> GetGaplessEnumerator() => bytes.GetEnumerator();
        
        // NOTE: in this implementation, all bytes at all addresses always exist, so,
        // this will never return null or have gaps in the sequence.
        //
        // other implementations can differ.
        public override IEnumerator<ByteEntry> GetNativeEnumerator() => GetGaplessEnumerator();
    }

    public class ByteStorageSparse : ByteStorage
    {
        [UsedImplicitly] public ByteStorageSparse() : base(0) { }
        public ByteStorageSparse(IReadOnlyCollection<ByteEntry> inBytes) : base(inBytes) { }
        public ByteStorageSparse(int emptyCreateSize) : base(emptyCreateSize) { }

        // keeps the keys sorted, which is what we want.
        private SortedDictionary<int, ByteEntry> bytes;

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

        public override void Clear()
        {
            OnPreClear();
            bytes.Clear();
        }

        public override bool Contains(ByteEntry item)
        {
            return item != null && bytes.Keys.Contains(item.ParentByteSourceIndex);
        }

        public override void CopyTo(ByteEntry[] array, int arrayIndex)
        {
            if (array.Length < Count)
                throw new ArgumentException(
                    "Destination array is not long enough to copy all the items in "+
                    "the collection. Check array index and length.");
            
            var x = CreateGapFillingEnumerator();
            while (x.MoveNext())
            {
                array[x.Position] = (ByteEntry) x.Current;
            }
        }

        public override bool Remove(ByteEntry item)
        {
            if (item == null)
                return false;

            Debug.Assert(item.ParentStorage != null);
            Debug.Assert(ValidIndex(item.ParentByteSourceIndex));
            
            return bytes.Remove(item.ParentByteSourceIndex);
        }

        public override void CopyTo(Array array, int index)
        {
            CopyTo((ByteEntry[])array, index);
        }

        private void SetByte(int index, ByteEntry value)
        {
            ValidateIndex(index);
            SetParentInfoFor(value, index);

            // will replace if it exists
            bytes[index] = value;
        }

        // we need to maintain this. it's not the # of bytes we're storing,
        // it's the max size of the sparse container. i.e. this will never change.
        private int count;
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
                Add(b);
                
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

        public override void Add(ByteEntry byteOffset)
        {
            // going to be a little weird. this would normally be "append" however it's
            // arbitrary where to do that with a dictionary. we wil interpret this as taking the highest
            // index (key i.e. SNES or ROM offset) and adding one to that.

            var largestKey = GetLargestKey();
            var indexThisWillBeAddedTo = largestKey + 1; // go one higher than our biggest

            ValidateIndex(indexThisWillBeAddedTo);
            SetParentInfoFor(byteOffset, indexThisWillBeAddedTo);
            
            bytes[indexThisWillBeAddedTo] = byteOffset;
            count++;
        }

        // for our normal enumerator facing client code, use our special enumerator
        // which will return sequential items but fill in the aps with Null.
        //
        // this is not the most efficient thing but makes the client code easier
        // to write. if performance becomes an issue, use GetTrueEnumerator() or GetNativeEnumerator()
        // which will just return the sections that have been populated.
        public override IEnumerator<ByteEntry> GetGaplessEnumerator()
        {
            return CreateGapFillingEnumerator();
        }

        private GapFillingEnumerator CreateGapFillingEnumerator()
        {
            return new(this);
        }

        // return only elements that actually exist (no gaps, no null items will be returned).
        public override IEnumerator<ByteEntry> GetNativeEnumerator()
        {
            return bytes.Select(pair => pair.Value).GetEnumerator();
        }

        protected override void UpdateAllParentInfo(bool shouldUnsetAll = false)
        {
            bytes.ForEach(pair => this.UpdateParentInfoFor(pair.Value, shouldUnsetAll, pair.Key));
        }

        // note: indices are going to be ordered, BUT there can be gaps.
        // caller should be prepared to handle this. 
        public SortedDictionary<int, ByteEntry>.Enumerator GetRealEnumerator()
        {
            return bytes.GetEnumerator();
        }
    }
}
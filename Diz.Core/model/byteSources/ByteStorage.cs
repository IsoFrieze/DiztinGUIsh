using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

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
}
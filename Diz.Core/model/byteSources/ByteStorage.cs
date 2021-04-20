using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;

// TODO: we can probably simplify this by replacing the parent class with ParentAwareCollection<ByteSource, ByteEntryBase>

namespace Diz.Core.model.byteSources
{
    public interface IParentAwareItem<TItem> where TItem : IParentAwareItem<TItem>
    {
        int ParentByteSourceIndex { get; internal set; }
        IStorage<TItem> ParentStorage { get; internal set; }
    }

    public interface IStorage<TItem> : ICollection<TItem>, ICollection where TItem : IParentAwareItem<TItem>
    {
        
    }

    public abstract class ByteStorage : Storage<ByteEntry>
    {
        protected internal ByteSource ParentByteSource { get; set; }
        
        [UsedImplicitly] public ByteStorage() : base(0) { }
        
        public ByteStorage(int emptyCreateSize) : base(emptyCreateSize) { }
        
        public ByteStorage(IReadOnlyCollection<ByteEntry> inBytes) : base(inBytes) { }
    }

    public abstract class Storage<TItem> : IStorage<TItem>
        where TItem : IParentAwareItem<TItem>, new()
    {
        public abstract TItem this[int index] { get; set; }

        public abstract void Add(TItem item);
        public abstract void Clear();
        public abstract bool Contains(TItem item);
        public abstract void CopyTo(TItem[] array, int arrayIndex);
        public abstract bool Remove(TItem item);
        public abstract void CopyTo(Array array, int index);
        public abstract int Count { get; }
        
        public bool IsReadOnly => false;
        public bool IsSynchronized => false;
        public object SyncRoot => default;

        protected Storage()
        {
            InitFromEmpty(0);
        }
        
        protected Storage(IReadOnlyCollection<TItem> inBytes)
        {
            InitFrom(inBytes);
        }

        protected Storage(int emptyCreateSize)
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

        private void InitFrom(IReadOnlyCollection<TItem> inBytes)
        {
            Debug.Assert(inBytes != null);
            
            InitEmptyContainer(inBytes.Count);
            FillEmptyContainerWithBytesFrom(inBytes);
            
            Debug.Assert(Count == inBytes.Count);
        }

        protected abstract void InitEmptyContainer(int emptyCreateSize);
        protected abstract void FillEmptyContainerWithBytesFrom(IReadOnlyCollection<TItem> inBytes);
        protected abstract void FillEmptyContainerWithBlankBytes(int numEntries);

        // GetEnumerator() will return an item at each index, or null if no Byte is present at that address.
        // this means, as long as clients check for null items during enumeration, the behavior will be the same
        // regardless of the internal storage type.
        //
        // This is potentially more inefficient but creates a consistent interface.
        // For performance-heavy code, or cases where you only only want non-null bytes, choose another enumerator function
        public abstract IEnumerator<TItem> GetGaplessEnumerator();
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetGaplessEnumerator();
        }

        public IEnumerator<TItem> GetEnumerator() => GetGaplessEnumerator();

        public abstract IEnumerator<TItem> GetNativeEnumerator();
        
        protected void ImportBytes(IReadOnlyCollection<TItem> inBytes)
        {
            Debug.Assert(inBytes != null);
            foreach (var b in inBytes)
            {
                Add(b);
            }
        }
        protected void OnRemoved(TItem item)
        {
            ClearParentInfoFor(item);
            UpdateAllParentInfo();
        }

        // refresh/rebuild parent info in each item.
        // update (or remove) all parent info when the collection has changed
        protected abstract void UpdateAllParentInfo(bool shouldUnsetAll = false);

        protected static void ClearParentInfoFor(TItem b) => SetParentInfoFor(b, -1, null);
        protected void SetParentInfoFor(TItem b, int index) => SetParentInfoFor(b, index, this);
        
        protected static void SetParentInfoFor(TItem b, int index, IStorage<TItem> parent)
        {
            b.ParentByteSourceIndex = index;
            b.ParentStorage = parent;
        }

        protected void UpdateParentInfoFor(TItem byteEntry, bool shouldUnsetAll, int newIndex)
        {
            if (shouldUnsetAll)
                ClearParentInfoFor(byteEntry);
            else
                SetParentInfoFor(byteEntry, newIndex);
        }

        protected void OnPreClear() => UpdateAllParentInfo(shouldUnsetAll: true);

        // iterate through a sparse ByteStorage class, if we encounter any gaps in the sequence,
        // fill them in 
        protected class GapFillingEnumerator : IEnumerator<TItem>
        {
            public Storage<TItem> Storage { get; protected set; }
            public int Position { get; set; } = -1;

            public GapFillingEnumerator(Storage<TItem> storage)
            {
                Debug.Assert(storage != null);
                Storage = storage;
            }
            public bool MoveNext()
            {
                Position++;
                return Position < Storage.Count;
            }

            public void Reset()
            {
                Position = -1;
            }

            TItem IEnumerator<TItem>.Current => Storage[Position];
            public object Current => Storage[Position];
            public void Dispose()
            {
                Position = -1;
                Storage = null;
            }
        }
    }
}
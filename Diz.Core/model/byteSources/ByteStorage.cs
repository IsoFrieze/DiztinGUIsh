using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml.Serialization;

// TODO: we can probably simplify most of this by replacing the parent class with ParentAwareCollection<ByteSource, ByteEntry>

namespace Diz.Core.model.byteSources
{
    
    // a child who keeps track of their parent AND INDEX
    public interface IParentReferenceTo<TParent>
    {
        int ParentIndex { get; set; }
        TParent Parent { get; set; } // TODO: change to TParent later if we can
    }

    // a child who keeps track of their parent (no index need be involved)
    // public interface IAmItemThatTracksMyParent<TParent>
    // {
    //     
    // }
    
    // this is hot garbage. replace with IList<T> and just suck it up and implement the rest.
    public interface IShouldReallyBeAListButIAmLazy<T> : ICollection<T>
    {
        T this[int index] { get; set; }
    }

    /*
    public interface IStorage<TItem> : IShouldReallyBeAListButIAmLazy<TItem> where
        // our items: Track their parent and parent index, and it must be a parent of IStorage<Item> (us)
        TItem : IParentReferenceTo<IStorage<TItem>>
    {
        
    }
    
    public interface IStorageWithParent<TItem, TOurParent> :
        // 1) we're storage that tracks an item type
        IStorage<TItem>,
        
        // 2) we are (unrelated) a child that also keeps track of our own (different) parent
        IAmItemThatTracksMyParent<TOurParent>
        
        where
        
        // we promise we're only storing items that know how to:
        // track their parent (us) and parentindex (index into us)
        TItem : IParentReferenceTo<IStorageWithParent<TItem, TOurParent>>
    {
        
    }

    public interface IByteStorage : Storage<ByteEntry> // add in for actual interfaces 
        // we are a class that stores TItems, and OUR parent is TOurParent
        //where 
        //TItemWeStore : IParentReferenceTo<IStorage<TItemWeStore>>
    {
        // IEnumerator<ByteEntry> GetNativeEnumerator();
    }*/

    public abstract class Storage<T> :
        IShouldReallyBeAListButIAmLazy<T>
        // // we are a class that stores TItems, and OUR parent is TOurParent
        // IStorageWithParent<TItemWeStore, TOurParent> 
        where 
        T : IParentReferenceTo<Storage<T>>
    {
        // don't get confused: this is the Parent holding THIS class.
        // it is unrelated to the items that WE hold, nor the fact that THEY also track
        // a parent (which their parent is US).
        // TODO: change to TParent if we can
        // TODO: disabled for serialization initial support. re-enable if we need it // public ByteSource Parent { get; } // (in Diz, this is ByteSource, which holds a Storage<ByteEntry>)
        public abstract int Count { get; }
        
        [XmlIgnore] public bool IsReadOnly => false;
        [XmlIgnore] public bool IsSynchronized => false;
        [XmlIgnore] public object SyncRoot => default;

        public abstract T this[int index] { get; set; }

        public abstract void Add(T item);
        public abstract void Clear();
        public abstract bool Contains(T item);
        public abstract void CopyTo(T[] array, int arrayIndex);
        public abstract bool Remove(T val);
        public abstract void CopyTo(Array array, int index);

        protected Storage()
        {
            InitFromEmpty(0);
        }
        
        protected Storage(IReadOnlyCollection<T> inBytes)
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

        private void InitFrom(IReadOnlyCollection<T> inBytes)
        {
            Debug.Assert(inBytes != null);
            
            InitEmptyContainer(inBytes.Count);
            FillEmptyContainerWithBytesFrom(inBytes);
            
            Debug.Assert(Count == inBytes.Count);
        }

        protected abstract void InitEmptyContainer(int emptyCreateSize);
        protected abstract void FillEmptyContainerWithBytesFrom(IReadOnlyCollection<T> inBytes);
        protected abstract void FillEmptyContainerWithBlankBytes(int numEntries);

        // GetEnumerator() will return an item at each index, or null if no Byte is present at that address.
        // this means, as long as clients check for null items during enumeration, the behavior will be the same
        // regardless of the internal storage type.
        //
        // This is potentially more inefficient but creates a consistent interface.
        // For performance-heavy code, or cases where you only only want non-null bytes, choose another enumerator function
        public abstract IEnumerator<T> GetGaplessEnumerator();
        public IEnumerator GetEnumerator()
        {
            return GetGaplessEnumerator();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetGaplessEnumerator();

        public abstract IEnumerator<T> GetNativeEnumerator();
        
        protected void ImportBytes(IReadOnlyCollection<T> inBytes)
        {
            Debug.Assert(inBytes != null);
            foreach (var b in inBytes)
            {
                Add(b);
            }
        }
        protected void OnRemoved(T item)
        {
            ClearParentInfoFor(item);
            UpdateAllParentInfo();
        }

        // refresh/rebuild parent info in each item.
        // update (or remove) all parent info when the collection has changed
        protected abstract void UpdateAllParentInfo(bool shouldUnsetAll = false);

        protected static void ClearParentInfoFor(T b) => SetParentInfoFor(b, -1, null);
        protected void SetParentInfoFor(T b, int index) => SetParentInfoFor(b, index, this);
        
        protected static void SetParentInfoFor(T b, int index, Storage<T> parent)
        {
            b.ParentIndex = index;
            b.Parent = parent;
        }

        protected void UpdateParentInfoFor(T byteEntry, bool shouldUnsetAll, int newIndex)
        {
            if (shouldUnsetAll)
                ClearParentInfoFor(byteEntry);
            else
                SetParentInfoFor(byteEntry, newIndex);
        }

        protected void OnPreClear() => UpdateAllParentInfo(shouldUnsetAll: true);
    }
    
    // iterate sequentially through a range in an IEnumerable<T> class like StorageSparse<ByteEntry>
    // if we encounter any gaps in the sequence fill them in with null. 
}
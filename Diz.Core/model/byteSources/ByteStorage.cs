using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

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

    public abstract class Storage<TItemWeStore> :
        IShouldReallyBeAListButIAmLazy<TItemWeStore>
        // // we are a class that stores TItems, and OUR parent is TOurParent
        // IStorageWithParent<TItemWeStore, TOurParent> 
        where 
        TItemWeStore : IParentReferenceTo<Storage<TItemWeStore>>
    {
        // don't get confused: this is the Parent holding THIS class.
        // it is unrelated to the items that WE hold, nor the fact that THEY also track
        // a parent (which their parent is US).
        // TODO: change to TParent if we can
        public ByteSource Parent { get; set; } // (in Diz, this is ByteSource, which holds a Storage<ByteEntry>)

        public abstract TItemWeStore this[int index] { get; set; }

        public abstract void Add(TItemWeStore item);
        public abstract void Clear();
        public abstract bool Contains(TItemWeStore item);
        public abstract void CopyTo(TItemWeStore[] array, int arrayIndex);
        public abstract bool Remove(TItemWeStore item);
        public abstract void CopyTo(Array array, int index);
        public abstract int Count { get; }
        
        public bool IsReadOnly => false;
        public bool IsSynchronized => false;
        public object SyncRoot => default;

        protected Storage()
        {
            InitFromEmpty(0);
        }
        
        protected Storage(IReadOnlyCollection<TItemWeStore> inBytes)
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

        private void InitFrom(IReadOnlyCollection<TItemWeStore> inBytes)
        {
            Debug.Assert(inBytes != null);
            
            InitEmptyContainer(inBytes.Count);
            FillEmptyContainerWithBytesFrom(inBytes);
            
            Debug.Assert(Count == inBytes.Count);
        }

        protected abstract void InitEmptyContainer(int emptyCreateSize);
        protected abstract void FillEmptyContainerWithBytesFrom(IReadOnlyCollection<TItemWeStore> inBytes);
        protected abstract void FillEmptyContainerWithBlankBytes(int numEntries);

        // GetEnumerator() will return an item at each index, or null if no Byte is present at that address.
        // this means, as long as clients check for null items during enumeration, the behavior will be the same
        // regardless of the internal storage type.
        //
        // This is potentially more inefficient but creates a consistent interface.
        // For performance-heavy code, or cases where you only only want non-null bytes, choose another enumerator function
        public abstract IEnumerator<TItemWeStore> GetGaplessEnumerator();
        public IEnumerator GetEnumerator()
        {
            return GetGaplessEnumerator();
        }

        IEnumerator<TItemWeStore> IEnumerable<TItemWeStore>.GetEnumerator() => GetGaplessEnumerator();

        public abstract IEnumerator<TItemWeStore> GetNativeEnumerator();
        
        protected void ImportBytes(IReadOnlyCollection<TItemWeStore> inBytes)
        {
            Debug.Assert(inBytes != null);
            foreach (var b in inBytes)
            {
                Add(b);
            }
        }
        protected void OnRemoved(TItemWeStore item)
        {
            ClearParentInfoFor(item);
            UpdateAllParentInfo();
        }

        // refresh/rebuild parent info in each item.
        // update (or remove) all parent info when the collection has changed
        protected abstract void UpdateAllParentInfo(bool shouldUnsetAll = false);

        protected static void ClearParentInfoFor(TItemWeStore b) => SetParentInfoFor(b, -1, null);
        protected void SetParentInfoFor(TItemWeStore b, int index) => SetParentInfoFor(b, index, this);
        
        protected static void SetParentInfoFor(TItemWeStore b, int index, Storage<TItemWeStore> parent)
        {
            b.ParentIndex = index;
            b.Parent = parent;
        }

        protected void UpdateParentInfoFor(TItemWeStore byteEntry, bool shouldUnsetAll, int newIndex)
        {
            if (shouldUnsetAll)
                ClearParentInfoFor(byteEntry);
            else
                SetParentInfoFor(byteEntry, newIndex);
        }

        protected void OnPreClear() => UpdateAllParentInfo(shouldUnsetAll: true);
    }
    
    // iterate through a sparse Storage<ByteEntry> class, if we encounter any gaps in the sequence,
    // fill them in 
    public class GapFillingEnumerator<T> : IEnumerator<T>
    {
        public IShouldReallyBeAListButIAmLazy<T> Collection { get; protected set; }
        public int Position { get; set; } = -1;

        public GapFillingEnumerator(IShouldReallyBeAListButIAmLazy<T> collection)
        {
            Debug.Assert(collection != null);
            Collection = collection;
        }
        public bool MoveNext()
        {
            Position++;
            return Position < Collection.Count;
        }

        public void Reset()
        {
            Position = -1;
        }

        T IEnumerator<T>.Current => Collection[Position];
        public object Current => Collection[Position];
        public void Dispose()
        {
            Position = -1;
            Collection = null;
        }
    }
}
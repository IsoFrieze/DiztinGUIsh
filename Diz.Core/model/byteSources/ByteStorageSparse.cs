using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace Diz.Core.model.byteSources
{
    public interface IStorageSparse<T> : IDictionary<int, T>, IDictionary
    {
        // public int Count { get; }
    }
    
    /// <summary>
    /// Sparse version of Storage which only stores bytes as they are created.
    /// Slower for indexed access, but, much better when you have a huge and mostly empty list
    /// i.e. great for stuff like emulator adress spaces which are huge and basically empty except
    /// for a few key areas.
    /// </summary>
    public class StorageSparse<T> : Storage<T>, IStorageSparse<T>
        where T : IParentReferenceTo<Storage<T>>
    {
        public override int Count => count;
        
        public int ActualCount => bytes.Count;

        private bool allowExpanding;
        public void SetAllowExpanding(bool val)
        {
            allowExpanding = true;
        }

        // keeps the keys sorted, which is important for us since we're
        // 1) (probably) iterating in order a lot
        // 2) need to compute the value of the largest key pretty often
        private SortedDictionary<int, T> bytes;
        
        // we need to maintain this. it's not the # of bytes we're storing,
        // it's the max size of the sparse container. i.e. this will never change.
        private int count;

        [UsedImplicitly] public StorageSparse() : this(0) { }

        public StorageSparse(IReadOnlyCollection<T> inBytes) : base(inBytes) { }

        public StorageSparse(int emptyCreateSize) : base(emptyCreateSize) { }
        
        [UsedImplicitly] 
        // used for serialization. every parameter needs to match each get-only property and this will be called.
        // for more info, see .EnableParameterizedContent() from ExtendedXmlSerializer, see
        // https://github.com/ExtendedXmlSerializer/home/wiki/Features#immutable-classes-and-content for the rules.
        public StorageSparse(int count /*SortedDictionary<int, T> bytesDict, */, int actualCount) 
            : this(count) // technically weird but...
        {
            Debug.Assert(this.count == count);
            
            // right now we don't actually need 'actualCount' but, can't remove it or else 
        }
        
        // TODO: maybe expose internal dictionary as property and keep it in the parameterized constructor
        
        private int GetLargestKey()
        {
            if (bytes == null || bytes.Count == 0)
                return -1;
            
            return bytes.Keys.Last();
        }

        public bool ContainsKey(int key)
        {
            return bytes.ContainsKey(key);
        }

        public bool TryGetValue(int key, out T value)
        {
            return bytes.TryGetValue(key, out value);
        }

        public override T this[int index]
        {
            get => GetByte(index);
            set => SetByte(index, value);
        }

        public object? this[object key]
        {
            get => GetByte((int)key);
            set => SetByte((int)key, (T)value);
        }

        public ICollection<int> Keys => bytes.Keys;
        ICollection IDictionary.Values => ((IDictionary) bytes).Values;

        ICollection IDictionary.Keys => ((IDictionary) bytes).Keys;

        public ICollection<T> Values => bytes.Values;

        public override void Clear()
        {
            OnPreClear();
            bytes.Clear();
        }

        public bool Contains(object key)
        {
            return ((IDictionary) bytes).Contains(key);
        }

        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            return ((IDictionary) bytes).GetEnumerator();
        }

        public bool IsFixedSize => ((IDictionary) bytes).IsFixedSize;

        public bool Contains(KeyValuePair<int, T> item)
        {
            return bytes.Contains(item);
        }

        public void CopyTo(KeyValuePair<int, T>[] array, int arrayIndex)
        {
            bytes.CopyTo(array, arrayIndex);
        }

        public override bool Contains(T item)
        {
            return item != null && bytes.Keys.Contains(item.ParentIndex);
        }

        public override void CopyTo(T[] array, int arrayIndex)
        {
            if (array.Length < Count)
                throw new ArgumentException(
                    "Destination array is not long enough to copy all the items in "+
                    "the collection. Check array index and length.");
            
            var x = CreateGapFillingEnumerator();
            while (x.MoveNext())
            {
                array[x.Position] = (T) x.Current;
            }
        }
        
        // TODO: write tests for these Remove()
        public bool Remove(KeyValuePair<int, T> kvp) => _RemoveKvp(kvp);
        public override bool Remove(T val) => _RemoveValue(val);
        public void Remove(object key) => _RemoveKey((int)key);
        public bool Remove(int key) => _RemoveKey(key);
        
        private bool _RemoveKvp(KeyValuePair<int, T> kvp)
        {
            var (key, value) = kvp;
            
            Debug.Assert(value.Parent != null);
            Debug.Assert(key == value.ParentIndex);
            
            return _RemoveKey(key);
        }
        
        private bool _RemoveKey(int key)
        {
            // we have to get the item so that we can adjust its parent data
            if (bytes == null || !bytes.TryGetValue(key, out var item)) 
                return false;

            // ok to be null. if we're not, verify parent data
            if (item != null)
            {
                Debug.Assert(item.ParentIndex == key);
                Debug.Assert(ValidIndex(item.ParentIndex));
                Debug.Assert(item.Parent != null);
            }

            // attempt to remove via item reference (works if item is non-null and in the dict)
            if (_RemoveValue(item))
                return true;
            
            // otherwise, remove via key. works OK if item is null 
            return bytes.Remove(key);
        }

        private bool _RemoveValue(T item)
        {
            if (item == null || bytes == null)
                return false;

            Debug.Assert(item.Parent != null);
            Debug.Assert(ValidIndex(item.ParentIndex));
            
            #if !DONT_CHECK_KEYS
            {
                // this isn't necessary, just an extra safety check. it could slow things down, disable if it matters.
                var (key, value) = bytes.FirstOrDefault(x => ReferenceEquals(x.Value, item));
                Debug.Assert(value != null);
                Debug.Assert(key == item.ParentIndex);
            }
            #endif

            if (!bytes.Remove(item.ParentIndex))
                return false;

            OnRemoved(item);
            return true;
        }

        public override void CopyTo(Array array, int index)
        {
            CopyTo((T[])array, index);
        }

        private void SetByte(int index, T value)
        {
            ValidateIndex(index);
            SetParentInfoFor(value, index);

            // will replace if it exists
            bytes[index] = value;
        }

        private bool ValidIndex(int index) => index >= 0 && index < Count;

        private void ValidateIndex(int index)
        {
            if (!ValidIndex(index))
                throw new ArgumentOutOfRangeException($"Index {index} out of range in SparseByteStorage");   
        }

        protected T GetByte(int index)
        {
            ValidateIndex(index);
            return bytes.GetValueOrDefault(index);
        }

        protected override void InitEmptyContainer(int emptyCreateSize)
        {
            bytes = new SortedDictionary<int, T>();
            count = emptyCreateSize;
        }
        
        public void Add(int key, T value)
        {
            this[key] = value;
            // bytes.Add(key, value);
        }
        
        public void Add(KeyValuePair<int, T> item)
        {
            var (key, value) = item;
            this[key] = value;
            // bytes.Add(key, value);
        }

        public void Add(object key, object? value)
        {
            this[key] = value;
            // ((IDictionary) bytes).Add(key, value);
        }

        protected override void FillEmptyContainerWithBytesFrom(IReadOnlyCollection<T> inBytes)
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

        public override void Add(T byteOffset)
        {
            // going to be a little weird. this would normally be "append" however it's
            // arbitrary where to do that with a dictionary. we wil interpret this as taking the highest
            // index (key i.e. SNES or ROM offset) and adding one to that.

            var indexThisWillBeAddedTo = ComputeNewIndexForAdd();

            // can happen during serialization, gracefully handle
            if (byteOffset != null)
            {
                SetParentInfoFor(byteOffset, indexThisWillBeAddedTo);
                bytes[indexThisWillBeAddedTo] = byteOffset;
            }
            
            count++;
        }

        private int ComputeNewIndexForAdd()
        {
            int indexThisWillBeAddedTo; // go one higher than our biggest
            if (!allowExpanding)
            {
                // normal: find the largest key already set, and add one to it. use that as the new key.
                var largestKey = GetLargestKey();
                indexThisWillBeAddedTo = largestKey + 1;
                
                ValidateIndex(indexThisWillBeAddedTo);
            }
            else
            {
                // expansion mode (used during deserialization)
                // in this mode, 'count' is not prefilled so will tick up even for null entries. use it
                // as the new index.
                //
                // NOTE: this will probably go away and can be re-unified once we get better serialization.
                // this is a little wacky.
                indexThisWillBeAddedTo = count;
            }
            
            return indexThisWillBeAddedTo;
        }

        // for our normal enumerator facing client code, use our special enumerator
        // which will return sequential items but fill in the aps with Null. this makes this collection
        // iterate like it's a list.
        //
        // this is not the most efficient thing but makes the client code easier
        // to write. if performance becomes an issue, use GetTrueEnumerator() or GetNativeEnumerator()
        // which will just return the sections that have been populated.
        //
        // *** For performance, use GetNativeEnumerator() instead where possible. *** 
        public override IEnumerator<T> GetGaplessEnumerator()
        {
            return CreateGapFillingEnumerator();
        }

        private GapFillingEnumerator<T> CreateGapFillingEnumerator()
        {
            return new(this);
        }

        // return only elements that actually exist (no gaps, no null items will be returned).
        // *** USE THIS WHENEVER POSSIBLE instead of GetGaplessEnumerator() *** 
        public override IEnumerator<T> GetNativeEnumerator()
        {
            return bytes.Select(pair => pair.Value).GetEnumerator();
        }

        protected override void UpdateAllParentInfo(bool shouldUnsetAll = false)
        {
            bytes.ForEach(pair => this.UpdateParentInfoFor(pair.Value, shouldUnsetAll, pair.Key));
        }

        // note: indices are going to be ordered, BUT there can be gaps.
        // caller should be prepared to handle this. 
        public SortedDictionary<int, T>.Enumerator GetRealEnumerator()
        {
            return bytes.GetEnumerator();
        }

        public IEnumerator<KeyValuePair<int, T>> GetEnumerator()
        {
            return bytes.GetEnumerator();
        }
    }
}
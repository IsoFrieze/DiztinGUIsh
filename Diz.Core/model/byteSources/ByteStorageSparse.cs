using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace Diz.Core.model.byteSources
{
    /// <summary>
    /// Sparse version of ByteStorage which only stores bytes as they are created.
    /// Slower for indexed access, but, much better when you have a huge and mostly empty list
    /// i.e. great for stuff like emulator adress spaces which are huge and basically empty except
    /// for a few key areas.
    /// </summary>
    public class StorageSparse<TItem> : Storage<TItem> 
        where TItem : IParentIndexAwareItem<TItem>, new()
    {
        [UsedImplicitly] public StorageSparse() : base(0) { }
        public StorageSparse(IReadOnlyCollection<TItem> inBytes) : base(inBytes) { }
        public StorageSparse(int emptyCreateSize) : base(emptyCreateSize) { }

        // keeps the keys sorted, which is what we want.
        private SortedDictionary<int, TItem> bytes;

        private int GetLargestKey()
        {
            if (bytes == null || bytes.Count == 0)
                return -1;
            
            return bytes.Keys.Last();
        }

        public override TItem this[int index]
        {
            get => GetByte(index);
            set => SetByte(index, value);
        }

        public override void Clear()
        {
            OnPreClear();
            bytes.Clear();
        }

        public override bool Contains(TItem item)
        {
            return item != null && bytes.Keys.Contains(item.ParentByteSourceIndex);
        }

        public override void CopyTo(TItem[] array, int arrayIndex)
        {
            if (array.Length < Count)
                throw new ArgumentException(
                    "Destination array is not long enough to copy all the items in "+
                    "the collection. Check array index and length.");
            
            var x = CreateGapFillingEnumerator();
            while (x.MoveNext())
            {
                array[x.Position] = (TItem) x.Current;
            }
        }

        public override bool Remove(TItem item)
        {
            if (item == null)
                return false;

            Debug.Assert(item.ParentStorage != null);
            Debug.Assert(ValidIndex(item.ParentByteSourceIndex));
            
            return bytes.Remove(item.ParentByteSourceIndex);
        }

        public override void CopyTo(Array array, int index)
        {
            CopyTo((TItem[])array, index);
        }

        private void SetByte(int index, TItem value)
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

        protected TItem GetByte(int index)
        {
            ValidateIndex(index);
            return bytes.GetValueOrDefault(index);
        }

        protected override void InitEmptyContainer(int emptyCreateSize)
        {
            bytes = new SortedDictionary<int, TItem>();
            count = emptyCreateSize;
        }

        protected override void FillEmptyContainerWithBytesFrom(IReadOnlyCollection<TItem> inBytes)
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

        public override void Add(TItem byteOffset)
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
        // which will return sequential items but fill in the aps with Null. this makes this collection
        // iterate like it's a list.
        //
        // this is not the most efficient thing but makes the client code easier
        // to write. if performance becomes an issue, use GetTrueEnumerator() or GetNativeEnumerator()
        // which will just return the sections that have been populated.
        //
        // *** For performance, use GetNativeEnumerator() instead where possible. *** 
        public override IEnumerator<TItem> GetGaplessEnumerator()
        {
            return CreateGapFillingEnumerator();
        }

        private GapFillingEnumerator CreateGapFillingEnumerator()
        {
            return new(this);
        }

        // return only elements that actually exist (no gaps, no null items will be returned).
        // *** USE THIS WHENEVER POSSIBLE instead of GetGaplessEnumerator() *** 
        public override IEnumerator<TItem> GetNativeEnumerator()
        {
            return bytes.Select(pair => pair.Value).GetEnumerator();
        }

        protected override void UpdateAllParentInfo(bool shouldUnsetAll = false)
        {
            bytes.ForEach(pair => this.UpdateParentInfoFor(pair.Value, shouldUnsetAll, pair.Key));
        }

        // note: indices are going to be ordered, BUT there can be gaps.
        // caller should be prepared to handle this. 
        public SortedDictionary<int, TItem>.Enumerator GetRealEnumerator()
        {
            return bytes.GetEnumerator();
        }
    }
}
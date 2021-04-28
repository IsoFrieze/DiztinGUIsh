using System;
using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;

namespace Diz.Core.model.byteSources
{
    /// <summary>
    /// Simple version of byte storage that stores everything as an actual list
    /// Use for linear filled data (like Roms), don't use for mostly empty large storage (like SNES Address space)
    /// address spaces (24bits of addressable bytes x HUGE data = slowwwww) 
    /// </summary>
    public class StorageList<T> : Storage<T> 
        where 
        T : IParentReferenceTo<Storage<T>>, new()
    {
        public override T this[int index]
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

        public override bool Contains(T item) => bytes?.Contains(item) ?? false;
        public override void CopyTo(T[] array, int arrayIndex) => bytes.CopyTo(array, arrayIndex);
        public override bool Remove(T item)
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

        public override void CopyTo(Array array, int index) => bytes.CopyTo((T[]) array, index);

        public override int Count => bytes?.Count ?? 0;

        // only ever use Add() to add bytes here
        private List<T> bytes;
        
        [UsedImplicitly] public StorageList() : base(0) { }
        
        public StorageList(int emptyCreateSize) : base(emptyCreateSize) { }
        
        public StorageList(IReadOnlyCollection<T> inBytes) : base(inBytes) { }

        protected override void InitEmptyContainer(int capacity)
        {
            bytes = new List<T>(capacity);
        }

        protected override void FillEmptyContainerWithBytesFrom(IReadOnlyCollection<T> inBytes)
        {
            ImportBytes(inBytes);
        }
        
        protected override void FillEmptyContainerWithBlankBytes(int numEntries)
        {
            for (var i = 0; i < numEntries; ++i) 
                Add(new T());
        }

        public override void Add(T byteOffset)
        {
            Debug.Assert(bytes != null);
            
            var newIndex = Count; // will be true once we add it 
            SetParentInfoFor(byteOffset, newIndex);

            bytes.Add(byteOffset);
        }
        
        public override IEnumerator<T> GetGaplessEnumerator() => bytes.GetEnumerator();
        
        // NOTE: in this implementation, all bytes at all addresses always exist, so,
        // this will never return null or have gaps in the sequence.
        //
        // other implementations can differ.
        public override IEnumerator<T> GetNativeEnumerator() => GetGaplessEnumerator();
    }
}
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
}
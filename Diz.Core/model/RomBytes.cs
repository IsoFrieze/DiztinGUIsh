using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Diz.Core.model
{
    public class RomBytes : IEnumerable<ROMByte>
    {
        // TODO: might be able to do something more generic now that other refactorings are completed.
        //
        // This class needs to do these things that are special:
        // 1) Be handled specially by our custom XML serializer (compresses to save disk space)
        // 2) Handle Equals() by comparing each element in the list (SequenceEqual)
        public List<ROMByte> Bytes { get; } = new List<ROMByte>();
        public ROMByte this[int i]
        {
            get => Bytes[i];
            set => Bytes[i] = value;
        }
        
        public int Count => Bytes.Count;
        public void Add(ROMByte romByte)
        {
            Bytes.Add(romByte);
        }

        public void Create(int size)
        {
            for (var i = 0; i < size; ++i)
                Add(new ROMByte());
        }
        public void Clear()
        {
            Bytes.Clear();
        }

        #region Equality
        protected bool Equals(RomBytes other)
        {
            return Bytes.SequenceEqual(other.Bytes);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RomBytes)obj);
        }

        public override int GetHashCode()
        {
            return (Bytes != null ? Bytes.GetHashCode() : 0);
        }
        #endregion

        #region Enumerator
        public IEnumerator<ROMByte> GetEnumerator()
        {
            return Bytes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion
    }
}

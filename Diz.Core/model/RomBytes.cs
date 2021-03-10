using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace Diz.Core.model
{
    // TODO: might be able to do something more generic for RomBytes now that other refactorings are completed.
    // This class needs to do these things that are special:
    // 1) Be handled specially by our custom XML serializer (compresses to save disk space)
    // 2) Handle Equals() by comparing each element in the list (SequenceEqual)
    // 3) Emit notifypropertychanged if any members change
    // 4) Participate as a databinding source for winforms controls (usually via an intermediate object)
    public class RomBytes : IList<RomByte>, IList, INotifyCollectionChanged, INotifyPropertyChanged
    {
        private sealed class BytesEqualityComparer : IEqualityComparer<RomBytes>
        {
            public bool Equals(RomBytes x, RomBytes y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return Equals(x.bytes, y.bytes);
            }

            public int GetHashCode(RomBytes obj)
            {
                return (obj.bytes != null ? obj.bytes.GetHashCode() : 0);
            }
        }

        public static IEqualityComparer<RomBytes> BytesComparer { get; } = new BytesEqualityComparer();

        private ObservableCollection<RomByte> bytes;
        
        private ObservableCollection<RomByte> Bytes
        {
            get => bytes;
            set
            {
                bytes = value;

                bytes.CollectionChanged += Bytes_CollectionChanged;
                foreach (var romByte in bytes)
                {
                    romByte.PropertyChanged += RomByteObjectChanged;
                }
            }
        }

        public int IndexOf(RomByte item)
        {
            return bytes.IndexOf(item);
        }

        public void Insert(int index, RomByte item)
        {
            bytes.Insert(index, item);
        }

        public void Remove(object? value)
        {
            ((IList) bytes).Remove(value);
        }

        public void RemoveAt(int index)
        {
            bytes.RemoveAt(index);
        }

        public bool IsFixedSize => ((IList) bytes).IsFixedSize;

        public RomByte this[int i]
        {
            get => Bytes[i];
            set => Bytes[i] = value;
        }

        public ArraySegment<RomByte> GetArraySegment(int offset, int count)
        {
            return new(bytes.ToArray(), offset, count);
        }

        public RomBytes()
        {
            Bytes = new ObservableCollection<RomByte>();
        }

        public void SetFrom(RomByte[] romBytes)
        {
            Bytes = new ObservableCollection<RomByte>(romBytes);
            for (var i = 0; i < romBytes.Length; ++i)
            {
                romBytes[i].SetCachedOffset(i);
            }
        }

        public bool Remove(RomByte item)
        {
            return bytes.Remove(item);
        }

        public void CopyTo(Array array, int index)
        {
            ((ICollection) bytes).CopyTo(array, index);
        }

        public int Count => Bytes.Count;
        public bool IsSynchronized => ((ICollection) bytes).IsSynchronized;

        public object SyncRoot => ((ICollection) bytes).SyncRoot;

        public bool IsReadOnly => false; // probably ok?
        object? IList.this[int index]
        {
            get => ((IList) bytes)[index];
            set => ((IList) bytes)[index] = value;
        }

        public bool SendNotificationChangedEvents { get; set; } = true;

        public void Add(RomByte romByte)
        {
            Bytes.Add(romByte);
            romByte.SetCachedOffset(Bytes.Count - 1); // I don't love this....
        }

        public void Create(int size)
        {
            for (var i = 0; i < size; ++i)
                Add(new RomByte());
        }

        public int Add(object? value)
        {
            return ((IList) bytes).Add(value);
        }

        public void Clear()
        {
            Bytes.Clear();
        }

        public bool Contains(object? value)
        {
            return ((IList) bytes).Contains(value);
        }

        public int IndexOf(object? value)
        {
            return ((IList) bytes).IndexOf(value);
        }

        public void Insert(int index, object? value)
        {
            ((IList) bytes).Insert(index, value);
        }

        public bool Contains(RomByte item)
        {
            return bytes.Contains(item);
        }

        public void CopyTo(RomByte[] array, int arrayIndex)
        {
            bytes.CopyTo(array, arrayIndex);
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
        public IEnumerator<RomByte> GetEnumerator()
        {
            return Bytes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion

        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        private void Bytes_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (RomByte item in e.NewItems)
                    item.PropertyChanged += RomByteObjectChanged;

            if (e.OldItems != null)
                foreach (RomByte item in e.OldItems)
                    item.PropertyChanged -= RomByteObjectChanged;

            if (SendNotificationChangedEvents)
                CollectionChanged?.Invoke(sender, e);
        }

        private void RomByteObjectChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (SendNotificationChangedEvents)
                PropertyChanged?.Invoke(sender, e);
        }
    }
}

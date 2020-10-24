using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using IX.Observable;

namespace Diz.Core.model
{
    public class RomBytes : IEnumerable<ROMByte>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        // TODO: might be able to do something more generic now that other refactorings are completed.
        //
        // This class needs to do these things that are special:
        // 1) Be handled specially by our custom XML serializer (compresses to save disk space)
        // 2) Handle Equals() by comparing each element in the list (SequenceEqual)
        public ObservableCollection<ROMByte> Bytes { get; } = new ObservableCollection<ROMByte>();
        public ROMByte this[int i]
        {
            get => Bytes[i];
            set => Bytes[i] = value;
        }

        public RomBytes()
        {
            // Bytes.PropertyChanged += Bytes_PropertyChanged;
            Bytes.CollectionChanged += Bytes_CollectionChanged;
        }

        public int Count => Bytes.Count;
        public bool SendNotificationChangedEvents { get; set; } = true;

        public void Add(ROMByte romByte)
        {
            Bytes.Add(romByte);
            romByte.SetCachedOffset(Bytes.Count - 1); // I don't love this....
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

        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        private void Bytes_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (ROMByte item in e.NewItems)
                    item.PropertyChanged += RomByteObjectChanged;

            if (e.OldItems != null)
                foreach (ROMByte item in e.OldItems)
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

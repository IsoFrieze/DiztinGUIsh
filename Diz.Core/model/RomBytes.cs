#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using IX.Observable;
using JetBrains.Annotations;

namespace Diz.Core.model
{
    public interface IDataManager : INotifyPropertyChanged
    {
        
    }

    public interface IDizObservable<T> : ICollection<T>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        
    }
    
    public interface IDizObservableList<T> : IDizObservable<T>
    {
        
    }
    //
    // public class DataManagerRomBytes : DataManagerList<ByteOffset, Observable>
    // {
    //     
    // }

    public class DataManagerList<TItem, TObservableList> : DataManager<TItem, TObservableList>
        where TItem : INotifyPropertyChanged, new()
        where TObservableList : IDizObservableList<TItem>, new()
    {
        
    }

    // does two things:
    // 1) stores and manages data in a particular type of list.
    // 2) handles routing the events from the inner collections out 
    public class DataManager<TItem, TObservableCollection> : IDataManager
        where TItem : INotifyPropertyChanged, new()
        where TObservableCollection : IDizObservable<TItem>, new()
    {
        public CollectionItemObserver<TItem>? CollectionObserver { get; set; }

        public DataManager()
        {
            if (CollectionObserver != null)
            {
                CollectionObserver.CollectionItemPropertyChanged += CollectionItemPropertyChanged;
                CollectionObserver.CollectionChanged += CollectionOnCollectionChanged;
            }
        }

        private TObservableCollection dataSource = new();

        public TObservableCollection DataSource
        {
            get => dataSource;
            set
            {
                var prevNotifyState = CollectionObserver?.ChangeNotificationsEnabled ?? false; 

                if (CollectionObserver != null)
                    CollectionObserver.ChangeNotificationsEnabled = false;
                
                if (!NotifyPropertyChangedExtensions.FieldIsEqual(dataSource, value))
                {
                    dataSource = value;
                    // TODO : CollectionObserver.Collection = dataSource;
                }

                // TODO: ResetCachedOffsets();

                if (CollectionObserver != null)
                    CollectionObserver.ChangeNotificationsEnabled = prevNotifyState;
                
                OnPropertyChanged();
            }
        }

        private void CollectionOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // when dataSource collection changes. NOT individual items in the list.
            // TODO ResetCachedOffsets();
        }

        private void CollectionItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // when any individual ByteOffset changes. nothing to do with the list.
            PropertyChanged?.Invoke(sender, e);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    // raise notifications when either 1) a collection changes or 2) items inside a collection change
    public class CollectionItemObserver<T> : INotifyCollectionChanged
    where T : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? CollectionItemPropertyChanged;
        public bool ChangeNotificationsEnabled { get; set; } = true;
        
        private ObservableCollection<T>? collection;
        public ObservableCollection<T>? Collection
        {
            get => collection;
            set
            {
                if (collection != null)
                    collection.CollectionChanged -= CollectionOnCollectionChanged; 
                
                SetupPropertyChangedOn(null, collection);
                
                collection = value;
                
                SetupPropertyChangedOn(collection, null);
                
                if (collection != null)
                    collection.CollectionChanged += CollectionOnCollectionChanged;
            }
        }

        private void CollectionOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            SetupPropertyChangedOn(e.NewItems, e.OldItems);
            
            if (ChangeNotificationsEnabled)
                CollectionChanged?.Invoke(sender, e);
        }
        
        private void OnCollectionItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            CollectionItemPropertyChanged?.Invoke(sender, e);
        }

        private void SetupPropertyChangedOn(IEnumerable? newItems, IEnumerable? oldItems)
        {
            if (newItems != null)
                foreach (T item in newItems)
                    item.PropertyChanged += OnCollectionItemPropertyChanged;

            if (oldItems != null)
                foreach (T item in oldItems)
                    item.PropertyChanged -= OnCollectionItemPropertyChanged;
        }

        public event NotifyCollectionChangedEventHandler? CollectionChanged;
    }
    
    // raises both CollectionChanged events for the collection, and PropertyChanged on any item in the collection
    /*public class ItemObservableCollection<T> : ObservableCollection<T>
    {
        public ItemObservableCollection() {}
        
        public ItemObservableCollection(IEnumerable<T> items) : base(items)
        {
            // SetupPropertyChangedOn(items, null);
        }

        /*protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            SetupPropertyChangedOn(e.NewItems, e.OldItems);
            
            // if (SendNotificationChangedEvents)
            base.OnCollectionChanged(e);
        }

        protected virtual void OnCollectionItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // if (SendNotificationChangedEvents)
                PropertyChanged?.Invoke(sender, e);
        }#1#
        
        // TODO
        /*private bool Equals(RomBytes other)
        {
            // important
            return Bytes.SequenceEqual(other.Bytes);
        }#1#
    }*/
    
    /*
    // TODO: This class is a hot mess after all the refactorings.
    // We likely shouldn't be implementing any list-related stuff directly anymore.
    //
    // This class needs to do these things that are special:
    // 1) Be handled specially by our custom XML serializer (compresses to save disk space)
    // 2) Handle Equals() by comparing each element in the list (SequenceEqual)
    // 3) Emit notifypropertychanged if any members change
    // 4) Participate as a databinding source for winforms controls (usually via an intermediate object)
    // 5) Adding ByteOffset objects should set their "cached offset"
    public class RomBytes2 : IList<ByteOffset>, IList, INotifyCollectionChanged, INotifyPropertyChanged
    {
        #region Actual Custom Logic, keep
        private ObservableCollection<ByteOffset> bytes = new();
        private ObservableCollection<ByteOffset> Bytes
        {
            get => bytes;
            set
            {
                bytes = value;
                bytes.CollectionChanged += OnCollectionChanged;
                
                SetupCachedOffsets();
                SetupPropertyChangedOn(bytes, null);
            }
        }

        private void SetupCachedOffsets()
        {
            for (var i = 0; i < bytes.Count; ++i)
                bytes[i].SetCachedOffset(i);
        }

        public void SetFrom(IEnumerable<ByteOffset> romBytes)
        {
            Bytes = new ObservableCollection<ByteOffset>(romBytes);
        }

        public void Add(ByteOffset byteOffset)
        {
            Bytes.Add(byteOffset); // regular.
            byteOffset.SetCachedOffset(Bytes.Count - 1); // CUSTOM. IMPORTANT.
        }

        #endregion
        
        #region NotifyPropertyChanged and NotifyCollectionChanged
        
        public bool SendNotificationChangedEvents { get; set; } = true;
        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            SetupPropertyChangedOn(e.NewItems, e.OldItems);

            if (SendNotificationChangedEvents)
                CollectionChanged?.Invoke(sender, e);
        }

        private void SetupPropertyChangedOn(IEnumerable newItems, IEnumerable oldItems)
        {
            if (newItems != null)
                foreach (ByteOffset item in newItems)
                    item.PropertyChanged += OnCollectionItemPropertyChanged;

            if (oldItems != null)
                foreach (ByteOffset item in oldItems)
                    item.PropertyChanged -= OnCollectionItemPropertyChanged;
        }

        private void OnCollectionItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (SendNotificationChangedEvents)
                PropertyChanged?.Invoke(sender, e);
        }
        
        #endregion

        #region Implementation Boilerplate - Nothing interesting in here.

        public int IndexOf(ByteOffset item) => bytes.IndexOf(item);
        public void Insert(int index, ByteOffset item) => bytes.Insert(index, item);
        public void Remove(object? value) => ((IList) bytes).Remove(value);
        public void RemoveAt(int index) => bytes.RemoveAt(index);
        public bool IsFixedSize => ((IList) bytes).IsFixedSize;
        public bool Remove(ByteOffset item) => bytes.Remove(item);
        public void CopyTo(Array array, int index) => ((ICollection) bytes).CopyTo(array, index);
        public int Count => Bytes.Count;
        public bool IsSynchronized => ((ICollection) bytes).IsSynchronized;
        public object SyncRoot => ((ICollection) bytes).SyncRoot;
        public bool IsReadOnly => false; // probably ok?
        public int Add(object? value) => ((IList) bytes).Add(value);
        public void Clear() => Bytes.Clear();
        public bool Contains(object? value) => ((IList) bytes).Contains(value);
        public int IndexOf(object? value) => ((IList) bytes).IndexOf(value);
        public void Insert(int index, object? value) => ((IList) bytes).Insert(index, value);
        public bool Contains(ByteOffset item) => bytes.Contains(item);
        public void CopyTo(ByteOffset[] array, int arrayIndex) => bytes.CopyTo(array, arrayIndex);

        public IEnumerator<ByteOffset> GetEnumerator() => Bytes.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public ByteOffset this[int i]
        {
            get => Bytes[i];
            set => Bytes[i] = value;
        }

        object? IList.this[int index]
        {
            get => ((IList) bytes)[index];
            set => ((IList) bytes)[index] = value;
        }
        #endregion

        #region Equality
        
        private sealed class BytesEqualityComparer : IEqualityComparer<RomBytes2>
        {
            public bool Equals(RomBytes2 x, RomBytes2 y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return Equals(x.bytes, y.bytes);
            }

            public int GetHashCode(RomBytes2 obj)
            {
                return (obj.bytes != null ? obj.bytes.GetHashCode() : 0);
            }
        }

        public static IEqualityComparer<RomBytes2> BytesComparer { get; } = new BytesEqualityComparer();

        private bool Equals(RomBytes2 other)
        {
            // important
            return Bytes.SequenceEqual(other.Bytes);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RomBytes) obj);
        }

        public override int GetHashCode()
        {
            return (Bytes != null ? Bytes.GetHashCode() : 0);
        }

        #endregion
    }*/
}
#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Diz.Core.util;
using JetBrains.Annotations;

namespace Diz.Core.model
{

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
        }

        private void CollectionItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // when any individual ByteOffset changes. nothing to do with the list.
            PropertyChanged?.Invoke(sender, e);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
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
}
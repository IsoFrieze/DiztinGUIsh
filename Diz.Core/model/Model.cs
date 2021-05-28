/*
namespace Diz.Core.model
{

    /// <summary>
    ///     This class adds the ability to refresh the list when any property of
    ///     the objects changes in the list which implements the INotifyPropertyChanged. 
    /// </summary>
    public class ItemsChangeObservableCollection<TKey, TValue> :
        ObservableDictionary<TKey, TValue> where TValue : INotifyPropertyChanged
    {
        /*
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                RegisterPropertyChanged(e.NewItems);
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                UnRegisterPropertyChanged(e.OldItems);
            }
            else if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                UnRegisterPropertyChanged(e.OldItems);
                RegisterPropertyChanged(e.NewItems);
            }

            base.OnCollectionChanged(e);
        }

        protected override void ClearItems()
        {
            UnRegisterPropertyChanged(this);
            base.ClearItems();
        }

        private void RegisterPropertyChanged(IList items)
        {
            foreach (INotifyPropertyChanged item in items)
            {
                if (item != null)
                {
                    item.PropertyChanged += new PropertyChangedEventHandler(item_PropertyChanged);
                }
            }
        }

        private void UnRegisterPropertyChanged(IList items)
        {
            foreach (INotifyPropertyChanged item in items)
            {
                if (item != null)
                {
                    item.PropertyChanged -= new PropertyChangedEventHandler(item_PropertyChanged);
                }
            }
        }

        private void item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
        
    }#1#

    /*public class Watcher<TK, TV> : INotifyPropertyChanged, INotifyCollectionChanged where TV : INotifyPropertyChanged
    {
        public ObservableDictionary<TK, TV> Dict { get; init; }

        public Watcher()
        {
            Dict.CollectionChanged += DictOnCollectionChanged;
        }

        public bool SendNotificationChangedEvents { get; set; } = true;

        private void DictOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (TV item in e.NewItems)
                    item.PropertyChanged += IndividualPropChanged;

            if (e.OldItems != null)
                foreach (TV item in e.OldItems)
                    item.PropertyChanged -= IndividualPropChanged;

            if (SendNotificationChangedEvents)
                CollectionChanged?.Invoke(sender, e);
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        private void IndividualPropChanged(object sender, PropertyChangedEventArgs e)
        {
            if (SendNotificationChangedEvents)
                PropertyChanged?.Invoke(sender, e);
        }
    }
}
*/

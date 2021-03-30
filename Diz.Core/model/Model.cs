using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Diz.Core.model
{
    // makes it a little easier to deal with INotifyPropertyChanged in derived classes
    public interface INotifyPropertyChangedExt : INotifyPropertyChanged
    {
        // would be great if this didn't have to be public. :shrug:
        void OnPropertyChanged(string propertyName);
    }
    
    public static class NotifyPropertyChangedExtensions
    {
        // returns true if we set property to a new value
        public static bool SetField<T>(this INotifyPropertyChanged sender, PropertyChangedEventHandler handler, ref T field, T value, bool compareRefOnly = false, [CallerMemberName] string propertyName = null)
        {
            if (!FieldCompare(field, value, compareRefOnly)) 
                return false;
            
            field = value;
            
            handler?.Invoke(sender, new PropertyChangedEventArgs(propertyName));
            return true;
        }
        
        // returns true if we set property to a new value
        public static bool SetField<T>(this INotifyPropertyChangedExt sender, ref T field, T value, bool compareRefOnly = false, [CallerMemberName] string propertyName = null)
        {
            if (!FieldCompare(field, value, compareRefOnly)) 
                return false;
            
            field = value;
            
            sender.OnPropertyChanged(propertyName);
            return true;
        }

        public static bool FieldCompare<T>(T field, T value, bool compareRefOnly = false)
        {
            if (compareRefOnly)
            {
                if (ReferenceEquals(field, value))
                    return false;
            }
            else if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    ///     This class adds the ability to refresh the list when any property of
    ///     the objects changes in the list which implements the INotifyPropertyChanged. 
    /// </summary>
    /*public class ItemsChangeObservableCollection<TKey, TValue> :
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
        
    }*/

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
    }*/
}

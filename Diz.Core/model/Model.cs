using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using IX.Observable;
using JetBrains.Annotations;

namespace DiztinGUIsh
{
    /*public class INotifyPropertyChanged : PropertyNotifyChanged
    {

    }*/

    /*TO DELETE
     public class TestNotifyChanged : INotifyPropertyChanged
    {
        private string name;
        public string Name
        {
            get => name;
            set => this.SetField(PropertyChanged, ref name, value);
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
    }*/
    
    public static class INotifyPropertyChangedExtensions
    {
        public static void Notify(
            this INotifyPropertyChanged sender,
            PropertyChangedEventHandler handler,
            [CallerMemberName] string propertyName = "")
        {
            handler?.Invoke(sender, new PropertyChangedEventArgs(propertyName));
        }
        
        public static bool SetField<T>(this INotifyPropertyChanged sender, PropertyChangedEventHandler handler, ref T field, T value, bool compareRefOnly = false, [CallerMemberName] string propertyName = null)
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
            field = value;
            
            handler?.Invoke(sender, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    /*public class PropertyNotifyChanged : INotifyPropertyChanged
    {
        // this stuff lets other parts of code subscribe to events that get fired anytime
        // properties of our class change.
        //
        // Just hook up SetField() to the 'set' param of any property you would like to 
        // expose to outside classes.
        public event PropertyChangedEventHandler PropertyChanged;
        protected bool SetField<T>(ref T field, T value, bool compareRefOnly = false, [CallerMemberName] string propertyName = null)
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

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }*/

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

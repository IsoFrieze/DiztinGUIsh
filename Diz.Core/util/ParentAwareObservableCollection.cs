using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using JetBrains.Annotations;

namespace Diz.Core.util
{
    public interface IParentAware<in TParent>
    {
        public void OnParentChanged(TParent parent);
    }

    public class ParentAwareObservableCollection<TParent, TItem> : ObservableCollection<TItem>
        where TParent : class
        where TItem : IParentAware<TParent>, INotifyPropertyChanged
    {
        private TParent parent;
        [XmlIgnore] 
        public bool DontSetParentOnCollectionItems { get; set; }

        [PublicAPI]
        [XmlIgnore] 
        public TParent Parent
        {
            get => parent;
            set
            {
                parent = value;
                SetAllItemParentsTo(parent);
            }
        }

        private void SetItemParent(TItem item, TParent newParent)
        {
            if (DontSetParentOnCollectionItems)
                return;
            
            item?.OnParentChanged(newParent);
        }

        private void SetAllItemParentsTo(TParent newParent)
        {
            foreach (var item in this)
            {
                SetItemParent(item, newParent);
            }
        }

        protected override void ClearItems()
        {
            SetAllItemParentsTo(null);
            foreach (var item in this)
            {
                if (item != null)
                    item.PropertyChanged -= ItemOnPropertyChanged;
            }
            base.ClearItems();
        }

        private void ItemOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            
        }

        protected override void InsertItem(int index, TItem item)
        {
            base.InsertItem(index, item);
            if (item != null)
                item.PropertyChanged += ItemOnPropertyChanged;
            SetItemParent(item, Parent);
        }

        protected override void RemoveItem(int index)
        {
            var item = Items[index];
            SetItemParent(item, null);
            if (item != null)
                item.PropertyChanged -= ItemOnPropertyChanged;
            
            base.RemoveItem(index);
        }

        protected override void SetItem(int index, TItem item)
        {
            {
                var existing = Items[index];
                if (existing != null)
                    existing.PropertyChanged -= ItemOnPropertyChanged;
            }

            base.SetItem(index, item);
            if (item != null)
                item.PropertyChanged += ItemOnPropertyChanged;
            
            SetItemParent(item, Parent);
        }

        protected virtual bool Equals(ParentAwareObservableCollection<TParent, TItem> other)
        {
            // might not be a great idea to override Equals() in list.
            // for now, try it.
            
            if (Util.BothListsNullOrContainNoItems(Items, other?.Items))
                return true;

            if (Items.Count != other?.Items.Count)
                return false;
            
            return Items.SequenceEqual(other.Items);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ParentAwareObservableCollection<TParent, TItem>) obj);
        }

        public override int GetHashCode()
        {
            // not... super-confident in this?
            var hashCode = 0;
            foreach (var item in Items)
            {
                hashCode = (hashCode * 397) ^ item.GetHashCode();
            }

            return hashCode;
        }

        public static bool operator ==(ParentAwareObservableCollection<TParent, TItem> left, ParentAwareObservableCollection<TParent, TItem> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ParentAwareObservableCollection<TParent, TItem> left, ParentAwareObservableCollection<TParent, TItem> right)
        {
            return !Equals(left, right);
        }
    }
}
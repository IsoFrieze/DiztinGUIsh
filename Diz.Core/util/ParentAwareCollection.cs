using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JetBrains.Annotations;

namespace Diz.Core.util
{
    public interface IParentAware<in TParent>
    {
        public void OnParentChanged(TParent parent);
    }

    public class ParentAwareCollection<TParent, TItem> : ParentAwareCollectionBase<TParent, TItem>
        where TParent : class
        where TItem : IParentAware<TParent>
    {
        public void RemoveAll(Predicate<TItem> match)
        {
            if (match == null)
                throw new ArgumentNullException(nameof(match));

            this.Where(item => match(item)).ToList().ForEach(item => Remove(item));
        }
        
        public void AddRange(IEnumerable<TItem> newItems)
        {
            foreach (var newItem in newItems)
            {
                Add(newItem);
            }
        }
    }

    public class ParentAwareCollectionBase<TParent, TItem> : Collection<TItem>
        where TParent : class
        where TItem : IParentAware<TParent>
    {
        private TParent parent;
        public bool DontSetParentOnCollectionItems { get; set; }

        [PublicAPI]
        public TParent Parent
        {
            get => parent;
            set
            {
                parent = value;
                UpdateAllItemParent(parent);
            }
        }

        private void SetItemParent(TItem item, TParent newParent)
        {
            if (DontSetParentOnCollectionItems)
                return;
            
            item?.OnParentChanged(newParent);
        }

        private void UpdateAllItemParent(TParent newParent)
        {
            foreach (var item in this)
            {
                SetItemParent(item, newParent);
            }
        }

        protected override void ClearItems()
        {
            UpdateAllItemParent(null);
            base.ClearItems();
        }

        protected override void InsertItem(int index, TItem item)
        {
            base.InsertItem(index, item);
            SetItemParent(item, Parent);
        }

        protected override void RemoveItem(int index)
        {
            var item = Items[index];
            SetItemParent(item, null);
            base.RemoveItem(index);
        }

        protected override void SetItem(int index, TItem item)
        {
            base.SetItem(index, item);
            SetItemParent(item, Parent);
        }
    }
}
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

    public class ParentAwareCollection<TParent, TItem> : Collection<TItem>
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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Diz.Core.datasubset;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.model.snes;
using Diz.Core.util;
using DiztinGUIsh.util;
using JetBrains.Annotations;

// this class and structure is a mess because it was just enough refactoring to 
// get a lot of this logic out of the implementation classes.  it needs further iteration and simplification
//
// 1) TODO: need to catch notifychanged from labels and comments or else updates won't propagate

namespace DiztinGUIsh.controller
{
    public class RomByteDataBindingGridController : RomByteDataBindingController
    {
        public void BeginEditingLabel()
        {
            ViewGrid.BeginEditingSelectionLabel();
        }

        public void BeginEditingComment()
        {
            ViewGrid.BeginEditingSelectionComment();
        }
    }

    public class RomByteDataBindingController : 
        ByteViewerDataBindingGridController<RomByteDataGridRow, ByteEntry>
    {
        protected override IEnumerable<ByteEntry> GetByteItems()
        {
            // TODO: note: underlying data source could be a sparse set, so, enumerating here we
            // may have to deal with null bytes or gaps in the sequence.
            
            // right now, return everything 1:1.
            // in the future, this would be the place to make a filtered or subset of this list.
            return Data.RomByteSource.Bytes;
        }

        #region Filters

        private bool filterShowOpcodesOnly;
        public bool FilterShowOpcodesOnly
        {
            get => filterShowOpcodesOnly;
            set
            {
                filterShowOpcodesOnly = value;
                UpdateFilters();
            }
        }

        private void UpdateFilters()
        {
            /*if (ViewGrid?.DataSource == null)
                return;
            
            ViewGrid.DataSource.RemoveFilter();
            
            if (FilterShowOpcodesOnly)
                ViewGrid.DataSource.Filter = new PredicateItemFilter<RomByteDataGridRow>(IsRomByteOpcode);*/
        }
        
        private static bool IsRomByteOpcode(RomByteDataGridRow romByteRow)
        {
            return romByteRow.ByteEntry.TypeFlag == FlagType.Opcode;
        }
        
        #endregion
    }
    
    // -----------------------------
    
    public abstract class ByteViewerDataBindingGridController<TRow, TItem> : 
        DataBindingController, 
        IBytesGridViewerDataController<TRow, TItem>,
        INotifyPropertyChangedExt
    
        // TODO: eventually, we should try and get rid of "ByteOffsetData" here to make this more generic.
        where TItem : ByteEntry
        
        where TRow : class, IGridRow<TItem>
    {
        private DataSubsetWithSelection<TRow, TItem> dataSubset;

        // stores just the current Rom bytes in view (subset of larger data source)
        public DataSubsetWithSelection<TRow, TItem> DataSubset
        {
            get => dataSubset;
            private set => this.SetField(ref dataSubset, value);
        }

        public IBytesGridViewer<TItem> ViewGrid
        {
            get => View as IBytesGridViewer<TItem>;
            set
            {
                // this is getting a bit messy, rethink it.
                
                if (ViewGrid != null)
                    ViewGrid.SelectedOffsetChanged -= ViewGridOnSelectedOffsetChanged;

                this.SetField(ref view, value);
                View = view;
                
                if (ViewGrid != null)
                    ViewGrid.SelectedOffsetChanged += ViewGridOnSelectedOffsetChanged;
            }
        }

        private void ViewGridOnSelectedOffsetChanged(object sender, IBytesGridViewer<TItem>.SelectedOffsetChangedEventArgs e)
        {
            if (e.RowIndex == -1)
                return;
            
            DataSubset?.SelectRow(e.RowIndex);
        }

        protected override void DataBind()
        {
            if (ViewGrid == null || Data == null)
                return;
            
            if (DataSubset != null)
                DataSubset.PropertyChanged -= RowsOnPropertyChanged;

            var dataBindSource = GetDataSourceForBind();

            DataSubset = new()
            {
                Items = dataBindSource,
                RowLoader = new DataSubsetLookaheadCacheRomByteDataGridLoader<TRow, TItem>
                {
                    View = ViewGrid,
                    Data = Data,
                }
            };

            DataSubset.PropertyChanged += RowsOnPropertyChanged;
            
            DataSubset.Items = dataBindSource;
            
            DataSubset.StartingRowLargeIndex = 0;
            MatchCachedRowsToView();
            
            DataSubset.SelectedLargeIndex = 0;
            
            ViewGrid.DataSource = dataBindSource;
        }

        public void MatchCachedRowsToView()
        {
            DataSubset.RowCount = ViewGrid.TargetNumberOfRowsToShow;
        }

        private void RowsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(sender, e);
            
            if (e.PropertyName == nameof(DataSubsetWithSelection<TRow, TItem>.SelectedLargeIndex))
            {
                OnSelectedRowChanged();
            }
        }

        private void OnSelectedRowChanged()
        {
            // NOP, currently. views are handling this themselves.
        }

        private List<TItem> GetDataSourceForBind()
        {
            if (ViewGrid == null || Data == null)
                return null;

            return GetByteItems().ToList();
        }

        // return which RomBytes we are interested in.
        // NOTE: this can be a subset of all RomBytes
        // (i.e. just the SPC700 section of a ROM,
        // or just the bytes marked as Instructions, etc)
        //
        // GetByteItems() is a SUBSET of the entire available Rom. 
        // DataSubset will show an additional smaller subset of GetByteItems()
        //
        // example:
        
        // - all possible data: Rom: 4MB of bytes, read from disk.
        // - first subset of the above list: GetByteItems()
        //   i.e. can return stuff like a filtered and sorted list of any bytes in the Rom
        //   like, just the bytes marked as graphics or something.
        // - subset of GetByteItems() i.e. DataSubset in a table displaying part of GetByteItems()
        //   i.e. this is what is actually showing up on the screen
        //
        // It's a little indirect, but it's extremely flexible.
        protected abstract IEnumerable<TItem> GetByteItems();
        
        public event PropertyChangedEventHandler PropertyChanged;
 
        [NotifyPropertyChangedInvocator]
        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    
    public abstract class DataBindingController : DataController
    {
        protected IViewer view;

        public override IViewer View
        {
            get => view;
            set
            {
                view = value;
                DataBind();
            }
        }
        
        private Data data;
        public override Data Data
        {
            get => data;
            set 
            {
                data = value;
                DataBind();
            }
        }
        
        protected abstract void DataBind();
    }
    
    public abstract class DataController : IDataController
    {
        private IViewer view;
        
        public virtual Data Data { get; set; }
        public event EventHandler Closed;

        public virtual IViewer View
        {
            get => view;
            set
            {
                if (view is IFormViewer formViewerBefore)
                    formViewerBefore.Closed -= OnClosed;
                
                view = value;
                
                if (view is IFormViewer formViewerAfter)
                    formViewerAfter.Closed += OnClosed;
            }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            Closed?.Invoke(this, e);
        }
    }
}
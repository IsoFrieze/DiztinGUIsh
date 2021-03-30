using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Diz.Core.model;
using DiztinGUIsh.util;
using Equin.ApplicationFramework;

// things to think about when the dust settles:
// 1) get rid of BindingListView? [which just has the nice filters and that's about it] and stick with BindingSource?
// 2) TODO: need to catch notifychanged from labels and comments or else updates won't propagate 

namespace DiztinGUIsh.window2
{
    // TODO: after refactoring, this class hierarchy is shaking out to be a little weird.
    // when the dust settles, think about restructuring and simplifying all this
    
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

    public class RomByteDataBindingController : ByteViewerDataBindingGridController<RomByteData>
    {
        protected override IEnumerable<RomByteData> GetByteItems()
        {
            // probably delete this now.
            return Data.RomBytes;  //.Select(romByte => new RomByteDataGridRow(romByte, Data, ViewGrid));
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
            return romByteRow.RomByte.TypeFlag == FlagType.Opcode;
        }
        
        #endregion
    }
    
    // -----------------------------
    
    public abstract class ByteViewerDataBindingGridController<TByteItem> : 
        DataBindingController, 
        IBytesGridViewerDataController<TByteItem>
    
        // hack for now.
        // TODO: remove this constraint by refactoring DataSubSet to be generic
        where TByteItem : RomByteData, new()
    {
        // stores just the current Rom bytes in view (subset of larger data source)
        public DataSubsetWithSelection Rows { get; set; }
        
        public IBytesGridViewer<TByteItem> ViewGrid
        {
            get => View as IBytesGridViewer<TByteItem>;
            set
            {
                if (ViewGrid != null)
                    ViewGrid.SelectedOffsetChanged -= ViewGridOnSelectedOffsetChanged;
                
                View = value;
                
                if (ViewGrid != null)
                    ViewGrid.SelectedOffsetChanged += ViewGridOnSelectedOffsetChanged;
            }
        }

        private void ViewGridOnSelectedOffsetChanged(object sender, 
            IBytesGridViewer<TByteItem>.SelectedOffsetChangedEventArgs e)
        {
            Rows?.SelectRow(e.RowIndex);
        }

        protected override void DataBind()
        {
            if (ViewGrid == null || Data == null)
                return;
            
            if (Rows != null)
                Rows.PropertyChanged -= RowsOnPropertyChanged;

            var dataBindSource = GetDataSourceForBind(); 
            
            Rows = DataSubsetWithSelection.Create(Data, dataBindSource as List<RomByteData>, ViewGrid as IBytesGridViewer<RomByteData>);

            Rows.PropertyChanged += RowsOnPropertyChanged;
            
            Rows.Data = Data;
            Rows.RomBytes = dataBindSource as List<RomByteData>;
            Rows.StartingRowLargeIndex = 0;
            Rows.RowCount = ViewGrid.TargetNumberOfRowsToShow;
            Rows.SelectedLargeIndex = 0;
            
            ViewGrid.DataSource = dataBindSource;
        }

        private void RowsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataSubsetWithSelection.SelectedLargeIndex))
            {
                OnSelectedRowChanged();
            }
        }

        private void OnSelectedRowChanged()
        {
            // NOP, currently. views are handling this themselves.
        }

        private List<TByteItem> GetDataSourceForBind()
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
        // Rows will show an additional smaller subset of GetByteItems()
        //
        // example:
        
        // - all possible data: Rom: 4MB of bytes, read from disk.
        // - first subset of the above list: GetByteItems()
        //   i.e. can return stuff like a filtered and sorted list of any bytes in the Rom
        //   like, just the bytes marked as graphics or something.
        // - subset of GetByteItems() i.e. Rows in a table displaying part of GetByteItems()
        //   i.e. this is what is actually showing up on the screen
        //
        // It's a little indirect, but it's extremely flexible.
        protected abstract IEnumerable<TByteItem> GetByteItems();
    }
    
    
    public abstract class DataBindingController : DataController
    {
        private IViewer view;

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
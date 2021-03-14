using System;
using System.Collections.Generic;
using System.Linq;
using Diz.Core.model;
using Equin.ApplicationFramework;

// things to think about when the dust settles:
// 1) get rid of BindingListView? [which just has the nice filters and that's about it] and stick with BindingSource?
// 2) TODO: need to catch notifychanged from labels and comments or else updates won't propagate 

namespace DiztinGUIsh.window2
{
    public class RomByteDataBindingGridController : RomByteDataBindingController
    {

    }

    public class RomByteDataBindingGridFormController : RomByteDataBindingController
    {
        public Project Project { get; init; }
    }
    
    // -----------------------------

    public class RomByteDataBindingController : ByteViewerDataBindingGridController<RomByteDataGridRow>
    {
        protected override IEnumerable<RomByteDataGridRow> GetByteItems()
        {
            return Data.RomBytes.Select(romByte =>
                new RomByteDataGridRow(romByte, Data, ViewGrid));
        }
    }
    
    // -----------------------------
    
    public abstract class ByteViewerDataBindingGridController<TByteItem> : DataBindingController, IBytesGridViewerDataController<TByteItem>
    {
        public IBytesGridViewer<TByteItem> ViewGrid
        {
            get => View as IBytesGridViewer<TByteItem>;
            set => View = value;
        }

        protected override void DataBind()
        {
            if (ViewGrid == null || Data == null)
                return;
            
            ViewGrid.DataSource = new BindingListView<TByteItem>(GetDataSourceForBind());
        }
        
        private List<TByteItem> GetDataSourceForBind()
        {
            if (ViewGrid == null || Data == null)
                return null;

            return GetByteItems().ToList(); 
        }

        protected abstract IEnumerable<TByteItem> GetByteItems();

        // private void UpdateFilters()
        // {
        //     bindingList.Filter = new PredicateItemFilter<RomByteData>(IsRomByteOpcode);
        // }
        //
        // private static bool IsRomByteOpcode(RomByteData romByte)
        // {
        //     return romByte.TypeFlag == FlagType.Opcode;
        // }
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

        private void OnClosed(object? sender, EventArgs e)
        {
            Closed?.Invoke(sender, e);
        }
    }
}
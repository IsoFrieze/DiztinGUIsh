using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Diz.Core.model;
using Equin.ApplicationFramework;

// things to think about when the dust settles:
// 1) get rid of BindingListView? [which just has the nice filters and that's about it] and stick with BindingSource?
// 2) TODO: need to catch notifychanged from labels and comments or else updates won't propagate 

namespace DiztinGUIsh.window2
{
    public class RomByteGridController : RomByteGridFormController
    {
        // temp. hack. making RomByteGridController be an alias of RomByteGridFormController til
        // we think of a better way to do this. it's fine for the moment
    }

    public class RomByteGridFormController : ByteViewerGridController<RomByteDataGridRow>
    {
        protected override IEnumerable<RomByteDataGridRow> GetByteItems()
        {
            return Data.RomBytes.Select(romByte =>
                new RomByteDataGridRow(romByte, Data, ViewGrid));
        }
    }
    
    public abstract class ByteViewerGridController<TByteItem> : BaseController, IBytesGridViewerController<TByteItem>
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

        /*private void UpdateFilters()
{
    bindingList.Filter = new PredicateItemFilter<RomByteData>(IsRomByteOpcode);
}

private static bool IsRomByteOpcode(RomByteData romByte)
{
    return romByte.TypeFlag == FlagType.Opcode;
}*/
    }
    
    public abstract class BaseController : IController
    {
        private IViewer view;

        public IViewer View
        {
            get => view;
            set
            {
                view = value;
                DataBind();
            }
        }
        
        private Data data;
        public Data Data
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
}
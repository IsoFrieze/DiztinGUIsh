using Diz.Core.model;
using Diz.Core.util;
using Equin.ApplicationFramework;

namespace DiztinGUIsh.window2
{
    public interface IBytesGridViewer : IBytesViewer
    {
        // get the number base that will be used to display certain items in the grid
        public Util.NumberBase DataGridNumberBase { get; }
        RomByteDataGridRow SelectedRomByteRow { get; }
    }

    public interface IBytesViewer
    {
        
    }
    
    public interface IBytesViewerController
    {
        // public BindingListView<RomByteData> BindingList { get; }
        Data Data { get; }

        // public void CreateDataBindingTo(Data data);
    }
}
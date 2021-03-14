using Diz.Core.model;
using Diz.Core.util;
using Equin.ApplicationFramework;

namespace DiztinGUIsh.window2
{
    public interface IViewer
    {
        public IController Controller { get; set; }
    }
    
    public interface IBytesGridViewer<TByteItem> : IViewer
    {
        // get the number base that will be used to display certain items in the grid
        public Util.NumberBase DataGridNumberBase { get; }
        TByteItem SelectedRomByteRow { get; }
        public BindingListView<TByteItem> DataSource { get; set; }
    }

    public interface IBytesFormViewer : IViewer
    {

    }

    public interface IController
    {
        IViewer View { get; }
        Data Data { get; }
    }
    
    public interface IBytesGridViewerController<TByteItem> : IController
    {
        IBytesGridViewer<TByteItem> ViewGrid { get; set; }
    }
}
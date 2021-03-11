using Diz.Core.model;
using Equin.ApplicationFramework;

namespace DiztinGUIsh.window2
{
    public interface IBytesViewer
    {
        
    }
    
    public interface IBytesViewerController
    {
        public BindingListView<RomByteData> BindingList { get; }

        public void CreateDataBindingTo(Data data);
    }
}
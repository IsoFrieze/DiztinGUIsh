using Diz.Gui.Avalonia.ViewModels;

namespace Diz.Gui.Avalonia
{
    public class MainWindowViewModel : ViewModel
    {
        public ByteEntriesViewModel ByteEntriesViewModel { get; } = new ByteEntriesViewModel();

        public MainWindowViewModel()
        {
            
        }
    }
}
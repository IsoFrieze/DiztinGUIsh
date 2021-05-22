namespace Diz.Gui.Avalonia.ViewModels
{
    public class MainWindowViewModel : ViewModel
    {
        public ByteEntriesViewModel ByteEntriesViewModel { get; } = new ByteEntriesViewModel();

        public MainWindowViewModel()
        {
            
        }
    }
}
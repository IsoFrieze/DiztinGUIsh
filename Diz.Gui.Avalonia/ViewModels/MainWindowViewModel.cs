using Diz.Gui.Avalonia.Views.Windows;

namespace Diz.Gui.Avalonia.ViewModels
{
    public class MainWindowViewModel : ViewModel
    {
        public ByteEntriesViewModel ByteEntriesViewModel { get; } = new ByteEntriesViewModel();

        public void OpenNewWindow()
        {
            var newWindow = new MainWindow();
            newWindow.Show();
        }
        
        public MainWindowViewModel()
        {
            
        }
    }
}
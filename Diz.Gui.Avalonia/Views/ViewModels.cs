using Diz.Gui.Avalonia.ViewModels;
using Diz.Gui.Avalonia.Views.Windows;

namespace Diz.Gui.Avalonia.Views
{
    public class MainWindowViewModel : MainWindowViewModelBase
    {
        public override MainWindow CreateWindow() => new MainWindow();
    }
}
using Diz.Gui.Avalonia.Views.Windows;
using Diz.ViewModels;

namespace Diz.Gui.Avalonia.Views
{
    public class MainWindowViewModel : MainWindowViewModelBase
    {
        public override IMainWindowView CreateWindow() => new MainWindow();
    }
}
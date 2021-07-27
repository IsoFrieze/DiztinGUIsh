using Diz.Gui.Avalonia.Views.Windows;
using Diz.Gui.ViewModels;
using Diz.Gui.ViewModels.ViewModels;

namespace Diz.Gui.Avalonia.ViewModels
{
    public abstract class MainWindowViewModelBase : ViewModel
    {
        public LabelsViewModel LabelsViewModel { get; } = new LabelsViewModel();

        public abstract MainWindow CreateWindow();

        public void OpenNewWindow()
        {
            var newWindow = CreateWindow();
            newWindow.Show();
        }
    }
}
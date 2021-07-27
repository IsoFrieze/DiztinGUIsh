using Diz.Gui.Avalonia.Views.Windows;
using Diz.Gui.AvaloniaUserControls;

namespace Diz.ViewModels
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
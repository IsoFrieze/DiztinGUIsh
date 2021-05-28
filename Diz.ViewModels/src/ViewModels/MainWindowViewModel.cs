namespace Diz.ViewModels
{
    public abstract class MainWindowViewModelBase : ViewModel
    {
        public ByteEntriesViewModel ByteEntriesViewModel { get; } = new();

        public abstract IMainWindowView CreateWindow();

        public void OpenNewWindow()
        {
            var newWindow = CreateWindow();
            newWindow.Show();
        }
    }
}
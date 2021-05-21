using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Diz.Gui.Avalonia.ViewModels;
using ReactiveUI;

namespace Diz.Gui.Avalonia.Views.UserControls
{
    public class MainGridUserControl : ReactiveUserControl<ByteEntriesViewModel>
    {
        private DataGrid Grid => this.FindControl<DataGrid>("MainGrid");
        
        public MainGridUserControl()
        {
            ViewModel = new ByteEntriesViewModel();
            
            this.WhenActivated(disposables =>
            {
                
            });
            
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
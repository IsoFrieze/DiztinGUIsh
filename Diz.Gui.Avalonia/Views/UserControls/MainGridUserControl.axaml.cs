using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Diz.Gui.Avalonia.Models;
using Diz.Gui.Avalonia.ViewModels;
using ReactiveUI;

namespace Diz.Gui.Avalonia.Views.UserControls
{
    public class MainGridUserControl : UserControl // ReactiveUserControl<MainGridViewModel>
    {
        public MainGridUserControl()
        {
            // this.WhenActivated(disposables =>
            // {
            //     
            // });
            
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
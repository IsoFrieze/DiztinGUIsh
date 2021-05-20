using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Diz.Gui.Avalonia.Models;
using Diz.Gui.Avalonia.ViewModels;

namespace Diz.Gui.Avalonia.Views.UserControls
{
    public class MainGrid : ReactiveUserControl<MainGridViewModel>
    {
        public MainGrid()
        {
            this
                .WhenActivated(
            
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            
            AvaloniaXamlLoader.Load(this);
        }
    }
}
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Diz.Gui.Avalonia.Views.Windows;
using ReactiveUI;

namespace Diz.Gui.Avalonia.Views.UserControls
{
    public class MainMenu : UserControl
    {
        public MainMenu()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
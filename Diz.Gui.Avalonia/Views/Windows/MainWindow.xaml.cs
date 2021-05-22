using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Diz.Gui.Avalonia.ViewModels;
using Diz.Gui.Avalonia.Views.UserControls;
using ReactiveUI;

namespace Diz.Gui.Avalonia.Views.Windows
{
    public class MainWindow : ReactiveWindow<ByteEntriesViewModel>
    {
        public MainWindow()
        {
            #if DEBUG
            this.AttachDevTools();
            #endif
            
            ViewModel = new ByteEntriesViewModel();

            this
                .WhenActivated(
                    disposableRegistration =>
                    {
                        this.OneWayBind(ViewModel, 
                                viewModel => viewModel.ByteEntries, 
                                view => view.MainGridUserControl.MainGrid.Items)
                            .DisposeWith(disposableRegistration);
                    });

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public MainGridUserControl MainGridUserControl => this.FindControl<MainGridUserControl>("MainGridUserControl");
    }
}
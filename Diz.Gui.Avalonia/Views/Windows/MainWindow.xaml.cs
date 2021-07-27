using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Diz.Gui.Avalonia.UserControls.UserControls;
using ReactiveUI;

namespace Diz.Gui.Avalonia.Views.Windows
{
    public class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        public MainWindow()
        {
            #if DEBUG
            this.AttachDevTools();
            #endif
            
            ViewModel = new MainWindowViewModel();

            this
                .WhenActivated(
                    disposableRegistration =>
                     {
                         this.Bind(ViewModel, 
                                 vm => vm.LabelsViewModel,
                                 view => view.LabelsListUserControl.ViewModel
                                 )
                             .DisposeWith(disposableRegistration);
                     });

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public LabelsListUserControl LabelsListUserControl => this.FindControl<LabelsListUserControl>("LabelsListUserControl");
    }
}
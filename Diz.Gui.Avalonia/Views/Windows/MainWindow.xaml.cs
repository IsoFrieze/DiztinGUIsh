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
    // TODO: this screen doesn't show ByteEntriesViewModel necessarily
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
                   
                        // // Jut log the View's activation
                        // Console.WriteLine(
                        //     $"[v  {Thread.CurrentThread.ManagedThreadId}]: " +
                        //     "View activated\n");
                        //
                        // // Just log the View's deactivation
                        // Disposable
                        //     .Create(
                        //         () =>
                        //             Console.WriteLine(
                        //                 $"[v  {Thread.CurrentThread.ManagedThreadId}]: " +
                        //                 "View deactivated"))
                        //     .DisposeWith(disposables);


                        // Observable
                        //     .FromEventPattern(WndMain, nameof(WndMain.Closing))
                        //     .Subscribe(
                        //         _ =>
                        //         {
                        //             Console.WriteLine(
                        //                 $"[v  {Thread.CurrentThread.ManagedThreadId}]: " +
                        //                 "Main window closing...");
                        //         })
                        //     .DisposeWith(disposables);

                        /*this
                            .OneWayBind(ViewModel, 
                                vm => vm.ByteEntries,
                                v => v.)
                            .DisposeWith(disposables);*/
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

// http://avaloniaui.net/docs/reactiveui/activation#activation-example
// https://reactiveui.net/docs/handbook/data-binding/avalonia
// https://reactiveui.net/docs/handbook/events/#how-do-i-convert-my-own-c-events-into-observables
//https://reactiveui.net/docs/handbook/when-activated/#views
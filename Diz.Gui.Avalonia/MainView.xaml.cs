using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Diz.Gui.Avalonia {

    // http://avaloniaui.net/docs/reactiveui/activation#activation-example
    // https://reactiveui.net/docs/handbook/data-binding/avalonia
    public class MainView : ReactiveWindow<MainViewModel> {
        public MainView() {
            ViewModel = new MainViewModel();

            //https://reactiveui.net/docs/handbook/when-activated/#views
            this
                .WhenActivated(
                    disposables => {
                        // Jut log the View's activation
                        Console.WriteLine(
                            $"[v  {Thread.CurrentThread.ManagedThreadId}]: " +
                            "View activated\n");

                        // Just log the View's deactivation
                        Disposable
                            .Create(
                                () =>
                                    Console.WriteLine(
                                        $"[v  {Thread.CurrentThread.ManagedThreadId}]: " +
                                        "View deactivated"))
                            .DisposeWith(disposables);

                        // https://reactiveui.net/docs/handbook/events/#how-do-i-convert-my-own-c-events-into-observables
                        Observable
                            .FromEventPattern(WndMain, nameof(WndMain.Closing))
                            .Subscribe(
                                _ => {
                                    Console.WriteLine(
                                        $"[v  {Thread.CurrentThread.ManagedThreadId}]: " +
                                        "Main window closing...");
                                })
                            .DisposeWith(disposables);

                        // https://reactiveui.net/docs/handbook/data-binding/
                        this
                            .OneWayBind(ViewModel, vm => vm.Greeting, v => v.TbGreetingLabel.Text)
                            .DisposeWith(disposables);
                    });

            InitializeComponent();
        }

        private void InitializeComponent() {
            // https://reactiveui.net/docs/handbook/data-binding/avalonia
            AvaloniaXamlLoader.Load(this);
        }

        // https://reactiveui.net/docs/handbook/data-binding/avalonia
        private TextBlock TbGreetingLabel => this.FindControl<TextBlock>("TbGreetingLabel");
        private Window WndMain => this.FindControl<Window>("WndMain");
    }
}

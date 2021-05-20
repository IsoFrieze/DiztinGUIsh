using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Diz.Gui.Avalonia.ViewModels;
using ReactiveUI;

namespace Diz.Gui.Avalonia.Views
{
    public class MainView : ReactiveWindow<MainViewModel>
    {
        public MainView()
        {
            ViewModel = new MainViewModel();

            this
                .WhenActivated(
                    disposables =>
                    {
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

                        // this
                        //     .OneWayBind(ViewModel, vm => vm.Greeting, v => v.TbGreetingLabel.Text)
                        //     .DisposeWith(disposables);
                    });

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        // private TextBlock TbGreetingLabel => this.FindControl<TextBlock>("TbGreetingLabel");
        // private Window WndMain => this.FindControl<Window>("WndMain");
    }
}

// http://avaloniaui.net/docs/reactiveui/activation#activation-example
// https://reactiveui.net/docs/handbook/data-binding/avalonia
// https://reactiveui.net/docs/handbook/events/#how-do-i-convert-my-own-c-events-into-observables
//https://reactiveui.net/docs/handbook/when-activated/#views
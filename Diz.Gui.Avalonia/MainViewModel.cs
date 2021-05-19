using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using ReactiveUI;

namespace Diz.Gui.Avalonia {

    // https://reactiveui.net/docs/handbook/when-activated/
    // https://reactiveui.net/docs/handbook/data-binding/avalonia
    // http://avaloniaui.net/docs/reactiveui/activation#activation-example
    public class MainViewModel : ReactiveObject, IActivatableViewModel {
        public ViewModelActivator Activator { get; }

        // https://reactiveui.net/docs/handbook/view-models/#read-write-properties
        // https://reactiveui.net/docs/handbook/view-models/boilerplate-code
        private string greeting;
        public string Greeting {
            get => greeting;
            private set => this.RaiseAndSetIfChanged(ref greeting, value);
        }

        public MainViewModel() {

            // https://reactiveui.net/docs/handbook/when-activated/
            Activator = new ViewModelActivator();

            this.WhenActivated(
                disposables => {

                    // Just log the ViewModel's activation
                    // https://github.com/kentcb/YouIandReactiveUI/blob/master/ViewModels/Samples/Chapter%2018/Sample%2004/ChildViewModel.cs
                    Console.WriteLine(
                        $"[vm {Thread.CurrentThread.ManagedThreadId}]: " +
                        "ViewModel activated");

                    // Asynchronously generate a new greeting message every second
                    // https://reactiveui.net/docs/guidelines/framework/ui-thread-and-schedulers
                    Observable
                        .Timer(
                            TimeSpan.FromMilliseconds(100), // give the view time to activate
                            TimeSpan.FromMilliseconds(1000),
                            RxApp.MainThreadScheduler)
                        .Take(Traits.Length)
                        .Do(
                            t => {
                                var newGreeting = $"Hello, {Traits[t % Traits.Length]} world !";
                                Console.WriteLine(
                                    $"[vm {Thread.CurrentThread.ManagedThreadId}]: " +
                                    $"Timer Observable -> " +
                                    $"Setting greeting to: \"{newGreeting}\"");
                                Greeting = newGreeting;
                            },
                            () => 
                                Console.WriteLine(
                                    "Those are all the greetings, folks! " +
                                    "Feel free to close the window now...\n"))
                        .Subscribe()
                        .DisposeWith(disposables);

                    // Just log the ViewModel's deactivation
                    // https://github.com/kentcb/YouIandReactiveUI/blob/master/ViewModels/Samples/Chapter%2018/Sample%2004/ChildViewModel.cs
                    Disposable
                        .Create(
                            () =>
                                Console.WriteLine(
                                    $"[vm {Thread.CurrentThread.ManagedThreadId}]: " +
                                    "ViewModel deactivated"))
                        .DisposeWith(disposables);
                });

            // Log any change of our greeting
            // https://reactiveui.net/docs/handbook/when-any/#basic-syntax
            // https://reactiveui.net/docs/guidelines/framework/dispose-your-subscriptions
            this
                .WhenAnyValue(vm => vm.Greeting)
                .Skip(1) // ignore the initial NullOrEmpty value of Greeting
                .Do(
                    greet =>
                        Console.WriteLine(
                            $"[vm {Thread.CurrentThread.ManagedThreadId}]: " +
                            "WhenAnyValue()   -> " +
                            $"Greeting value changed to: \"{greet}\"\n"))
                .Subscribe();
        }
                                    
        private static readonly string[] Traits = {
            "expressive",
            "clear",
            "responsive",
            "concurrent",
            "reactive"
        };
    }
}

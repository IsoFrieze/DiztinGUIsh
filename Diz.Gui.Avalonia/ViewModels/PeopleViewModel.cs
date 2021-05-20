using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using Diz.Gui.Avalonia.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Diz.Gui.Avalonia.ViewModels
{
    public class PeopleViewModel : ReactiveObject, IActivatableViewModel
    {
        public ViewModelActivator Activator { get; }

        [Reactive] public ObservableCollection<Person> People { get; set; }

        public PeopleViewModel()
        {
            Activator = new ViewModelActivator();
            
            People = new ObservableCollection<Person>(GenerateMockPeopleTable());
            
            this.WhenActivated(
                 (CompositeDisposable disposables) =>
                 {
                     //         Observable
            //             .Timer(
            //                 TimeSpan.FromMilliseconds(100), // give the view time to activate
            //                 TimeSpan.FromMilliseconds(1000),
            //                 RxApp.MainThreadScheduler)
            //             .Take(6)
            //             .Select(x=>(int)x)
            //             .Do(
            //                 t =>
            //                 {
            //                     People[t];
            //                 })
            //             .Subscribe()
            //             .DisposeWith(disposables);
            });
        }
        
        private IEnumerable<Person> GenerateMockPeopleTable()
        {
            var defaultPeople = new List<Person>()
            {
                new()
                {
                    FirstName = "Pat", 
                    LastName = "Patterson", 
                    EmployeeNumber = 1010,
                    DepartmentNumber = 100, 
                    DeskLocation = "B3F3R5T7"
                },
                new()
                {
                    FirstName = "Jean", 
                    LastName = "Jones", 
                    EmployeeNumber = 973,
                    DepartmentNumber = 200, 
                    DeskLocation = "B1F1R2T3"
                },
                new()
                {
                    FirstName = "Terry", 
                    LastName = "Tompson", 
                    EmployeeNumber = 300,
                    DepartmentNumber = 100, 
                    DeskLocation = "B3F2R10T1"
                }
            };

            return defaultPeople;
        }
    }
}

// reference
// https://reactiveui.net/docs/handbook/when-activated/
// https://reactiveui.net/docs/handbook/data-binding/avalonia
// http://avaloniaui.net/docs/reactiveui/activation#activation-example
// Log any change of our greeting
// https://reactiveui.net/docs/handbook/when-any/#basic-syntax
// https://reactiveui.net/docs/guidelines/framework/dispose-your-subscriptions
// Just log the ViewModel's deactivation
// https://github.com/kentcb/YouIandReactiveUI/blob/master/ViewModels/Samples/Chapter%2018/Sample%2004/ChildViewModel.cs
// Asynchronously generate a new greeting message every second
// https://reactiveui.net/docs/guidelines/framework/ui-thread-and-schedulers
// https://reactiveui.net/docs/handbook/view-models/#read-write-properties
// https://reactiveui.net/docs/handbook/view-models/boilerplate-code
// https://reactiveui.net/docs/handbook/when-activated/

//    get => greeting;
//    private set => this.RaiseAndSetIfChanged(ref greeting, value);
//}


// this
//     .WhenAnyValue(vm => vm.Greeting)
//     .Skip(1) // ignore the initial NullOrEmpty value of Greeting
//     .Do(
//         greet =>
//             Console.WriteLine(
//                 $"[vm {Thread.CurrentThread.ManagedThreadId}]: " +
//                 "WhenAnyValue()   -> " +
//                 $"Greeting value changed to: \"{greet}\"\n"))
//     .Subscribe();

//
//         // Just log the ViewModel's activation
//         // https://github.com/kentcb/YouIandReactiveUI/blob/master/ViewModels/Samples/Chapter%2018/Sample%2004/ChildViewModel.cs
//         Console.WriteLine(
//             $"[vm {Thread.CurrentThread.ManagedThreadId}]: " +
//             "ViewModel activated");
//         
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Diz.Core.model;
using DynamicData;
using ReactiveUI;

namespace Diz.Gui.ViewModels.ViewModels
{
    public class LabelsViewModel : ViewModel
    {
        private ObservableAsPropertyHelper<IEnumerable<LabelViewModel>> searchResults;
        public IEnumerable<LabelViewModel> SearchResults => searchResults?.Value;

        private LabelViewModel selectedItem;
        private string searchText;
        private IObservable<IChangeSet<LabelProxy, int>> sourceLabels;

        public LabelViewModel SelectedItem
        {
            get => selectedItem;
            set => this.RaiseAndSetIfChanged(ref selectedItem, value);
        }

        public string SearchText
        {
            get => searchText;
            set => this.RaiseAndSetIfChanged(ref searchText, value);
        }

        public IObservable<IChangeSet<LabelProxy, int>> SourceLabels
        {
            get => sourceLabels;
            set => this.RaiseAndSetIfChanged(ref sourceLabels, value);
        }

        public LabelsViewModel()
        {
            // to use sample data instead....
            // var data = SampleDataService.SourceData.Value;

            this.WhenActivated(
                (CompositeDisposable disposables) =>
                {
                    // ReactiveCommand.Create<DataGridCellEditEndedEventArgs>(CellEdited);
                    
                    var command = ReactiveCommand.Create<string>(
                        s =>
                        {
                            Console.WriteLine($"CMD: {s}");
                            
                        });
                    
                    // In the ViewModel.
                    this.WhenAnyValue(x => x.SearchText)
                        // .Where(x => !string.IsNullOrWhiteSpace(x))
                        // .Throttle(TimeSpan.FromSeconds(.1))
                        .InvokeCommand(command)
                        .SearchForLabels(SourceLabels)
                    
                    // ReactiveCommand<string,IObservable<IChangeSet<LabelProxy, int>>> cmd = ReactiveCommand.
                    // IObservable<IChangeSet<LabelProxy, int>> sourceLabels

                    // SearchCommand = ReactiveCommand.CreateFromTask(CreateUser, canCreateUser);

                    // searchResults = this
                    //     .WhenAnyValue(x => x.SourceLabels, x => x.SearchText)
                    //     .Subscribe(x =>
                    //     {
                    //
                    //     });
                    // .Select(x=>x.Item2)
                    // .Throttle(TimeSpan.FromMilliseconds(50))
                    // .Select(s =>
                    // {
                    //     var (labels, offsetFilterTxt) = s;
                    //     return (labels, TrimSearchTerm(offsetFilterTxt));
                    // })
                    // .DistinctUntilChanged()
                    // .SearchForLabels(SourceLabels)
                    // .ObserveOn(RxApp.MainThreadScheduler)
                    // .AsObservable()
                    // .ToProperty(this, x => x.SearchResults);
                });
        }

        public ICommand SearchCommand { get; set; }

        private static string TrimSearchTerm(string searchTerm)
        {
            return searchTerm?.Trim() ?? "";
        }
    }
}
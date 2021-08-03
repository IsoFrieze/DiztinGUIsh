using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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
        private string offsetFilter;
        private IObservable<IChangeSet<LabelProxy, int>> sourceLabels;

        public LabelViewModel SelectedItem
        {
            get => selectedItem;
            set => this.RaiseAndSetIfChanged(ref selectedItem, value);
        }

        public string OffsetFilter
        {
            get => offsetFilter;
            set => this.RaiseAndSetIfChanged(ref offsetFilter, value);
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
                    
                    searchResults = this
                        .WhenAnyValue(x=>x.SourceLabels, x => x.OffsetFilter)
                        .Select(x=>x.Item2)
                        .Throttle(TimeSpan.FromMilliseconds(50))
                        .Select(TrimSearchTerm)
                        .DistinctUntilChanged()
                        .SearchForLabels(SourceLabels)
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .AsObservable()
                        .ToProperty(this, x => x.SearchResults);
                });
        }

        private static string TrimSearchTerm(string searchTerm)
        {
            return searchTerm?.Trim() ?? "";
        }
    }
}
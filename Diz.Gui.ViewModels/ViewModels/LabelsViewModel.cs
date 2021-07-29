using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;

namespace Diz.Gui.ViewModels.ViewModels
{
    public class LabelsViewModel : ViewModel
    {
        private readonly ObservableAsPropertyHelper<IEnumerable<LabelViewModel>> searchResults;
        public IEnumerable<LabelViewModel> SearchResults => searchResults.Value;

        private LabelViewModel selectedItem;
        private string offsetFilter;

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

        public LabelsViewModel()
        {
            searchResults = this
                .WhenAnyValue(x => x.OffsetFilter)
                .Throttle(TimeSpan.FromMilliseconds(50))
                .Select(searchTerm => searchTerm?.Trim() ?? "")
                .DistinctUntilChanged()
                .SearchForLabels()
                .ObserveOn(RxApp.MainThreadScheduler)
                .AsObservable()
                .ToProperty(this, x => x.SearchResults);

            this.WhenActivated(
                (CompositeDisposable disposables) =>
                {
                    // ReactiveCommand.Create<DataGridCellEditEndedEventArgs>(CellEdited);
                });
        }
    }
}
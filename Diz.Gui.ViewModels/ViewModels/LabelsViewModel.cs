using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Diz.Core.util;
using Diz.Gui.ViewModels.ViewModels;
using DynamicData;
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

public static class LabelSearchEx
{
    public static IObservable<IEnumerable<LabelViewModel>> SearchForLabels(this IObservable<string> searchQuery)
    {
        return searchQuery
            .Select(Util.StripHex)
            .SelectMany(SearchForLabelsAsync);
    }

    public static async Task<IEnumerable<LabelViewModel>> SearchForLabelsAsync(
        string searchTerm,
        CancellationToken token)
    {
        return await Task.Run(() =>
        {
            return FakeLabelService
                .Connect()
                .Filter(labelViewModel =>
                    LabelViewMatches(labelViewModel, searchTerm)
                ).AsObservableCache().Items;
        }, token);
    }

    private static bool LabelViewMatches(LabelViewModel labelVm, string addressSubsetToMatch) =>
        string.IsNullOrEmpty(addressSubsetToMatch) ||
        Util.StripHex(labelVm.SnesAddress).Contains(addressSubsetToMatch);
}
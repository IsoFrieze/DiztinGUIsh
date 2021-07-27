using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Diz.Core.model;
using DynamicData;
using ReactiveUI;

namespace Diz.Gui.ViewModels.ViewModels
{
    public class LabelViewModel : ViewModel
    {
        private Label label;

        public Label Label
        {
            get => label;
            set => this.RaiseAndSetIfChanged(ref label, value);
        }

        public LabelViewModel()
        {
            // this.WhenActivated(disposableRegistration =>
            // {
            //     label.WhenAnyValue(x => x.Comment)
            //         .Subscribe(x =>
            //             Console.WriteLine($"changed! {x}")
            //         );
            // });
        }
    }

    public class LabelsViewModel : ViewModel
    {
        private readonly ObservableAsPropertyHelper<IEnumerable<LabelViewModel>> searchResults;
        public IEnumerable<LabelViewModel> SearchResults => searchResults.Value;

        private LabelViewModel selectedItem;
        private string offsetFilter;

        private ReadOnlyObservableCollection<LabelViewModel> _allLabels;

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
                .SelectMany(SearchForLabels)
                .ObserveOn(RxApp.MainThreadScheduler)
                .AsObservable()
                .ToProperty(this, x=>x.SearchResults);

            FakeLabelService
                .Connect()
                .Bind(out _allLabels)
                .Subscribe(x => RefreshSearch());

            this.WhenActivated(
                (CompositeDisposable disposables) =>
                {
                    // ReactiveCommand.Create<DataGridCellEditEndedEventArgs>(CellEdited);
                });
        }

        private void RefreshSearch()
        {
            this.RaisePropertyChanging(nameof(OffsetFilter));
        }

        // private IEnumerable<LabelViewModel> SearchLabels(string searchTerm)
        private static async Task<IEnumerable<LabelViewModel>> SearchForLabels(string searchTerm, CancellationToken token)
        {
            return await Task.Run(() =>
            {
                var results = FakeLabelService
                    .Connect()
                    .Filter(x =>
                        SearchLabelView(x, searchTerm)
                    ).AsObservableCache().Items;

                return results;
            }, token);
        }

        private static bool SearchLabelView(LabelViewModel x, string searchTerm) =>
            string.IsNullOrEmpty(searchTerm) ||
            x.Label.Offset.ToString().Contains(searchTerm);
    }

    public static class FakeLabelService
    {
        private static Lazy<SourceCache<LabelViewModel, int>> SourceLabels
            = new(valueFactory: () =>
            {
                var sourceCache = new SourceCache<LabelViewModel, int>(x => x.Label.Offset);
                sourceCache.AddOrUpdate(CreateSampleLabels());
                return sourceCache;
            });

        public static IObservable<IChangeSet<LabelViewModel, int>> Connect() =>
            SourceLabels.Value.Connect();

        private static IEnumerable<LabelViewModel> CreateSampleLabels()
        {
            // var loader = Service.Container.GetInstance<ISampleProjectLoader>("SampleProjectLoader");
            return new List<Label>
                {
                    new()
                    {
                        Comment = "test2",
                        Name = "name2",
                        Offset = 2,
                    },
                    new()
                    {
                        Comment = "test1",
                        Name = "name1", Offset = 1,
                    },
                    new()
                    {
                        Comment = "test3",
                        Name = "name3", Offset = 3,
                    }
                }
                .Select(x =>
                    new LabelViewModel {Label = x}
                ).ToList();
        }
    }
}
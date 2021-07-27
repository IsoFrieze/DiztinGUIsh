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
using DynamicData.Binding;
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
        // public ObservableCollection<Label> Labels
        // {
        //     get => labels;
        //     set => this.RaiseAndSetIfChanged(ref labels, value);
        // }

        private readonly ObservableAsPropertyHelper<IEnumerable<LabelViewModel>> _labels;
        public IEnumerable<LabelViewModel> Labels => _labels.Value;

        private LabelViewModel selectedItem;

        // private ObservableCollection<Label> labels;
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
            // temp hack
            // Labels = new ObservableCollection<Label>(GetSampleLabels() ?? new List<Label>());

            // Labels.ToObservableChangeSet();

            // var sourceCache = new SourceCache<Label, int>(x => x.Offset);
            // var outObservableCollection = new ObservableCollectionExtended<Label>();
            // sourceCache
            //     .Connect()
            //     .Bind(outObservableCollection);

            // byteEntries = this
            //     .WhenAnyValue(x => x.StartingOffset)
            //     .Throttle(TimeSpan.FromMilliseconds(800))
            //     // .WhenActivated()
            //     .DistinctUntilChanged()
            //     .Where( x=> x != -1)
            //     .SelectMany(GetByteEntries)
            //     .ObserveOn(RxApp.MainThreadScheduler)
            //     .ToProperty(this, x => x.ByteEntries);

            _labels = this.WhenAnyValue(x => x.OffsetFilter)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .Select(term => term?.Trim() ?? "")
                .DistinctUntilChanged()
                .SelectMany(SearchLabels)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x=>x.Labels);

            this.WhenActivated(
                (CompositeDisposable disposables) =>
                {
                    // ReactiveCommand.Create<DataGridCellEditEndedEventArgs>(CellEdited);
                    // this
                    //     .WhenAnyValue(x => x.Labels)
                    //     .Subscribe(x);
                });
        }

        // private IEnumerable<LabelViewModel> SearchLabels(string searchTerm)
        private static async Task<IEnumerable<LabelViewModel>> SearchLabels(string searchTerm, CancellationToken token)
        {
            return await Task.Run(() =>
            {
                var sourceData = FakeModel.SourceLabels.Value;
                return sourceData.Items
                    .Where(x=>
                        SearchLabelView(x, searchTerm)
                    );
            }, token);
        }

        private static bool SearchLabelView(LabelViewModel x, string searchTerm) =>
            string.IsNullOrEmpty(searchTerm) || 
            x.Label.Offset.ToString().Contains(searchTerm);
    }

    public static class FakeModel
    {
        public static Lazy<SourceCache<LabelViewModel, int>> SourceLabels
            = new(valueFactory: () =>
            {
                var sourceCache = new SourceCache<LabelViewModel, int>(x => x.Label.Offset);
                sourceCache.AddOrUpdate(CreateSampleLabels());
                return sourceCache;
            });

        private static IEnumerable<LabelViewModel> CreateSampleLabels()
        {
            // var loader = Service.Container.GetInstance<ISampleProjectLoader>("SampleProjectLoader");

            // this interface access code is a little screwed up with Diz here, it's just a starting point.

            // var project = loader?.GetSampleProject();
            //
            // return project?.Data?.RomByteSource.Bytes?.Skip(0)
            //     .Take(20)
            //     .Select(x => new ByteEntryDetailsViewModel
            //     {
            //         ByteEntry = new RomByteRowBase
            //         {
            //             ByteEntry = x,
            //             Data = project.Data,
            //             ParentView = this,
            //         }
            //     });

            // temp hack for some temp data
            // FakeLabelCache = new SourceCache<Label, int>(x => x.Label.Offset);

            // FakeLabelCache.AddOrUpdate(new Label
            // {
            //     Label = new Label
            //     {
            //         Comment = "test2",
            //         Name = "name2", Offset = 2,
            //     }
            // });

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
                .Select(x=>
                    new LabelViewModel {Label = x}
                ).ToList();
        }
    }
}
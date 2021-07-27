using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using Diz.Core.model;
using ReactiveUI;

namespace Diz.Gui.ViewModels.ViewModels
{
    public class LabelsViewModel : ViewModel
    {
        public ObservableCollection<Label> Labels
        {
            get => labels;
            set => this.RaiseAndSetIfChanged(ref labels, value);
        }

        private Label selectedItem;
        private ObservableCollection<Label> labels;

        public Label SelectedItem
        {
            get => selectedItem;
            set => this.RaiseAndSetIfChanged(ref selectedItem, value);
        }

        public LabelsViewModel()
        {
            // temp hack
            Labels = new ObservableCollection<Label>(GetSampleLabels() ?? new List<Label>());

            // byteEntries = this
            //     .WhenAnyValue(x => x.StartingOffset)
            //     .Throttle(TimeSpan.FromMilliseconds(800))
            //     // .WhenActivated()
            //     .DistinctUntilChanged()
            //     .Where( x=> x != -1)
            //     .SelectMany(GetByteEntries)
            //     .ObserveOn(RxApp.MainThreadScheduler)
            //     .ToProperty(this, x => x.ByteEntries);

            this.WhenActivated(
                (CompositeDisposable disposables) =>
                {
                    // ReactiveCommand.Create<DataGridCellEditEndedEventArgs>(CellEdited);                    
                });
        }

        private List<Label> GetSampleLabels()
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

            return new()
            {
                new Label
                {
                    Comment = "test2",
                    Name = "name2",
                    Offset = 2,
                },
                new Label
                {
                    Comment = "test1",
                    Name = "name1", Offset = 1,
                },
                new Label
                {
                    Comment = "test3",
                    Name = "name3", Offset = 3,
                }
            };
        }
    }
}
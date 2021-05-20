using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Diz.Core.util;
using Diz.Gui.Avalonia.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Diz.Gui.Avalonia.ViewModels
{
    public class MainGridViewModel : ReactiveObject, IActivatableViewModel
    {
        public ViewModelActivator Activator { get; }
        
        [Reactive] 
        public ObservableCollection<Person> People { get; set; }
        
        public MainGridViewModel()
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
        
        private async Task<IEnumerable<ByteEntryDetailsViewModel>?> SearchByteEntries(
            int startRomOffset, CancellationToken token)
        {
            var loader = Service.Container.GetInstance<ISampleProjectLoader>("SampleProjectLoader");
            if (loader == null)
                return null;
            
            var project = await Task.Run(loader.GetSampleProject, token);
            // var (startRomOffset, count) = range;
            
            return project?.Data?.RomByteSource.Bytes
                .Skip(startRomOffset)
                .Take(1)
                .Select(x => new RomByteRowBase {ByteEntry = x})
                .Select(x => new ByteEntryDetailsViewModel(x));

        }



        public class ByteEntryDetailsViewModel : ViewModelBase
        {
            private readonly RomByteRowBase byteEntry;

            public ByteEntryDetailsViewModel(RomByteRowBase byteEntry)
            {
                this.byteEntry = byteEntry;
                SetComment = ReactiveCommand.Create((string comment) =>
                {
                    this.byteEntry.Comment = comment;
                });
            }

            [PublicAPI] public ReactiveCommand<string, Unit> SetComment { get; }

            public string Label => byteEntry.Label;
            public string Comment => byteEntry.Comment;
            public string Offset => byteEntry.Offset;
            public char AsciiCharRep => byteEntry.AsciiCharRep;
            public string NumericRep => byteEntry.NumericRep;
            public string Point => byteEntry.Point;
            public string Instruction => byteEntry.Instruction;
        }
}
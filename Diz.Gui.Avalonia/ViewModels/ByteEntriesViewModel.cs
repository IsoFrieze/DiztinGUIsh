#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using Avalonia.Controls;
using Diz.Controllers.interfaces;
using Diz.Controllers.util;
using Diz.Core.model.byteSources;
using Diz.Core.util;
using LightInject;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Diz.Gui.Avalonia.ViewModels
{
    public class ByteEntriesViewModel : ViewModel, IRowBaseViewer<ByteEntry>
    {
        // [Reactive] public int StartingOffset { get; set; }
        //
        // [Reactive] public int Count { get; set; }
        
        [Reactive] 
        public ObservableCollection<ByteEntryDetailsViewModel> ByteEntries { get; set; }
        
        public Util.NumberBase NumberBaseToShow => Util.NumberBase.Hexadecimal;

        public ByteEntry SelectedByteOffset => SelectedItem?.ByteEntry?.ByteEntry!;

        // public ReactiveCommand<DataGridCellEditEndedEventArgs, Unit> SetSelectedItem { get; }

        private ByteEntryDetailsViewModel? selectedItem = null;

        public ByteEntryDetailsViewModel? SelectedItem
        {
            get => selectedItem;
            set => this.RaiseAndSetIfChanged(ref selectedItem, value);
        }

        // public ReactiveCommand<string, Unit> SetComment { get; }
        // public ByteEntry SelectedByteOffset;
    
        public ByteEntriesViewModel()
        {
            // temp hack
            ByteEntries = new ObservableCollection<ByteEntryDetailsViewModel>(GetByteEntriesSync() ?? Array.Empty<ByteEntryDetailsViewModel>());

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

        private void CellEdited(DataGridCellEditEndedEventArgs args)
        {
            if (args.EditAction != DataGridEditAction.Commit)
                return;
            
            if (args.Column.Header.ToString() == "Comment")
            {
                
            }
        }

        /*private async Task<IEnumerable<ByteEntryDetailsViewModel>?> GetByteEntries(int startRomOffset, CancellationToken token)
        {
            var loader = Service.Container.GetInstance<ISampleProjectLoader>("SampleProjectLoader");
            if (loader == null)
                return null;

            var project = await Task.Run(loader.GetSampleProject, token);

            // TODO: migrate/unify with GetByteEntriesSync()
            return project?.Data?.RomByteSource.Bytes
                //.Skip(startRomOffset)
                //.Take(1)
                .Select(x => new RomByteRowBase {ParentView = this, Data=project.Data, ByteEntry = x})
                .Select(x => new ByteEntryDetailsViewModel {ByteEntry = x});
        }*/

        private IEnumerable<ByteEntryDetailsViewModel>? GetByteEntriesSync()
        {
            var loader = Service.Container.GetInstance<ISampleProjectLoader>("SampleProjectLoader");

            // this interface access code is a little screwed up with Diz here, it's just a starting point.
            
            var project = loader?.GetSampleProject();

            return project?.Data?.RomByteSource.Bytes?.Skip(0)
                .Take(20)
                .Select(x => new ByteEntryDetailsViewModel
                {
                    ByteEntry = new RomByteRowBase
                    {
                        ByteEntry = x,
                        Data = project.Data,
                        ParentView = this,
                    }
                });
        }
    }
}
#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Diz.Controllers.interfaces;
using Diz.Controllers.util;
using Diz.Core.util;
using Diz.Gui.Avalonia.Models;
using LightInject;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Diz.Gui.Avalonia.ViewModels
{
    public class MainGridViewModel : ViewModel
    {
        [Reactive] public int StartingOffset { get; set; }

        [Reactive] public int Count { get; set; }

        public IEnumerable<ByteEntryDetailsViewModel>? ByteEntries => byteEntries.Value;
        private readonly ObservableAsPropertyHelper<IEnumerable<ByteEntryDetailsViewModel>?> byteEntries;

        public MainGridViewModel()
        {
            byteEntries = this
                .WhenAnyValue(x => x.StartingOffset)
                .Throttle(TimeSpan.FromMilliseconds(800))
                // .WhenActivated()
                .DistinctUntilChanged()
                .Where( x=> x != -1)
                .SelectMany(GetByteEntries)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.ByteEntries);

            this.WhenActivated(
                (CompositeDisposable disposables) => { });
        }

        private async Task<IEnumerable<ByteEntryDetailsViewModel>?> GetByteEntries(int startRomOffset, CancellationToken token)
        {
            var loader = Service.Container.GetInstance<ISampleProjectLoader>("SampleProjectLoader");
            if (loader == null)
                return null;

            var project = await Task.Run(loader.GetSampleProject, token);
            // var (startRomOffset, count) = range;

            return project?.Data?.RomByteSource.Bytes
                //.Skip(startRomOffset)
                //.Take(1)
                .Select(x => new RomByteRowBase {ByteEntry = x})
                .Select(x => new ByteEntryDetailsViewModel(x));
        }
    }
}
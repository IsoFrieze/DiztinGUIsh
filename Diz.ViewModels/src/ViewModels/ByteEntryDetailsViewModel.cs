using System.Reactive.Disposables;
using Diz.Controllers.util;
using ReactiveUI;

namespace Diz.ViewModels
{
    public class ByteEntryDetailsViewModel : ViewModel
    {
        public RomByteRowBase ByteEntry { get; init; }

        public ByteEntryDetailsViewModel()
        {
            this.WhenActivated(
                (CompositeDisposable disposables) =>
                {
                    
                });
        }
    }
}
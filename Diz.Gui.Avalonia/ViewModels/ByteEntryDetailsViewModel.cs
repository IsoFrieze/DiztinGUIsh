using System.Reactive;
using Diz.Controllers.util;
using JetBrains.Annotations;
using ReactiveUI;

namespace Diz.Gui.Avalonia.ViewModels
{
    public class ByteEntryDetailsViewModel : ViewModel
    {
        public RomByteRowBase ByteEntry { get; init; }

        public ByteEntryDetailsViewModel()
        {
            SetComment = ReactiveCommand.Create((string comment) => { ByteEntry.Comment = comment; });
        }

        [PublicAPI] public ReactiveCommand<string, Unit> SetComment { get; }
    }
}
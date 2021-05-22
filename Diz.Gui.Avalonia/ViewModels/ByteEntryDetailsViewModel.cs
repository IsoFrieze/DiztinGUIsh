using Diz.Controllers.util;
using ReactiveUI;

namespace Diz.Gui.Avalonia.ViewModels
{
    public class ByteEntryDetailsViewModel : ViewModel
    {
        public RomByteRowBase ByteEntry { get; init; }

        public ByteEntryDetailsViewModel()
        {
        }
    }
}
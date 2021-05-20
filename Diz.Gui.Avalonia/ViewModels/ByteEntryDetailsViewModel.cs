using System.Reactive;
using Diz.Controllers.util;
using JetBrains.Annotations;
using ReactiveUI;

namespace Diz.Gui.Avalonia.ViewModels
{
    public class ByteEntryDetailsViewModel : ViewModel
    {
        private readonly RomByteRowBase byteEntry;

        public ByteEntryDetailsViewModel(RomByteRowBase byteEntry)
        {
            this.byteEntry = byteEntry;
            SetComment = ReactiveCommand.Create((string comment) => { this.byteEntry.Comment = comment; });
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
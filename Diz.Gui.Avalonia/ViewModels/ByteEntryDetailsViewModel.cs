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

        public string Label => ByteEntry.Label;
        public string Comment => ByteEntry.Comment;
        public string Offset => ByteEntry.Offset;
        public char AsciiCharRep => ByteEntry.AsciiCharRep;
        public string NumericRep => ByteEntry.NumericRep;
        public string Point => ByteEntry.Point;
        public string Instruction => ByteEntry.Instruction;
        public string DataBank => ByteEntry.DataBank;
        public string DirectPage => ByteEntry.DirectPage;
        public string IA => ByteEntry.IA;
        public string MFlag => ByteEntry.MFlag;
        public string XFlag => ByteEntry.XFlag;
        public string TypeFlag => ByteEntry.TypeFlag;
    }
}
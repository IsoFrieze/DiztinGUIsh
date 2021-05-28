using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using Diz.Controllers.interfaces;
using Diz.Core.model;
using Diz.Core.model.byteSources;
using Diz.Core.model.snes;
using Diz.Core.util;
using JetBrains.Annotations;

namespace Diz.Controllers.util
{
    public class RomByteRowBase : INotifyPropertyChangedExt
    {
        [DisplayName("Label")]
        [Editable(true)]
        // [CellStyleFormatter(GetBackColorInOut)]
        public string Label
        {
            get => Data.Labels.GetLabelName(Data.ConvertPCtoSnes(ByteEntry.ParentIndex));

            // todo (validate for valid label characters)
            // (note: validation implemented in Furious's branch, integrate here)
            set
            {
                Data.Labels.AddLabel(
                    Data.ConvertPCtoSnes(ByteEntry.ParentIndex),
                    new Label {Name = value},
                    true);
                OnPropertyChanged();
            }
        }

        [DisplayName("PC")]
        [ReadOnly(true)]
        public string Offset => Util.ToHexString6(Data.ConvertPCtoSnes(ByteEntry.ParentIndex));

        // show the byte two different ways: ascii and numeric
        [DisplayName("@")]
        [ReadOnly(true)]
        public char AsciiCharRep => 
            ByteEntry?.Byte == null ? ' ' : ByteEntry.Byte.ToString()[0];

        [DisplayName("#")]
        [ReadOnly(true)]
        public string NumericRep =>
            Util.NumberToBaseString(ByteEntry.ParentIndex, NumberBase);

        [DisplayName("<*>")]
        [ReadOnly(true)]
        public string Point =>
            RomUtil.PointToString(ByteEntry.Point);

        [DisplayName("Instruction")]
        [ReadOnly(true)]
        public string Instruction
        {
            get
            {
                // NOTE: this does not handle instructions whose opcodes cross banks correctly.
                // if we hit this situation, just return empty for the grid, it's likely real instruction won't do this?
                var romOffset = ByteEntry.ParentIndex;
                var len = Data.GetInstructionLength(romOffset);
                return romOffset + len <= Data.GetRomSize() ? Data.GetInstruction(romOffset) : "";
            }
        }

        [DisplayName("IA")]
        [ReadOnly(true)]
        // ReSharper disable once InconsistentNaming
        public string IA
        {
            get
            {
                var ia = Data.GetIntermediateAddressOrPointer(ByteEntry.ParentIndex);
                return ia >= 0 ? Util.ToHexString6(ia) : "";
            }
        }

        [DisplayName("Flag")]
        [ReadOnly(true)]
        public string TypeFlag =>
            Util.GetEnumDescription(Data.GetFlag(ByteEntry.ParentIndex));

        [DisplayName("B")]
        [Editable(true)]
        public string DataBank
        {
            get => Util.NumberToBaseString(Data.GetDataBank(ByteEntry.ParentIndex), Util.NumberBase.Hexadecimal, 2);
            set
            {
                if (!int.TryParse(value, NumberStyles.HexNumber, null, out var parsed))
                    return;

                Data.SetDataBank(ByteEntry.ParentIndex, parsed);
                OnPropertyChanged();
            }
        }

        [DisplayName("D")]
        [Editable(true)]
        public string DirectPage
        {
            get => Util.NumberToBaseString(Data.GetDirectPage(ByteEntry.ParentIndex), Util.NumberBase.Hexadecimal, 4);
            set
            {
                if (!int.TryParse(value, NumberStyles.HexNumber, null, out var parsed))
                    return;

                Data.SetDirectPage(ByteEntry.ParentIndex, parsed);
                OnPropertyChanged();
            }
        }

        [DisplayName("M")]
        [Editable(true)]
        public string MFlag
        {
            get => RomUtil.BoolToSize(Data.GetMFlag(ByteEntry.ParentIndex));
            set
            {
                Data.SetMFlag(ByteEntry.ParentIndex, value is "8" or "M");
                OnPropertyChanged();
            }
        }

        [DisplayName("X")]
        [Editable(true)]
        public string XFlag
        {
            get => RomUtil.BoolToSize(Data.GetXFlag(ByteEntry.ParentIndex));
            set
            {
                Data.SetXFlag(ByteEntry.ParentIndex, value is "8" or "X");
                OnPropertyChanged();
            }
        }

        [DisplayName("Comment")]
        [Editable(true)]
        public string Comment
        {
            get => Data.GetCommentText(Data.ConvertPCtoSnes(ByteEntry.ParentIndex));
            set
            {
                Data.AddComment(Data.ConvertPCtoSnes(ByteEntry.ParentIndex), value, true);
                OnPropertyChanged();
            }
        }
        
        private readonly ByteEntry byteEntry;

        [Browsable(false)]
        public ByteEntry ByteEntry
        {
            get => byteEntry;
            init
            {
                this.SetField(PropertyChanged, ref byteEntry, value);
                // tmp disable // if (ByteOffset != null)
                // ByteOffset.PropertyChanged += OnRomBytePropertyChanged;
            }
        }

        [Browsable(false)] public Data Data { get; init; }
        [Browsable(false)] public IRowBaseViewer<ByteEntry> ParentView { get; init; }
        [Browsable(false)] private Util.NumberBase NumberBase => 
            ParentView?.NumberBaseToShow ?? Util.NumberBase.Hexadecimal;

        [Browsable(false)] public event PropertyChangedEventHandler PropertyChanged;

        private void OnRomBytePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            void OnInstructionRelatedChanged()
            {
                OnPropertyChanged(nameof(Instruction));
                OnPropertyChanged(nameof(IA));
            }

            // NOTE: if any properties under ByteOffset change, make sure the names update here
            switch (e.PropertyName)
            {
                case nameof(ByteEntry.Byte):
                    OnPropertyChanged(nameof(AsciiCharRep));
                    OnPropertyChanged(nameof(NumericRep));
                    OnInstructionRelatedChanged();
                    break;
                case nameof(ByteEntry.Arch):
                    OnInstructionRelatedChanged();
                    break;
                case nameof(ByteEntry.DataBank):
                case nameof(ByteEntry.DirectPage):
                case nameof(ByteEntry.XFlag):
                case nameof(ByteEntry.MFlag):
                case nameof(ByteEntry.TypeFlag):
                case nameof(ByteEntry.Point):
                    OnPropertyChanged(e.PropertyName);
                    break;
            }
        }

        [NotifyPropertyChangedInvocator]
        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        
        #region Formatting
        protected Color? GetBackgroundColorForMarkedAsOpcode(string colPropName)
        {
            // TODO: eventually, don't match strings here.
            // instead, look for the appropriate attribute attached to romByteRow and let that 
            // attribute hook in here.
            return colPropName switch
            {
                nameof(Point) => GetBackColorInOut(),
                nameof(Instruction) => GetInstructionBackgroundColor(),
                nameof(DataBank) => GetDataBankColor(),
                nameof(DirectPage) => GetDirectPageColor(),
                nameof(MFlag) => GetMFlagColor(),
                nameof(XFlag) => GetXFlagColor(),
                _ => null
            };
        }

        private Color? GetBackColorInOut()
        {
            int r = 255, g = 255, b = 255;
            if ((ByteEntry.Point & (InOutPoint.EndPoint | InOutPoint.OutPoint)) != 0) g -= 50;
            if ((ByteEntry.Point & InOutPoint.InPoint) != 0) r -= 50;
            if ((ByteEntry.Point & InOutPoint.ReadPoint) != 0) b -= 50;
            return Color.FromArgb(r, g, b);
        }

        private Color? GetInstructionBackgroundColor()
        {
            var opcode = ByteEntry.Byte;
            var isWeirdInstruction =
                    opcode == 0x40 || opcode == 0xCB || opcode == 0xDB || opcode == 0xF8 || // RTI WAI STP SED
                    opcode == 0xFB || opcode == 0x00 || opcode == 0x02 || opcode == 0x42 // XCE BRK COP WDM
                ;
            return isWeirdInstruction ? Color.Yellow : null;
        }

        private Color? GetDataBankColor()
        {
            switch (ByteEntry.Byte)
            {
                // PLB MVP MVN
                case 0xAB:
                case 0x44:
                case 0x54:
                    return Color.OrangeRed;
                // PHB
                case 0x8B:
                    return Color.Yellow;
                default:
                    return null;
            }
        }

        private Color? GetDirectPageColor()
        {
            switch (ByteEntry.Byte)
            {
                // PLD TCD
                case 0x2B:
                case 0x5B:
                    return Color.OrangeRed;

                // PHD TDC
                case 0x0B:
                case 0x7B:
                    return Color.Yellow;

                default:
                    return null;
            }
        }

        public Color? GetMFlagColor() => GetMxFlagColor(0x20);
        public Color? GetXFlagColor() => GetMxFlagColor(0x10);

        private Color? GetMxFlagColor(int nextByteMask)
        {
            var nextByte = Data.GetNextRomByte(ByteEntry.ParentIndex) ?? 0;
            switch (ByteEntry.Byte)
            {
                // PLP
                // SEP REP, *iff* relevant bit is set on next byte
                case 0x28:
                case 0xC2 or 0xE2 when (nextByte & nextByteMask) != 0:
                    return Color.OrangeRed;
                case 0x08: // PHP
                    return Color.Yellow;
                default:
                    return null;
            }
        }
        
        #endregion
    }

    public static class RomByteRowAttributes
    {
        public static bool IsColumnEditable(string propertyName) => TestAttribute((EditableAttribute attr) => attr?.AllowEdit ?? false, propertyName);

        public static string GetColumnDisplayName(string propertyName) => TestAttribute((DisplayNameAttribute attr) => attr?.DisplayName, propertyName);

        public static bool GetColumnIsReadOnly(string propertyName) => TestAttribute((ReadOnlyAttribute attr) => attr?.IsReadOnly ?? false, propertyName);

        public static bool IsPropertyBrowsable(string propertyName) => TestAttribute((BrowsableAttribute attr) => attr?.Browsable ?? true, propertyName);

        private static TResult TestAttribute<TAttribute, TResult>(
            Func<TAttribute, TResult> getValueFn, string memberName)
            where TAttribute : Attribute
        {
            return Util.GetPropertyAttribute(getValueFn, typeof(RomByteRowBase), memberName);
        }
    }

    public interface IRowBaseViewer<out TItem>
    {
        Util.NumberBase NumberBaseToShow { get; }
        TItem SelectedByteOffset { get; }
    }
}
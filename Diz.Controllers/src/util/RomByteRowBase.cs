using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public static bool IsColumnEditable(string propertyName)
        {
            return TestAttribute((EditableAttribute attr) => attr?.AllowEdit ?? false, propertyName);
        }

        public static string GetColumnDisplayName(string propertyName)
        {
            return TestAttribute((DisplayNameAttribute attr) => attr?.DisplayName, propertyName);
        }
        
        public static bool GetColumnIsReadOnly(string propertyName)
        {
            return TestAttribute((ReadOnlyAttribute attr) => attr?.IsReadOnly ?? false, propertyName);
        }
        
        public static bool IsPropertyBrowsable(string propertyName)
        {
            return TestAttribute((BrowsableAttribute attr) => attr?.Browsable ?? true, propertyName);
        }

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
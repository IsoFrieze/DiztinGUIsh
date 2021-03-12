using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using Diz.Core.model;
using Diz.Core.util;
using JetBrains.Annotations;

namespace DiztinGUIsh.window2
{
    [AttributeUsage(AttributeTargets.Property)]
    public class CellStyleFormatter : Attribute
    {
        public Func<Color?> BackgroundColorFormatter { get; }

        public CellStyleFormatter(Func<Color?> bgColorFormatter)
        {
            BackgroundColorFormatter = bgColorFormatter;
        }
    }
    
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class RomByteDataGridRow : INotifyPropertyChanged
    {
        [DisplayName("Label")]
        [Editable(true)]
        // [CellStyleFormatter(GetBackColorInOut)]
        public string Label
        {
            get => Data.GetLabelName(Data.ConvertPCtoSnes(RomByte.Offset));

            // todo (validate for valid label characters)
            // (note: validation implemented in Furious's branch, integrate here)
            set
            {
                Data.AddLabel(
                    Data.ConvertPCtoSnes(RomByte.Offset),
                    new Label {Name = value},
                    true);
                
                OnPropertyChanged();
            }
        }

        [DisplayName("PC")]
        [ReadOnly(true)]
        public string Offset =>
            Util.NumberToBaseString(Data.ConvertPCtoSnes(RomByte.Offset), Util.NumberBase.Hexadecimal, 6);

        // show the byte two different ways: ascii and numeric
        [DisplayName("@")]
        [ReadOnly(true)]
        public char AsciiCharRep =>
            (char) RomByte.Rom;

        [DisplayName("#")]
        [ReadOnly(true)]
        public string NumericRep =>
            Util.NumberToBaseString(RomByte.Rom, NumberBase);

        [DisplayName("<*>")]
        [ReadOnly(true)]
        public string Point =>
            RomUtil.PointToString(RomByte.Point);

        [DisplayName("Instruction")]
        [ReadOnly(true)]
        public string Instruction
        {
            get
            {
                // NOTE: this does not handle instructions whose opcodes cross banks correctly.
                // if we hit this situation, just return empty for the grid, it's likely real instruction won't do this?
                var len = Data.GetInstructionLength(RomByte.Offset);
                return RomByte.Offset + len <= Data.GetRomSize() ? Data.GetInstruction(RomByte.Offset) : "";
            }
        }

        [DisplayName("IA")]
        [ReadOnly(true)]
        public string IA
        {
            get
            {
                var ia = Data.GetIntermediateAddressOrPointer(RomByte.Offset);
                return ia >= 0 ? Util.NumberToBaseString(ia, Util.NumberBase.Hexadecimal, 6) : "";
            }
        }

        [DisplayName("Flag")]
        [ReadOnly(true)]
        public string TypeFlag => 
            Util.GetEnumDescription(Data.GetFlag(RomByte.Offset));

        [DisplayName("B")]
        [Editable(true)]
        public string DataBank
        {
            get => Util.NumberToBaseString(Data.GetDataBank(RomByte.Offset), Util.NumberBase.Hexadecimal, 2);
            set
            {
                if (!int.TryParse(value, NumberStyles.HexNumber, null, out var parsed))
                    return;

                Data.SetDataBank(RomByte.Offset, parsed);
                OnPropertyChanged();
            }
        }

        [DisplayName("D")]
        [Editable(true)]
        public string DirectPage
        {
            get => Util.NumberToBaseString(Data.GetDirectPage(RomByte.Offset), Util.NumberBase.Hexadecimal, 4);
            set
            {
                if (!int.TryParse(value, NumberStyles.HexNumber, null, out var parsed))
                    return;

                Data.SetDirectPage(RomByte.Offset, parsed);
                OnPropertyChanged();
            }
        }

        [DisplayName("M")]
        [Editable(true)]
        public string MFlag
        {
            get => RomUtil.BoolToSize(Data.GetMFlag(RomByte.Offset));
            set
            {
                Data.SetMFlag(RomByte.Offset, value == "8" || value == "M");
                OnPropertyChanged();
            }
        }

        [DisplayName("X")]
        [Editable(true)]
        public string XFlag
        {
            get => RomUtil.BoolToSize(Data.GetXFlag(RomByte.Offset));
            set
            {
                Data.SetXFlag(RomByte.Offset, value == "8" || value == "X");
                OnPropertyChanged();
            }
        }

        [DisplayName("Comment")]
        [Editable(true)]
        public string Comment
        {
            get => Data.GetComment(Data.ConvertPCtoSnes(RomByte.Offset));
            set
            {
                Data.AddComment(Data.ConvertPCtoSnes(RomByte.Offset), value, true);
                OnPropertyChanged();
            }
        }

        public RomByteDataGridRow(RomByteData rb, Data d, IBytesGridViewer parentView)
        {
            RomByte = rb;
            Data = d;
            ParentView = parentView;

            if (rb != null)
                rb.PropertyChanged += OnRomBytePropertyChanged;
        }

        private void OnRomBytePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            void OnInstructionRelatedChanged()
            {
                OnPropertyChanged(nameof(Instruction));
                OnPropertyChanged(nameof(IA));
            }

            // NOTE: if any properties under RomByte change, make sure the names update here
            switch (e.PropertyName)
            {
                case nameof(RomByteData.Rom):
                    OnPropertyChanged(nameof(AsciiCharRep));
                    OnPropertyChanged(nameof(NumericRep));
                    OnInstructionRelatedChanged();
                    break;
                case nameof(RomByteData.Arch):
                    OnInstructionRelatedChanged();
                    break;
                case nameof(RomByteData.DataBank):
                case nameof(RomByteData.DirectPage):
                case nameof(RomByteData.XFlag):
                case nameof(RomByteData.MFlag):
                case nameof(RomByteData.TypeFlag):
                case nameof(RomByteData.Point):
                    OnPropertyChanged(e.PropertyName);
                    break;
            }
        }

        [Browsable(false)] public RomByteData RomByte { get; }
        [Browsable(false)] public Data Data { get; }
        [Browsable(false)] public IBytesGridViewer ParentView { get; }
        [Browsable(false)] private Util.NumberBase NumberBase => ParentView.DataGridNumberBase;

        [Browsable(false)] public event PropertyChangedEventHandler? PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        public Color? GetBackgroundColorForMarkedAsOpcode(string colPropName)
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
            if ((RomByte.Point & (InOutPoint.EndPoint | InOutPoint.OutPoint)) != 0) g -= 50;
            if ((RomByte.Point & InOutPoint.InPoint) != 0) r -= 50;
            if ((RomByte.Point & InOutPoint.ReadPoint) != 0) b -= 50;
            return Color.FromArgb(r, g, b);
        }

        private Color? GetInstructionBackgroundColor()
        {
            var opcode = RomByte.Rom;
            var isWeirdInstruction =
                    opcode == 0x40 || opcode == 0xCB || opcode == 0xDB || opcode == 0xF8 || // RTI WAI STP SED
                    opcode == 0xFB || opcode == 0x00 || opcode == 0x02 || opcode == 0x42 // XCE BRK COP WDM
                ;
            return isWeirdInstruction ? Color.Yellow : null;
        }

        private Color? GetDataBankColor()
        {
            switch (RomByte.Rom)
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
            switch (RomByte.Rom)
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

        public Color? GetMFlagColor() => GetMXFlagColor(0x20);
        public Color? GetXFlagColor() => GetMXFlagColor(0x10);

        private Color? GetMXFlagColor(int nextByteMask)
        {
            var nextByte = Data.GetNextRomByte(RomByte.Offset) ?? 0;
            switch (RomByte.Rom)
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
    }
}
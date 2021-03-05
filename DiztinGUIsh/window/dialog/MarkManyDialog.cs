using System;
using System.Globalization;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;

namespace DiztinGUIsh.window.dialog
{
    public partial class MarkManyDialog : Form
    {
        public int Start { get; private set; }
        public int End { get; private set; }
        public int Count { get; private set; }
        
        private int value;

        private readonly Data data;

        public MarkManyDialog(int offset, int column, Data data)
        {
            InitializeComponent();
            this.data = data;

            property.SelectedIndex = column switch
            {
                8 => 1,
                9 => 2,
                10 => 3,
                11 => 4,
                _ => 0
            };
            
            Start = offset;
            var rest = this.data.GetRomSize() - Start;
            Count = rest < 0x10 ? rest : 0x10;
            End = Start + Count;

            flagCombo.SelectedIndex = 3;
            archCombo.SelectedIndex = 0;
            mxCombo.SelectedIndex = 0;

            UpdateGroup();
            UpdateText(null);
        }
        
        public int Property => property.SelectedIndex;

        public object Value {
            get
            {
                switch (property.SelectedIndex)
                {
                    case 0:
                        switch (flagCombo.SelectedIndex)
                        {
                            case 0: return Data.FlagType.Unreached;
                            case 1: return Data.FlagType.Opcode;
                            case 2: return Data.FlagType.Operand;
                            case 3: return Data.FlagType.Data8Bit;
                            case 4: return Data.FlagType.Graphics;
                            case 5: return Data.FlagType.Music;
                            case 6: return Data.FlagType.Empty;
                            case 7: return Data.FlagType.Data16Bit;
                            case 8: return Data.FlagType.Pointer16Bit;
                            case 9: return Data.FlagType.Data24Bit;
                            case 10: return Data.FlagType.Pointer24Bit;
                            case 11: return Data.FlagType.Data32Bit;
                            case 12: return Data.FlagType.Pointer32Bit;
                            case 13: return Data.FlagType.Text;
                        }

                        break;
                    case 1:
                    case 2:
                        return value;
                    case 3:
                    case 4:
                        return mxCombo.SelectedIndex != 0;
                    case 5:
                        switch (archCombo.SelectedIndex)
                        {
                            case 0: return Data.Architecture.Cpu65C816;
                            case 1: return Data.Architecture.Apuspc700;
                            case 2: return Data.Architecture.GpuSuperFx;
                        }

                        break;
                    default:
                        return 0;
                }
                
                return 0;
            }
        }

        private void UpdateGroup()
        {
            flagCombo.Visible = (property.SelectedIndex == 0);
            regValue.Visible = (property.SelectedIndex == 1 || property.SelectedIndex == 2);
            mxCombo.Visible = (property.SelectedIndex == 3 || property.SelectedIndex == 4);
            archCombo.Visible = (property.SelectedIndex == 5);
            regValue.MaxLength = (property.SelectedIndex == 1 ? 3 : 5);
            value = property.SelectedIndex == 1 ? data.GetDataBank(Start) : data.GetDirectPage(Start);
        }

        private bool updatingText;

        private void UpdateText(TextBox selected)
        {
            var noBase = radioDec.Checked ? Util.NumberBase.Decimal : Util.NumberBase.Hexadecimal;
            var digits = noBase == Util.NumberBase.Hexadecimal && radioROM.Checked ? 6 : 0;
            var size = data.GetRomSize();
            var maxValue = property.SelectedIndex == 1 ? 0x100 : 0x10000;

            if (Start < 0) Start = 0;
            if (End >= size) End = size - 1;
            Count = End - Start;
            if (Count < 0) Count = 0;
            if (value < 0) value = 0;
            if (value >= maxValue) value = maxValue - 1;

            updatingText = true;
            if (selected != textStart) textStart.Text = Util.NumberToBaseString(radioROM.Checked ? data.ConvertPCtoSnes(Start) : Start, noBase, digits);
            if (selected != textEnd) textEnd.Text = Util.NumberToBaseString(radioROM.Checked ? data.ConvertPCtoSnes(End) : End, noBase, digits);
            if (selected != textCount) textCount.Text = Util.NumberToBaseString(Count, noBase, 0);
            if (selected != regValue) regValue.Text = Util.NumberToBaseString(value, noBase, 0);
            updatingText = false;
        }
        
        private void property_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateGroup();
        }
        
        private void regValue_TextChanged(object sender, EventArgs e)
        {
            var style = radioDec.Checked ? NumberStyles.Number : NumberStyles.HexNumber;

            if (int.TryParse(regValue.Text, style, null, out var result))
            {
                value = result;
            }
        }

        private void okay_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void textCount_TextChanged(object sender, EventArgs e)
        {
            OnTextChanged(textCount, value =>
            {
                Count = value;
                End = Start + Count;
            });
        }

        private void textEnd_TextChanged(object sender, EventArgs e)
        {
            OnTextChanged(textEnd, value =>
            {
                if (radioROM.Checked)
                    value = data.ConvertSnesToPc(value);

                End = value;
                Count = End - Start;
            });
        }

        private void textStart_TextChanged(object sender, EventArgs e)
        {
            OnTextChanged(textStart, value =>
            {
                if (radioROM.Checked)
                    value = data.ConvertSnesToPc(value);

                Start = value;
                Count = End - Start;
            });
        }
        
        private void OnTextChanged(TextBox textBox, Action<int> OnResult)
        {        
            if (updatingText)
                return;

            updatingText = true;
            var style = radioDec.Checked ? NumberStyles.Number : NumberStyles.HexNumber;

            if (int.TryParse(textBox.Text, style, null, out var result))
                OnResult(result);

            UpdateText(textBox);
        }

        private void radioHex_CheckedChanged(object sender, EventArgs e)
        {
            UpdateText(null);
        }

        private void radioROM_CheckedChanged(object sender, EventArgs e)
        {
            UpdateText(null);
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiztinGUIsh
{
    public partial class MarkManyDialog : Form
    {
        private int start, end, count, value;

        private Data Data;

        public MarkManyDialog(int offset, int column, Data data)
        {
            InitializeComponent();
            Data = data;

            switch (column)
            {
                case 8: property.SelectedIndex = 1; break;
                case 9: property.SelectedIndex = 2; break;
                case 10: property.SelectedIndex = 3; break;
                case 11: property.SelectedIndex = 4; break;
                default: property.SelectedIndex = 0; break;
            }
            start = offset;
            int rest = Data.GetROMSize() - start;
            count = rest < 0x10 ? rest : 0x10;
            end = start + count;

            flagCombo.SelectedIndex = 3;
            archCombo.SelectedIndex = 0;
            mxCombo.SelectedIndex = 0;

            UpdateGroup();
            UpdateText(null);
        }

        public int GetOffset()
        {
            return start;
        }

        public int GetCount()
        {
            return count;
        }

        public int GetProperty()
        {
            return property.SelectedIndex;
        }

        public object GetValue()
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
                        case 0: return Data.Architechture.CPU65C816;
                        case 1: return Data.Architechture.APUSPC700;
                        case 2: return Data.Architechture.GPUSuperFX;
                    }
                    break;
            }
            return 0;
        }

        private void UpdateGroup()
        {
            flagCombo.Visible = (property.SelectedIndex == 0);
            regValue.Visible = (property.SelectedIndex == 1 || property.SelectedIndex == 2);
            mxCombo.Visible = (property.SelectedIndex == 3 || property.SelectedIndex == 4);
            archCombo.Visible = (property.SelectedIndex == 5);
            regValue.MaxLength = (property.SelectedIndex == 1 ? 3 : 5);
            value = property.SelectedIndex == 1 ? Data.GetDataBank(start) : Data.GetDirectPage(start);
        }

        private bool updatingText = false;

        private void UpdateText(TextBox selected)
        {
            Util.NumberBase noBase = radioDec.Checked ? Util.NumberBase.Decimal : Util.NumberBase.Hexadecimal;
            int digits = noBase == Util.NumberBase.Hexadecimal && radioROM.Checked ? 6 : 0;
            int size = Data.GetROMSize();
            int maxValue = property.SelectedIndex == 1 ? 0x100 : 0x10000;

            if (start < 0) start = 0;
            if (end >= size) end = size - 1;
            count = end - start;
            if (count < 0) count = 0;
            if (value < 0) value = 0;
            if (value >= maxValue) value = maxValue - 1;

            updatingText = true;
            if (selected != textStart) textStart.Text = Util.NumberToBaseString(radioROM.Checked ? Data.ConvertPCtoSNES(start) : start, noBase, digits);
            if (selected != textEnd) textEnd.Text = Util.NumberToBaseString(radioROM.Checked ? Data.ConvertPCtoSNES(end) : end, noBase, digits);
            if (selected != textCount) textCount.Text = Util.NumberToBaseString(count, noBase, 0);
            if (selected != regValue) regValue.Text = Util.NumberToBaseString(value, noBase, 0);
            updatingText = false;
        }

        private void textCount_TextChanged(object sender, EventArgs e)
        {
            if (!updatingText)
            {
                updatingText = true;
                NumberStyles style = radioDec.Checked ? NumberStyles.Number : NumberStyles.HexNumber;

                int result = 0;
                if (int.TryParse(textCount.Text, style, null, out result))
                {
                    count = result;
                    end = start + count;
                }

                UpdateText(textCount);
            }
        }

        private void textEnd_TextChanged(object sender, EventArgs e)
        {
            if (!updatingText)
            {
                updatingText = true;
                NumberStyles style = radioDec.Checked ? NumberStyles.Number : NumberStyles.HexNumber;

                int result = 0;
                if (int.TryParse(textEnd.Text, style, null, out result))
                {
                    if (radioROM.Checked) result = Data.ConvertSNEStoPC(result);
                    end = result;
                    count = end - start;
                }

                UpdateText(textEnd);
            }
        }

        private void property_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateGroup();
        }

        private void regValue_TextChanged(object sender, EventArgs e)
        {
            NumberStyles style = radioDec.Checked ? NumberStyles.Number : NumberStyles.HexNumber;

            int result = 0;
            if (int.TryParse(regValue.Text, style, null, out result))
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
            this.Close();
        }

        private void textStart_TextChanged(object sender, EventArgs e)
        {
            if (!updatingText)
            {
                updatingText = true;
                NumberStyles style = radioDec.Checked ? NumberStyles.Number : NumberStyles.HexNumber;

                int result = 0;
                if (int.TryParse(textStart.Text, style, null, out result))
                {
                    if (radioROM.Checked) result = Data.ConvertSNEStoPC(result);
                    start = result;
                    count = end - start;
                }

                UpdateText(textStart);
            }
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

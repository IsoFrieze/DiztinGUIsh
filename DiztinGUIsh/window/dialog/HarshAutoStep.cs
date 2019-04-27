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
    public partial class HarshAutoStep : Form
    {
        private int start, end, count;

        public HarshAutoStep(int offset)
        {
            InitializeComponent();

            start = offset;
            int rest = Data.GetROMSize() - start;
            count = rest < 0x100 ? rest : 0x100;
            end = start + count;

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

        private bool updatingText = false;

        private void UpdateText(TextBox selected)
        {
            Util.NumberBase noBase = radioDec.Checked ? Util.NumberBase.Decimal : Util.NumberBase.Hexadecimal;
            int digits = noBase == Util.NumberBase.Hexadecimal && radioROM.Checked ? 6 : 0;
            int size = Data.GetROMSize();

            if (start < 0) start = 0;
            if (end >= size) end = size - 1;
            count = end - start;
            if (count < 0) count = 0;

            updatingText = true;
            if (selected != textStart) textStart.Text = Util.NumberToBaseString(radioROM.Checked ? Util.ConvertPCtoSNES(start) : start, noBase, digits);
            if (selected != textEnd) textEnd.Text = Util.NumberToBaseString(radioROM.Checked ? Util.ConvertPCtoSNES(end) : end, noBase, digits);
            if (selected != textCount) textCount.Text = Util.NumberToBaseString(count, noBase, 0);
            updatingText = false;
        }

        private void radioHex_CheckedChanged(object sender, EventArgs e)
        {
            UpdateText(null);
        }

        private void radioROM_CheckedChanged(object sender, EventArgs e)
        {
            UpdateText(null);
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
                    if (radioROM.Checked) result = Util.ConvertSNEStoPC(result);
                    end = result;
                    count = end - start;
                }

                UpdateText(textEnd);
            }
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
                    if (radioROM.Checked) result = Util.ConvertSNEStoPC(result);
                    start = result;
                    count = end - start;
                }

                UpdateText(textStart);
            }
        }

        private void go_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}

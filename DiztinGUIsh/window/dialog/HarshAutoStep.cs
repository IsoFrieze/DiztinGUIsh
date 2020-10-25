using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;

namespace DiztinGUIsh
{
    public partial class HarshAutoStep : Form
    {
        private int start, end, count;

        private readonly Data data;

        public HarshAutoStep(int offset, Data data)
        {
            Debug.Assert(this.data!=null);
            this.data = data;

            InitializeComponent();

            start = offset;
            var rest = data.GetRomSize() - start;
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

        private bool updatingText;

        private void UpdateText(TextBox selected)
        {
            Util.NumberBase noBase = radioDec.Checked ? Util.NumberBase.Decimal : Util.NumberBase.Hexadecimal;
            int digits = noBase == Util.NumberBase.Hexadecimal && radioROM.Checked ? 6 : 0;

            if (start < 0) start = 0;
            if (end >= data.GetRomSize()) end = data.GetRomSize() - 1;
            count = end - start;
            if (count < 0) count = 0;

            updatingText = true;
            if (selected != textStart) textStart.Text = Util.NumberToBaseString(radioROM.Checked ? data.ConvertPCtoSnes(start) : start, noBase, digits);
            if (selected != textEnd) textEnd.Text = Util.NumberToBaseString(radioROM.Checked ? data.ConvertPCtoSnes(end) : end, noBase, digits);
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
                var style = radioDec.Checked ? NumberStyles.Number : NumberStyles.HexNumber;

                if (int.TryParse(textCount.Text, style, null, out var result))
                {
                    count = result;
                    end = start + count;
                }

                UpdateText(textCount);
            }
        }

        private void textEnd_TextChanged(object sender, EventArgs e)
        {
            if (updatingText) 
                return;

            updatingText = true;
            var style = radioDec.Checked ? NumberStyles.Number : NumberStyles.HexNumber;

            if (int.TryParse(textEnd.Text, style, null, out var result))
            {
                if (radioROM.Checked) 
                    result = data.ConvertSnesToPc(result);

                end = result;
                count = end - start;
            }

            UpdateText(textEnd);
        }

        private void textStart_TextChanged(object sender, EventArgs e)
        {
            if (updatingText) 
                return;

            updatingText = true;
            var style = radioDec.Checked ? NumberStyles.Number : NumberStyles.HexNumber;

            if (int.TryParse(textStart.Text, style, null, out var result))
            {
                if (radioROM.Checked) 
                    result = data.ConvertSnesToPc(result);
                start = result;
                count = end - start;
            }

            UpdateText(textStart);
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

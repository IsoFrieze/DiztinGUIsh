using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
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

        private readonly Data Data;

        public HarshAutoStep(int offset, Data data)
        {
            Debug.Assert(Data!=null);
            Data = data;

            InitializeComponent();

            start = offset;
            var rest = data.GetROMSize() - start;
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

            if (start < 0) start = 0;
            if (end >= Data.GetROMSize()) end = Data.GetROMSize() - 1;
            count = end - start;
            if (count < 0) count = 0;

            updatingText = true;
            if (selected != textStart) textStart.Text = Util.NumberToBaseString(radioROM.Checked ? Data.ConvertPCtoSNES(start) : start, noBase, digits);
            if (selected != textEnd) textEnd.Text = Util.NumberToBaseString(radioROM.Checked ? Data.ConvertPCtoSNES(end) : end, noBase, digits);
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
                    result = Data.ConvertSNEStoPC(result);

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
                    result = Data.ConvertSNEStoPC(result);
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

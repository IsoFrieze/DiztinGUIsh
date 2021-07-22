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
        public int StartRomOffset { get; private set; }
        public int EndRomOffset { get; private set; }
        public int Count { get; private set; }
        
        private readonly Data data;
        private bool updatingText;

        public HarshAutoStep(int offset, Data data)
        {
            Debug.Assert(data != null);
            this.data = data;

            InitializeComponent();

            StartRomOffset = offset;
            var rest = data.GetRomSize() - StartRomOffset;
            Count = rest < 0x100 ? rest : 0x100;
            EndRomOffset = StartRomOffset + Count;

            UpdateText(null);
        }

        private void UpdateText(TextBox selected)
        {
            var noBase = radioDec.Checked ? Util.NumberBase.Decimal : Util.NumberBase.Hexadecimal;
            var digits = noBase == Util.NumberBase.Hexadecimal && radioSNES.Checked ? 6 : 0;

            if (StartRomOffset < 0) StartRomOffset = 0;
            if (EndRomOffset >= data.GetRomSize()) EndRomOffset = data.GetRomSize() - 1;
            Count = EndRomOffset - StartRomOffset;
            if (Count < 0) Count = 0;

            updatingText = true;
            if (selected != textStart) textStart.Text = Util.NumberToBaseString(radioSNES.Checked ? data.ConvertPCtoSnes(StartRomOffset) : StartRomOffset, noBase, digits);
            if (selected != textEnd) textEnd.Text = Util.NumberToBaseString(radioSNES.Checked ? data.ConvertPCtoSnes(EndRomOffset) : EndRomOffset, noBase, digits);
            if (selected != textCount) textCount.Text = Util.NumberToBaseString(Count, noBase, 0);
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
            OnTextChanged(textCount, value =>
            {
                Count = value;
                EndRomOffset = StartRomOffset + Count;
            });
        }

        private void textEnd_TextChanged(object sender, EventArgs e)
        {
            OnTextChanged(textEnd, value =>
            {
                if (radioSNES.Checked)
                    value = data.ConvertSnesToPc(value);

                EndRomOffset = value;
                Count = EndRomOffset - StartRomOffset;
            });
        }

        private void textStart_TextChanged(object sender, EventArgs e)
        {
            OnTextChanged(textStart, value =>
            {
                if (radioSNES.Checked)
                    value = data.ConvertSnesToPc(value);

                StartRomOffset = value;
                Count = EndRomOffset - StartRomOffset;
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

        private void go_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}

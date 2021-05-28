using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;
using Diz.Core.model.snes;
using Diz.Core.util;

namespace DiztinGUIsh.window.dialog
{
    public partial class HarshAutoStep : Form
    {
        public int Start { get; private set; }
        public int End { get; private set; }
        public int Count { get; private set; }
        
        private readonly Data data;
        private bool updatingText;

        public HarshAutoStep(int offset, Data data)
        {
            Debug.Assert(data != null);
            this.data = data;

            InitializeComponent();

            Start = offset;
            var rest = data.GetRomSize() - Start;
            Count = rest < 0x100 ? rest : 0x100;
            End = Start + Count;

            UpdateText(null);
        }

        private void UpdateText(TextBox selected)
        {
            var noBase = radioDec.Checked ? Util.NumberBase.Decimal : Util.NumberBase.Hexadecimal;
            var digits = noBase == Util.NumberBase.Hexadecimal && radioROM.Checked ? 6 : 0;

            if (Start < 0) Start = 0;
            if (End >= data.GetRomSize()) End = data.GetRomSize() - 1;
            Count = End - Start;
            if (Count < 0) Count = 0;

            updatingText = true;
            if (selected != textStart) textStart.Text = Util.NumberToBaseString(radioROM.Checked ? data.ConvertPCtoSnes(Start) : Start, noBase, digits);
            if (selected != textEnd) textEnd.Text = Util.NumberToBaseString(radioROM.Checked ? data.ConvertPCtoSnes(End) : End, noBase, digits);
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

        private void OnTextChanged(TextBox textBox, Action<int> onResult)
        {
            if (updatingText)
                return;

            updatingText = true;
            var style = radioDec.Checked ? NumberStyles.Number : NumberStyles.HexNumber;

            if (int.TryParse(textBox.Text, style, null, out var result))
                onResult(result);

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

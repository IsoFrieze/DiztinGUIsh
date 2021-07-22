﻿using System;
using System.Globalization;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;

namespace DiztinGUIsh.window.dialog
{
    public partial class GotoDialog : Form
    {
        public Data Data { get; set; }
        public GotoDialog(int offset, Data data)
        {
            InitializeComponent();
            Data = data;
            textROM.Text = Util.NumberToBaseString(Data.ConvertPCtoSnes(offset), Util.NumberBase.Hexadecimal, 6);
            textPC.Text = Util.NumberToBaseString(offset, Util.NumberBase.Hexadecimal, 0);
        }

        private void GotoDialog_Load(object sender, EventArgs e)
        {
            textROM.SelectAll();
            UpdateUi();
        }

        private int ParseOffset(string text)
        {
            NumberStyles style = radioDec.Checked ? NumberStyles.Number : NumberStyles.HexNumber;
            if (int.TryParse(text, style, null, out int offset)) return offset;
            return -1;
        }

        public int GetPcOffset()
        {
            return ParseOffset(textPC.Text);
        }

        private void Go()
        {
            this.DialogResult = DialogResult.OK;
        }

        private bool updatingText;

        private bool UpdateTextChanged(string txtChanged, Action<string, int, Util.NumberBase> onSuccess)
        {
            bool result = false;
            if (!updatingText)
            {
                updatingText = true;

                var style = radioDec.Checked ? NumberStyles.Number : NumberStyles.HexNumber;
                var noBase = radioDec.Checked ? Util.NumberBase.Decimal : Util.NumberBase.Hexadecimal;

                if (ByteUtil.StripFormattedAddress(ref txtChanged, style, out var address) && address >= 0)
                {
                    onSuccess(txtChanged, address, noBase);
                    result = true;
                }
                updatingText = false;
            }

            return result;
        }

        // For both textbox TextChanged events:
        // precondition: unvalidated input in textbox
        // postcondtion: valid text is in both textboxes, or, button is greyed out and error message displayed.

        private void UpdateUi()
        {
            var valid = true;
            lblError.Text = "";

            if (!IsPcOffsetValid())
            {
                lblError.Text = "Invalid ROM File Offset";
                valid = false;
            }

            if (!IsRomAddressValid())
            {
                lblError.Text = "Invalid SNES Address";
                valid = false;
            }

            go.Enabled = valid;
        }

        private bool IsValidPcAddress(int pc)
        {
            return pc >= 0 && pc < Data.GetRomSize();
        }

        private bool IsPcOffsetValid()
        {
            var offset = GetPcOffset();
            return IsValidPcAddress(offset);
        }

        private bool IsRomAddressValid()
        {
            var address = ParseOffset(textROM.Text);
            if (address < 0)
                return false;
            
            return IsValidPcAddress(Data.ConvertSnesToPc(address));
        }

        private void textROM_TextChanged(object sender, EventArgs e)
        {
            UpdateTextChanged(textROM.Text,(finaltext, address, noBase) =>
            {
                int pc = Data.ConvertSnesToPc(address);
                
                textROM.Text = finaltext;
                textPC.Text = Util.NumberToBaseString(pc, noBase, 0);
            });

            UpdateUi();
        }

        private void textPC_TextChanged(object sender, EventArgs e)
        {
            UpdateTextChanged(textPC.Text, (finaltext, offset, noBase) =>
            {
                int addr = Data.ConvertPCtoSnes(offset);

                textPC.Text = finaltext;
                textROM.Text = Util.NumberToBaseString(addr, noBase, 6);
            });

            UpdateUi();
        }

        private void go_Click(object sender, EventArgs e)
        {
            Go();
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void textROM_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && textROM.Text.Length > 0) Go();
        }

        private void textPC_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && textPC.Text.Length > 0) Go();
        }

        private void radioHex_CheckedChanged(object sender, EventArgs e)
        {
            if (radioHex.Checked) {
                if (int.TryParse(textPC.Text, out var result))
                {
                    textPC.Text = Util.NumberToBaseString(result, Util.NumberBase.Hexadecimal, 0);
                }
            } else {
                if (int.TryParse(textPC.Text, NumberStyles.HexNumber, null, out var result))
                {
                    textPC.Text = result.ToString();
                }
            }
        }
    }
}

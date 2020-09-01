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
    public partial class GotoDialog : Form
    {
        public GotoDialog(int offset)
        {
            InitializeComponent();
            textROM.Text = Util.NumberToBaseString(Util.ConvertPCtoSNES(offset), Util.NumberBase.Hexadecimal, 6);
            textPC.Text = Util.NumberToBaseString(offset, Util.NumberBase.Hexadecimal, 0);
        }

        private void GotoDialog_Load(object sender, EventArgs e)
        {
            textROM.SelectAll();
            UpdateUI();
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

        private bool updatingText = false;

        private bool UpdateTextChanged(string txtChanged, Action<string, int, Util.NumberBase> onSuccess)
        {
            bool result = false;
            if (!updatingText)
            {
                updatingText = true;

                NumberStyles style = radioDec.Checked ? NumberStyles.Number : NumberStyles.HexNumber;
                Util.NumberBase noBase = radioDec.Checked ? Util.NumberBase.Decimal : Util.NumberBase.Hexadecimal;

                if (Util.StripFormattedAddress(ref txtChanged, style, out var address) && address >= 0)
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

        private void UpdateUI()
        {
            bool valid = true;
            lblError.Text = "";

            if (!IsPCOffsetValid())
            {
                lblError.Text = "Invalid PC Offset";
                valid = false;
            }

            if (!IsRomAddressValid())
            {
                lblError.Text = "Invalid ROM Address";
                valid = false;
            }

            go.Enabled = valid;
        }

        private bool IsValidPCAddress(int pc)
        {
            return pc >= 0 && pc < Data.GetROMSize();
        }

        private bool IsPCOffsetValid()
        {
            var offset = GetPcOffset();
            return IsValidPCAddress(offset);
        }

        private bool IsRomAddressValid()
        {
            var address = ParseOffset(textROM.Text);
            if (address < 0)
                return false;
            
            return IsValidPCAddress(Util.ConvertSNEStoPC(address));
        }

        private void textROM_TextChanged(object sender, EventArgs e)
        {
            UpdateTextChanged(textROM.Text,(finaltext, address, noBase) =>
            {
                int pc = Util.ConvertSNEStoPC(address);
                
                textROM.Text = finaltext;
                textPC.Text = Util.NumberToBaseString(pc, noBase, 0);
            });

            UpdateUI();
        }

        private void textPC_TextChanged(object sender, EventArgs e)
        {
            UpdateTextChanged(textPC.Text, (finaltext, offset, noBase) =>
            {
                int addr = Util.ConvertPCtoSNES(offset);

                textPC.Text = finaltext;
                textROM.Text = Util.NumberToBaseString(addr, noBase, 6);
            });

            UpdateUI();
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
            if (radioHex.Checked)
            {
                int result;
                if (int.TryParse(textPC.Text, out result))
                {
                    textPC.Text = Util.NumberToBaseString(result, Util.NumberBase.Hexadecimal, 0);
                }
            } else
            {
                int result;
                if (int.TryParse(textPC.Text, NumberStyles.HexNumber, null, out result))
                {
                    textPC.Text = result.ToString();
                }
            }
        }
    }
}

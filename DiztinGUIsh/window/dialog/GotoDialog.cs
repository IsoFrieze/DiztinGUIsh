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
        }

        public int GetOffset()
        {
            int offset;
            NumberStyles style = radioDec.Checked ? NumberStyles.Number : NumberStyles.HexNumber;
            if (int.TryParse(textPC.Text, style, null, out offset)) return offset;
            return -1;
        }

        private void Go()
        {
            this.DialogResult = DialogResult.OK;
        }

        private bool updatingText = false;

        private void textROM_TextChanged(object sender, EventArgs e)
        {
            if (!updatingText)
            {
                updatingText = true;

                int address;
                NumberStyles style = radioDec.Checked ? NumberStyles.Number : NumberStyles.HexNumber;
                Util.NumberBase noBase = radioDec.Checked ? Util.NumberBase.Decimal : Util.NumberBase.Hexadecimal;
                if (int.TryParse(textROM.Text, style, null, out address))
                {
                    int pc = Util.ConvertSNEStoPC(address);
                    if (pc >= 0 && pc < Data.GetROMSize()) textPC.Text = Util.NumberToBaseString(pc, noBase, 0);
                }
                updatingText = false;
            }
        }

        private void textPC_TextChanged(object sender, EventArgs e)
        {
            if (!updatingText)
            {
                updatingText = true;

                NumberStyles style = radioDec.Checked ? NumberStyles.Number : NumberStyles.HexNumber;
                Util.NumberBase noBase = radioDec.Checked ? Util.NumberBase.Decimal : Util.NumberBase.Hexadecimal;
                int offset;
                if (int.TryParse(textPC.Text, style, null, out offset))
                {
                    int addr = Util.ConvertPCtoSNES(offset);
                    if (addr >= 0) textROM.Text = Util.NumberToBaseString(addr, noBase, 6);
                }
                updatingText = false;
            }
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

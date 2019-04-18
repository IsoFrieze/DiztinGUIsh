using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiztinGUIsh
{
    public partial class MisalignmentChecker : Form
    {
        public MisalignmentChecker()
        {
            InitializeComponent();
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void buttonScan_Click(object sender, EventArgs e)
        {
            textLog.Text = "";
            int found = 0, offset = 0;

            while (found < 500 && offset < Data.GetROMSize())
            {
                if (Data.GetFlag(offset) == Data.FlagType.Opcode)
                {
                    int len = Manager.GetInstructionLength(offset);
                    for (int i = 1; i < len; i++)
                    {
                        if (Data.GetFlag(offset + i) != Data.FlagType.Operand)
                        {
                            found++;
                            textLog.Text += string.Format("{0} (0x{1}): {2} is not Operand\r\n",
                                Util.NumberToBaseString(Util.ConvertPCtoSNES(offset + i), Util.NumberBase.Hexadecimal, 6, true),
                                Util.NumberToBaseString(offset + i, Util.NumberBase.Hexadecimal, 0),
                                Data.GetFlag(offset + i).ToString());
                        }
                    }
                    offset += len;
                } else if (Data.GetFlag(offset) == Data.FlagType.Operand)
                {
                    found++;
                    textLog.Text += string.Format("{0} (0x{1}): Operand without Opcode\r\n",
                        Util.NumberToBaseString(Util.ConvertPCtoSNES(offset), Util.NumberBase.Hexadecimal, 6, true),
                        Util.NumberToBaseString(offset, Util.NumberBase.Hexadecimal, 0));
                    offset++;
                } else
                {
                    offset++;
                }
            }

            if (found == 0) textLog.Text = "No misaligned instructions found!";
        }

        private void buttonFix_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
        }
    }
}

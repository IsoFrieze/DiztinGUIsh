using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DiztinGUIsh.core.util;

namespace DiztinGUIsh
{
    public partial class MisalignmentChecker : Form
    {
        private Data Data { get; set; }
        public MisalignmentChecker(Data data)
        {
            Data = data;
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
                Data.FlagType flag = Data.GetFlag(offset), check = flag == Data.FlagType.Opcode ? Data.FlagType.Operand : flag;
                int step = flag == Data.FlagType.Opcode ? Data.GetInstructionLength(offset) : RomUtil.TypeStepSize(flag);

                if (flag == Data.FlagType.Operand)
                {
                    found++;
                    textLog.Text += string.Format("{0} (0x{1}): Operand without Opcode\r\n",
                        Util.NumberToBaseString(Data.ConvertPCtoSNES(offset), Util.NumberBase.Hexadecimal, 6, true),
                        Util.NumberToBaseString(offset, Util.NumberBase.Hexadecimal, 0));
                } else if (step > 1)
                {
                    for (int i = 1; i < step; i++)
                    {
                        if (Data.GetFlag(offset + i) != check)
                        {
                            found++;
                            textLog.Text += string.Format("{0} (0x{1}): {2} is not {3}\r\n",
                                Util.NumberToBaseString(Data.ConvertPCtoSNES(offset + i), Util.NumberBase.Hexadecimal, 6, true),
                                Util.NumberToBaseString(offset + i, Util.NumberBase.Hexadecimal, 0),
                                RomUtil.TypeToString(Data.GetFlag(offset + i)),
                                RomUtil.TypeToString(check));
                        }
                    }
                }

                offset += step;
            }

            if (found == 0) textLog.Text = "No misaligned flags found!";
        }

        private void buttonFix_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
        }
    }
}

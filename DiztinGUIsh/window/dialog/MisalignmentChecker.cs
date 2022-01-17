using System;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.Core.util;
using Diz.Cpu._65816;

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

            while (found < 500 && offset < Data.GetRomSize())
            {
                FlagType flag = Data.GetSnesApi().GetFlag(offset), check = flag == FlagType.Opcode ? FlagType.Operand : flag;
                var step = flag == FlagType.Opcode ? Data.GetSnesApi().GetInstructionLength(offset) : RomUtil.GetByteLengthForFlag(flag);

                if (flag == FlagType.Operand)
                {
                    found++;
                    textLog.Text +=
                        $"{Util.NumberToBaseString(Data.ConvertPCtoSnes(offset), Util.NumberBase.Hexadecimal, 6, true)} (0x{Util.NumberToBaseString(offset, Util.NumberBase.Hexadecimal, 0)}): Operand without Opcode\r\n";
                } else if (step > 1)
                {
                    for (var i = 1; i < step; i++)
                    {
                        if (Data.GetSnesApi().GetFlag(offset + i) == check) continue;
                        found++;
                        textLog.Text +=
                            $"{Util.NumberToBaseString(Data.ConvertPCtoSnes(offset + i), Util.NumberBase.Hexadecimal, 6, true)} (0x{Util.NumberToBaseString(offset + i, Util.NumberBase.Hexadecimal, 0)}): {Util.GetEnumDescription(Data.GetSnesApi().GetFlag(offset + i))} is not {Util.GetEnumDescription(check)}\r\n";
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

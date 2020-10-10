using System;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;

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
                var step = flag == Data.FlagType.Opcode ? Data.GetInstructionLength(offset) : RomUtil.GetByteLengthForFlag(flag);

                if (flag == Data.FlagType.Operand)
                {
                    found++;
                    textLog.Text +=
                        $"{Util.NumberToBaseString(Data.ConvertPCtoSNES(offset), Util.NumberBase.Hexadecimal, 6, true)} (0x{Util.NumberToBaseString(offset, Util.NumberBase.Hexadecimal, 0)}): Operand without Opcode\r\n";
                } else if (step > 1)
                {
                    for (var i = 1; i < step; i++)
                    {
                        if (Data.GetFlag(offset + i) == check) continue;
                        found++;
                        textLog.Text +=
                            $"{Util.NumberToBaseString(Data.ConvertPCtoSNES(offset + i), Util.NumberBase.Hexadecimal, 6, true)} (0x{Util.NumberToBaseString(offset + i, Util.NumberBase.Hexadecimal, 0)}): {Util.GetEnumDescription(Data.GetFlag(offset + i))} is not {Util.GetEnumDescription(check)}\r\n";
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

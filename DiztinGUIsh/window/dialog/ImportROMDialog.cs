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
    public partial class ImportROMDialog : Form
    {
        private Data.ROMMapMode mode;
        private Data.ROMSpeed speed;
        private string title;
        private byte[] data;
        private int offset;
        private string[,] vectorNames = new string[2, 6]
        {
            { "Native_COP", "Native_BRK", "Native_ABORT", "Native_NMI", "Native_RESET", "Native_IRQ"},
            { "Emulation_COP", "Emulation_Unknown", "Emulation_ABORT", "Emulation_NMI", "Emulation_RESET", "Emulation_IRQBRK"}
        };
        private TextBox[,] vectors;
        private CheckBox[,] checkboxes;

        public ImportROMDialog(byte [] rom)
        {
            InitializeComponent();
            vectors = new TextBox[2, 6]
            {
                { textNativeCOP, textNativeBRK, textNativeABORT, textNativeNMI, textNativeRESET, textNativeIRQ },
                { textEmuCOP, textEmuBRK, textEmuABORT, textEmuNMI, textEmuRESET, textEmuIRQ },
            };
            checkboxes = new CheckBox[2, 6]
            {
                { checkboxNativeCOP, checkboxNativeBRK, checkboxNativeABORT, checkboxNativeNMI, checkboxNativeRESET, checkboxNativeIRQ },
                { checkboxEmuCOP, checkboxEmuBRK, checkboxEmuABORT, checkboxEmuNMI, checkboxEmuRESET, checkboxEmuIRQ },
            };
            data = rom;
            mode = DetectROMMapMode();
            UpdateOffsetAndSpeed();
        }

        public Dictionary<int, Data.AliasInfo> GetGeneratedLabels()
        {
            var labels = new Dictionary<int, Data.AliasInfo>();
            
            for (int i = 0; i < checkboxes.GetLength(0); i++)
            {
                for (int j = 0; j < checkboxes.GetLength(1); j++)
                {
                    if (checkboxes[i, j].Checked)
                    {
                        int index = offset + 15 + 0x10 * i + 2 * j;
                        int val = data[index] + (data[index + 1] << 8);
                        int pc = Util.ConvertSNEStoPC(val);
                        if (pc >= 0 && pc < data.Length && !labels.ContainsKey(val)) 
                            labels.Add(val, new Data.AliasInfo() {name = vectorNames[i, j]}); 
                    }
                }
            }

            return labels;
        }

        public Dictionary<int, Data.FlagType> GetHeaderFlags()
        {
            Dictionary<int, Data.FlagType> flags = new Dictionary<int, Data.FlagType>();

            if (checkHeader.Checked)
            {
                for (int i = 0; i < 0x15; i++) flags.Add(offset - 0x15 + i, Data.FlagType.Text);
                for (int i = 0; i < 7; i++) flags.Add(offset + i, Data.FlagType.Data8Bit);
                for (int i = 0; i < 4; i++) flags.Add(offset + 7 + i, Data.FlagType.Data16Bit);
                for (int i = 0; i < 0x20; i++) flags.Add(offset + 11 + i, Data.FlagType.Pointer16Bit);

                if (data[offset - 1] == 0)
                {
                    flags.Remove(offset - 1);
                    flags.Add(offset - 1, Data.FlagType.Data8Bit);
                    for (int i = 0; i < 0x10; i++) flags.Add(offset - 0x25 + i, Data.FlagType.Data8Bit);
                }
                else if (data[offset + 5] == 0x33)
                {
                    for (int i = 0; i < 6; i++) flags.Add(offset - 0x25 + i, Data.FlagType.Text);
                    for (int i = 0; i < 10; i++) flags.Add(offset - 0x1F + i, Data.FlagType.Data8Bit);
                }
            }

            return flags;
        }

        private Data.ROMMapMode DetectROMMapMode()
        {
            if ((data[Data.LOROM_SETTING_OFFSET] & 0xEF) == 0x23)
            {
                if (data.Length > 0x400000)
                {
                    detectMessage.Text = "ROM Map Mode Detected: SA-1 ROM (FuSoYa's 8MB mapper)";
                    comboBox1.SelectedIndex = 4;
                    return Data.ROMMapMode.ExSA1ROM;
                }
                else
                {
                    detectMessage.Text = "ROM Map Mode Detected: SA-1 ROM";
                    comboBox1.SelectedIndex = 3;
                    return Data.ROMMapMode.SA1ROM;
                }
            }
            else if ((data[Data.LOROM_SETTING_OFFSET] & 0xEC) == 0x20)
            {
                if ((data[Data.LOROM_SETTING_OFFSET + 1] & 0xF0) == 0x10) {
                    detectMessage.Text = "ROM Map Mode Detected: SuperFX";
                    comboBox1.SelectedIndex = 5;
                    return Data.ROMMapMode.SuperFX;
                } else {
                    detectMessage.Text = "ROM Map Mode Detected: LoROM";
                    comboBox1.SelectedIndex = 0;
                    return Data.ROMMapMode.LoROM;
                }
            }
            else if (data.Length >= 0x10000 && (data[Data.HIROM_SETTING_OFFSET] & 0xEF) == 0x21)
            {
                detectMessage.Text = "ROM Map Mode Detected: HiROM";
                comboBox1.SelectedIndex = 1;
                return Data.ROMMapMode.HiROM;
            }
            else if (data.Length >= 0x10000 && (data[Data.HIROM_SETTING_OFFSET] & 0xE7) == 0x22)
            {
                detectMessage.Text = "ROM Map Mode Detected: Super MMC";
                comboBox1.SelectedIndex = 2;
                return Data.ROMMapMode.SuperMMC;
            }
            else if (data.Length >= 0x410000 && (data[Data.EXHIROM_SETTING_OFFSET] & 0xEF) == 0x25)
            {
                detectMessage.Text = "ROM Map Mode Detected: ExHiROM";
                comboBox1.SelectedIndex = 6;
                return Data.ROMMapMode.ExHiROM;
            }
            else
            {
                detectMessage.Text = "Couldn't auto detect ROM Map Mode!";
                if (data.Length > 0x40000)
                {
                    comboBox1.SelectedIndex = 7;
                    return Data.ROMMapMode.ExLoROM;
                } else
                {
                    comboBox1.SelectedIndex = 0;
                    return Data.ROMMapMode.LoROM;
                }
            }
        }

        public Data.ROMMapMode GetROMMapMode()
        {
            return mode;
        }

        public Data.ROMSpeed GetROMSpeed()
        {
            return speed;
        }

        private void UpdateOffsetAndSpeed()
        {
            offset = Data.Inst.GetRomSettingOffset(mode);
            if (offset >= data.Length)
            {
                speed = Data.ROMSpeed.Unknown;
                okay.Enabled = false;
            } else
            {
                okay.Enabled = true;
                speed = (data[offset] & 0x10) != 0 ? Data.ROMSpeed.FastROM : Data.ROMSpeed.SlowROM;
            }
        }

        private void UpdateTextboxes()
        {
            if (speed == Data.ROMSpeed.Unknown)
            {
                romspeed.Text = "????";
                romtitle.Text = "?????????????????????";
                for (int i = 0; i < vectors.GetLength(0); i++) for (int j = 0; j < vectors.GetLength(1); j++) vectors[i, j].Text = "????";
            } else
            {
                if (speed == Data.ROMSpeed.SlowROM) romspeed.Text = "SlowROM";
                else romspeed.Text = "FastROM";

                title = "";
                for (int i = 0; i < 0x15; i++) title += (char)data[offset - 0x15 + i];
                romtitle.Text = title;

                for (int i = 0; i < vectors.GetLength(0); i++)
                {
                    for (int j = 0; j < vectors.GetLength(1); j++)
                    {
                        int index = offset + 15 + 0x10 * i + 2 * j;
                        int val = data[index] + (data[index + 1] << 8);
                        vectors[i, j].Text = Util.NumberToBaseString(val, Util.NumberBase.Hexadecimal, 4);

                        if (val < 0x8000)
                        {
                            checkboxes[i, j].Checked = false;
                            checkboxes[i, j].Enabled = false;
                        } else
                        {
                            checkboxes[i, j].Enabled = true;
                        }
                    }
                }
            }
        }

        private void ImportROMDialog_Load(object sender, EventArgs e)
        {
            UpdateTextboxes();
        }

        private void okay_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (comboBox1.SelectedIndex)
            {
                case 0: mode = Data.ROMMapMode.LoROM; break;
                case 1: mode = Data.ROMMapMode.HiROM; break;
                case 2: mode = Data.ROMMapMode.SuperMMC; break;
                case 3: mode = Data.ROMMapMode.SA1ROM; break;
                case 4: mode = Data.ROMMapMode.ExSA1ROM; break;
                case 5: mode = Data.ROMMapMode.SuperFX; break;
                case 6: mode = Data.ROMMapMode.ExHiROM; break;
                case 7: mode = Data.ROMMapMode.ExLoROM; break;
            }
            UpdateOffsetAndSpeed();
            UpdateTextboxes();
        }
    }
}

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
        private Project.ImportRomSettings importSettings;
        private bool couldnt_detect_rom_type;

        private string title;
        private int offset;
        private string[,] vectorNames = new string[2, 6]
        {
            { "Native_COP", "Native_BRK", "Native_ABORT", "Native_NMI", "Native_RESET", "Native_IRQ"},
            { "Emulation_COP", "Emulation_Unknown", "Emulation_ABORT", "Emulation_NMI", "Emulation_RESET", "Emulation_IRQBRK"}
        };
        private TextBox[,] vectors;
        private CheckBox[,] checkboxes;

        public Project.ImportRomSettings? PromptForImportSettings(string filename)
        {
            importSettings = new Project.ImportRomSettings
            {
                rom_filename = filename,
                rom_bytes = Util.ReadAllRomBytesFromFile(filename),
                ROMMapMode = Util.DetectROMMapMode(importSettings.rom_bytes, out couldnt_detect_rom_type)
            };

            UpdateUIFromRomMapDetection();
            UpdateOffsetAndSpeed();

            if (ShowDialog() != DialogResult.OK)
                return null;

            importSettings.InitialLabels = GetGeneratedLabels();
            importSettings.InitialHeaderFlags = GetHeaderFlags();

            return importSettings;
        }

        public ImportROMDialog()
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
        }

        private Dictionary<int, Label> GetGeneratedLabels()
        {
            var labels = new Dictionary<int, Label>();
            
            for (int i = 0; i < checkboxes.GetLength(0); i++)
            {
                for (int j = 0; j < checkboxes.GetLength(1); j++)
                {
                    if (!checkboxes[i, j].Checked) 
                        continue;

                    int index = offset + 15 + 0x10 * i + 2 * j;
                    int val = importSettings.rom_bytes[index] + (importSettings.rom_bytes[index + 1] << 8);
                    int pc = Util.ConvertSNESToPC(val, importSettings.ROMMapMode, importSettings.rom_bytes.Length);
                    if (pc >= 0 && pc < importSettings.rom_bytes.Length && !labels.ContainsKey(val)) 
                        labels.Add(val, new Label() {name = vectorNames[i, j]});
                }
            }

            return labels;
        }

        private Dictionary<int, Data.FlagType> GetHeaderFlags()
        {
            var flags = new Dictionary<int, Data.FlagType>();

            if (checkHeader.Checked)
            {
                for (int i = 0; i < 0x15; i++) flags.Add(offset - 0x15 + i, Data.FlagType.Text);
                for (int i = 0; i < 7; i++) flags.Add(offset + i, Data.FlagType.Data8Bit);
                for (int i = 0; i < 4; i++) flags.Add(offset + 7 + i, Data.FlagType.Data16Bit);
                for (int i = 0; i < 0x20; i++) flags.Add(offset + 11 + i, Data.FlagType.Pointer16Bit);

                if (importSettings.rom_bytes[offset - 1] == 0)
                {
                    flags.Remove(offset - 1);
                    flags.Add(offset - 1, Data.FlagType.Data8Bit);
                    for (int i = 0; i < 0x10; i++) flags.Add(offset - 0x25 + i, Data.FlagType.Data8Bit);
                }
                else if (importSettings.rom_bytes[offset + 5] == 0x33)
                {
                    for (int i = 0; i < 6; i++) flags.Add(offset - 0x25 + i, Data.FlagType.Text);
                    for (int i = 0; i < 10; i++) flags.Add(offset - 0x1F + i, Data.FlagType.Data8Bit);
                }
            }

            return flags;
        }

        private void UpdateUIFromRomMapDetection()
        {
            if (couldnt_detect_rom_type)
                detectMessage.Text = "Couldn't auto detect ROM Map Mode!";
            else
                detectMessage.Text = "ROM Map Mode Detected: " + Util.GetRomMapModeName(importSettings.ROMMapMode);

            // TODO: there's definitely a better way. probably have the control read from a data table,
            // then have it update itself based on the value of importSettings.ROMMapMode.
            switch (importSettings.ROMMapMode)
            {
                case Data.ROMMapMode.LoROM:
                    comboBox1.SelectedIndex = 0;
                    break;
                case Data.ROMMapMode.HiROM:
                    comboBox1.SelectedIndex = 1;
                    break;
                case Data.ROMMapMode.ExHiROM:
                    comboBox1.SelectedIndex = 6;
                    break;
                case Data.ROMMapMode.SA1ROM:
                    comboBox1.SelectedIndex = 3;
                    break;
                case Data.ROMMapMode.ExSA1ROM:
                    comboBox1.SelectedIndex = 4;
                    break;
                case Data.ROMMapMode.SuperFX:
                    comboBox1.SelectedIndex = 5;
                    break;
                case Data.ROMMapMode.SuperMMC:
                    comboBox1.SelectedIndex = 2;
                    break;
                case Data.ROMMapMode.ExLoROM:
                    comboBox1.SelectedIndex = 7;
                    break;
                default:
                    break;
            }
        }

        private void UpdateOffsetAndSpeed()
        {
            offset = Util.GetRomSettingOffset(importSettings.ROMMapMode);
            if (offset >= importSettings.rom_bytes.Length)
            {
                importSettings.ROMSpeed = Data.ROMSpeed.Unknown;
                okay.Enabled = false;
            } else
            {
                okay.Enabled = true;
                importSettings.ROMSpeed = (importSettings.rom_bytes[offset] & 0x10) != 0 ? Data.ROMSpeed.FastROM : Data.ROMSpeed.SlowROM;
            }
        }

        private void UpdateTextboxes()
        {
            if (importSettings.ROMSpeed == Data.ROMSpeed.Unknown)
            {
                romspeed.Text = "????";
                romtitle.Text = "?????????????????????";
                for (int i = 0; i < vectors.GetLength(0); i++) for (int j = 0; j < vectors.GetLength(1); j++) vectors[i, j].Text = "????";
            } else
            {
                if (importSettings.ROMSpeed == Data.ROMSpeed.SlowROM) romspeed.Text = "SlowROM";
                else romspeed.Text = "FastROM";

                title = "";
                for (int i = 0; i < 0x15; i++) title += (char)importSettings.rom_bytes[offset - 0x15 + i];
                romtitle.Text = title;

                for (int i = 0; i < vectors.GetLength(0); i++)
                {
                    for (int j = 0; j < vectors.GetLength(1); j++)
                    {
                        int index = offset + 15 + 0x10 * i + 2 * j;
                        int val = importSettings.rom_bytes[index] + (importSettings.rom_bytes[index + 1] << 8);
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
            // TODO: there's definitely a better way, we'll get to it :)
            switch (comboBox1.SelectedIndex)
            {
                case 0: importSettings.ROMMapMode = Data.ROMMapMode.LoROM; break;
                case 1: importSettings.ROMMapMode = Data.ROMMapMode.HiROM; break;
                case 2: importSettings.ROMMapMode = Data.ROMMapMode.SuperMMC; break;
                case 3: importSettings.ROMMapMode = Data.ROMMapMode.SA1ROM; break;
                case 4: importSettings.ROMMapMode = Data.ROMMapMode.ExSA1ROM; break;
                case 5: importSettings.ROMMapMode = Data.ROMMapMode.SuperFX; break;
                case 6: importSettings.ROMMapMode = Data.ROMMapMode.ExHiROM; break;
                case 7: importSettings.ROMMapMode = Data.ROMMapMode.ExLoROM; break;
            }
            UpdateOffsetAndSpeed();
            UpdateTextboxes();
        }
    }
}

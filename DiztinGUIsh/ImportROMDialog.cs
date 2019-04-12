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

        public ImportROMDialog(byte [] rom)
        {
            InitializeComponent();
            data = rom;
            mode = DetectROMMapMode();
            UpdateOffsetAndSpeed();
        }

        private Data.ROMMapMode DetectROMMapMode()
        {
            if ((data[Data.LOROM_SETTING_OFFSET] & 0xEC) == 0x20)
            {
                detectMessage.Text = "ROM Map Mode Detected: LoROM";
                comboBox1.SelectedIndex = 0;
                return Data.ROMMapMode.LoROM;
            }
            else if (data.Length >= 0x10000 && (data[Data.HIROM_SETTING_OFFSET] & 0xE4) == 0x20)
            {
                detectMessage.Text = "ROM Map Mode Detected: HiROM";
                comboBox1.SelectedIndex = 1;
                return Data.ROMMapMode.HiROM;
            }
            else if (data.Length >= 0x410000 && (data[Data.EXHIROM_SETTING_OFFSET] & 0xEF) == 0x25)
            {
                detectMessage.Text = "ROM Map Mode Detected: ExHiROM";
                comboBox1.SelectedIndex = 2;
                return Data.ROMMapMode.ExHiROM;
            }
            else
            {
                detectMessage.Text = "Couldn't auto detect ROM Map Mode!";
                comboBox1.SelectedIndex = 0;
                return Data.ROMMapMode.LoROM;
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
            offset = mode == Data.ROMMapMode.LoROM ? Data.LOROM_SETTING_OFFSET : mode == Data.ROMMapMode.HiROM ? Data.HIROM_SETTING_OFFSET : Data.EXHIROM_SETTING_OFFSET;
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
            } else
            {
                if (speed == Data.ROMSpeed.SlowROM) romspeed.Text = "SlowROM";
                else romspeed.Text = "FastROM";

                title = "";
                for (int i = 0; i < 0x15; i++) title += (char)data[offset - 0x15 + i];
                romtitle.Text = title;
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
                case 2: mode = Data.ROMMapMode.ExHiROM; break;
            }
            UpdateOffsetAndSpeed();
            UpdateTextboxes();
        }
    }
}

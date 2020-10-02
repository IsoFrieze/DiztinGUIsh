using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using DiztinGUIsh.core.util;

namespace DiztinGUIsh.window.dialog
{
    public partial class ImportRomDialog : Form
    {
        public Project.ImportRomSettings ImportSettings { get; protected set; }
        public bool romTypeNotDetectedCorrectly = true;

        public class ImportRomViewModel
        {
            private readonly Project.ImportRomSettings settings;

            public int SelectedRomMappingIndex
            {
                get => (int)settings.ROMMapMode;
                set => settings.ROMMapMode = (Data.ROMMapMode)value;
            }

            public List<KeyValuePair<Data.ROMMapMode, string>> SelectedRomMappingList { get; }
                = Util.GetEnumDescriptions<Data.ROMMapMode>();

            public ImportRomViewModel(Project.ImportRomSettings settingsDataSource)
            {
                Debug.Assert(settingsDataSource != null);
                settings = settingsDataSource;
            }
        }


        public string title;
        public int RomSettingsOffset;
        private readonly string[,] vectorNames = {
            { "Native_COP", "Native_BRK", "Native_ABORT", "Native_NMI", "Native_RESET", "Native_IRQ"},
            { "Emulation_COP", "Emulation_Unknown", "Emulation_ABORT", "Emulation_NMI", "Emulation_RESET", "Emulation_IRQBRK"}
        };
        private readonly TextBox[,] vectors;
        private readonly CheckBox[,] checkboxes;

        public ImportRomDialog()
        {
            InitializeComponent();
            vectors = new[,]
            {
                { textNativeCOP, textNativeBRK, textNativeABORT, textNativeNMI, textNativeRESET, textNativeIRQ },
                { textEmuCOP, textEmuBRK, textEmuABORT, textEmuNMI, textEmuRESET, textEmuIRQ },
            };
            checkboxes = new[,]
            {
                { checkboxNativeCOP, checkboxNativeBRK, checkboxNativeABORT, checkboxNativeNMI, checkboxNativeRESET, checkboxNativeIRQ },
                { checkboxEmuCOP, checkboxEmuBRK, checkboxEmuABORT, checkboxEmuNMI, checkboxEmuRESET, checkboxEmuIRQ },
            };
        }

        public Project.ImportRomSettings PromptForImportSettings(string filename)
        {
            CreateRomImportSettingsFor(filename);

            DataBind();

            // UpdateUiFromRomMapDetection();
            UpdateOffsetAndSpeed();

            if (ShowDialog() != DialogResult.OK)
                return null;

            ImportSettings.InitialLabels = GetGeneratedLabels();
            ImportSettings.InitialHeaderFlags = GetHeaderFlags();

            return ImportSettings;
        }

        private void DataBind()
        {
            // importRomSettingsBindingSource.DataSource = ImportSettings;

            // specific to this combo. datasource is a static list of enum values
            var dataSource = Util.GetEnumDescriptions<Data.ROMMapMode>();
            var bs = new BindingSource(dataSource, null);
            bs.CurrentChanged += Bs_CurrentChanged;

            cmbRomMapMode.DataSource = bs;
            cmbRomMapMode.ValueMember = "Key";         // names of properties of each item on datasource.
            cmbRomMapMode.DisplayMember = "Value";

            // bind comboboxes "SelectedIndex" property to store its value in settings.ROMMapMode
            cmbRomMapMode.DataBindings.Add(new Binding(
                "SelectedIndex", ImportViewModel,
                "SelectedRomMappingIndex", false, 
                DataSourceUpdateMode.OnPropertyChanged));

            
        }

        private void Bs_CurrentChanged(object sender, EventArgs e)
        {
            UpdateUI();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            
        }

        private void UpdateUI()
        {
            UpdateOffsetAndSpeed();
            UpdateTextboxes();
        }

        void UpdateFromDatabinding(IBindableComponent sender)
        {
            // ValidateChildren();
            //Validate(true);

            // this seems to work the best to get the values flushed out in realtime
            /*foreach (var i in sender.DataBindings)
            {
                var q = i as Binding;
                // flushes the cached value back out to the real data structure
                q.WriteValue();
            }*/
        }

        private void importRomSettingsBindingSource_CurrentChanged(object sender, EventArgs e)
        {
            int x = 3;
            // UpdateUI();
        }

        private void comboBox1_DropDownClosed(object sender, EventArgs e)
        {
            int x = 3;
        }

        private void comboBox1_Validating(object sender, System.ComponentModel.CancelEventArgs e)
        {
            int x = 3;
        }

        private void comboBox1_TextChanged(object sender, EventArgs e)
        { 
            //Validate(true);
            // UpdateFromDatabinding(sender as IBindableComponent);
        }

        ImportRomViewModel ImportViewModel;

        private void CreateRomImportSettingsFor(string filename)
        {
            var romBytes = RomUtil.ReadAllRomBytesFromFile(filename);
            ImportSettings = new Project.ImportRomSettings
            {
                RomFilename = filename,
                RomBytes = romBytes,
                ROMMapMode = RomUtil.DetectROMMapMode(romBytes, out romTypeNotDetectedCorrectly)
            };

            ImportViewModel = new ImportRomViewModel(ImportSettings);
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

                    int index = RomSettingsOffset + 15 + 0x10 * i + 2 * j;
                    int val = ImportSettings.RomBytes[index] + (ImportSettings.RomBytes[index + 1] << 8);
                    int pc = RomUtil.ConvertSNESToPC(val, ImportSettings.ROMMapMode, ImportSettings.RomBytes.Length);
                    if (pc >= 0 && pc < ImportSettings.RomBytes.Length && !labels.ContainsKey(val)) 
                        labels.Add(val, new Label() {name = vectorNames[i, j]});
                }
            }

            return labels;
        }

        private Dictionary<int, Data.FlagType> GetHeaderFlags()
        {
            var flags = new Dictionary<int, Data.FlagType>();

            if (checkHeader.Checked) 
                RomUtil.GetHeaderFlags(RomSettingsOffset, flags, ImportSettings.RomBytes);

            return flags;
        }

        private void UpdateOffsetAndSpeed()
        {
            RomSettingsOffset = RomUtil.GetRomSettingOffset(ImportSettings.ROMMapMode);
            var romSpeed = ImportSettings.ROMSpeed;
            var romBytes = ImportSettings.RomBytes;

            romSpeed = RomUtil.GetRomSpeed(RomSettingsOffset, romBytes);

            okay.Enabled = ImportSettings.ROMSpeed != Data.ROMSpeed.Unknown;
        }

        private bool IsOffsetInRange(int offset, int count = 0)
        {
            return offset > 0 && offset <= ImportSettings.RomBytes.Length;
        }
        
        private void UpdateTextboxes()
        {
            if (IsProbablyValidDetection())
            {
                try {
                    UpdateDetectedValues();
                    return;
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            SetDefaultsIfDetectionFailed();
        }

        private bool IsProbablyValidDetection()
        {
            return 
                ImportSettings.ROMSpeed != Data.ROMSpeed.Unknown && 
                IsOffsetInRange(RomSettingsOffset);
        }

        private void SetDefaultsIfDetectionFailed()
        {
            romspeed.Text = "????";
            romtitle.Text = "?????????????????????";
            for (int i = 0; i < vectors.GetLength(0); i++)
            for (int j = 0; j < vectors.GetLength(1); j++)
                vectors[i, j].Text = "????";
            return;
        }

        private void UpdateDetectedValues()
        {
            // caution: things can go wrong here if we didn't guess settings correctly,
            // you usually want to call this function with a try/catch around it.

            var romspeedStr = ImportSettings.ROMSpeed == Data.ROMSpeed.SlowROM ? "SlowROM" : "FastROM";
            var romTitleName = RomUtil.GetRomTitleName(ImportSettings.RomBytes, RomSettingsOffset);

            for (int i = 0; i < vectors.GetLength(0); i++)
            {
                for (int j = 0; j < vectors.GetLength(1); j++)
                {
                    int index = RomSettingsOffset + 15 + 0x10 * i + 2 * j;
                    int val = ImportSettings.RomBytes[index] + (ImportSettings.RomBytes[index + 1] << 8);
                    vectors[i, j].Text = Util.NumberToBaseString(val, Util.NumberBase.Hexadecimal, 4);

                    if (val < 0x8000)
                    {
                        checkboxes[i, j].Checked = false;
                        checkboxes[i, j].Enabled = false;
                    }
                    else
                    {
                        checkboxes[i, j].Enabled = true;
                    }
                }
            }

            romspeed.Text = romspeedStr;
            romtitle.Text = romTitleName;
        }

        private void ImportROMDialog_Load(object sender, EventArgs e)
        {
            UpdateTextboxes();
        }

        private void okay_Click(object sender, EventArgs e) { DialogResult = DialogResult.OK; }

        private void cancel_Click(object sender, EventArgs e) { Close(); }


        /*private Data.ROMMapMode SelectRomMapModeFromUi(int selectedIndex)
        {
            // TODO: there's definitely a better way. Databinding, or use a dict at worst.
            var mode = selectedIndex switch
            {
                0 => Data.ROMMapMode.LoROM,
                1 => Data.ROMMapMode.HiROM,
                2 => Data.ROMMapMode.SuperMMC,
                3 => Data.ROMMapMode.SA1ROM,
                4 => Data.ROMMapMode.ExSA1ROM,
                5 => Data.ROMMapMode.SuperFX,
                6 => Data.ROMMapMode.ExHiROM,
                7 => Data.ROMMapMode.ExLoROM,
                _ => ImportSettings.ROMMapMode
            };
            return mode;
        }

        private void UpdateUiFromRomMapDetection()
        {
            if (romTypeNotDetectedCorrectly)
                detectMessage.Text = "Couldn't auto detect ROM Map Mode!";
            else
                detectMessage.Text = "ROM Map Mode Detected: " + RomUtil.GetRomMapModeName(ImportSettings.ROMMapMode);

            // TODO: there's definitely a better way. probably have the control read from a data table,
            // then have it update itself based on the value of importSettings.ROMMapMode.
            switch (ImportSettings.ROMMapMode)
            {
                case Data.ROMMapMode.LoROM:
                    comboBox1.SelectedIndex = 0;
                    break;
                case Data.ROMMapMode.HiROM:
                    comboBox1.SelectedIndex = 1;
                    break;
                case Data.ROMMapMode.SuperMMC:
                    comboBox1.SelectedIndex = 2;
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
                case Data.ROMMapMode.ExHiROM:
                    comboBox1.SelectedIndex = 6;
                    break;
                case Data.ROMMapMode.ExLoROM:
                    comboBox1.SelectedIndex = 7;
                    break;
            }
        }*/
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using DiztinGUIsh.core;
using DiztinGUIsh.core.util;
using DiztinGUIsh.loadsave;

namespace DiztinGUIsh.window.dialog
{
    public partial class ImportRomDialog : Form
    {
        // NOTE: all this could be converted to use databinding and be easier to deal with, but,
        // probably more work than its worth. this is fine, if a bit manual. it's unlikely to ever need to change.
        private CheckBox[,] GetVectorsCheckboxes() => new[,]
        {
            {checkboxNativeCOP, checkboxNativeBRK, checkboxNativeABORT, checkboxNativeNMI, checkboxNativeRESET, checkboxNativeIRQ},
            {checkboxEmuCOP, checkboxEmuBRK, checkboxEmuABORT, checkboxEmuNMI, checkboxEmuRESET, checkboxEmuIRQ},
        };
        private TextBox[,] GetVectorsTextBoxes() => new[,]
        {
            {textNativeCOP, textNativeBRK, textNativeABORT, textNativeNMI, textNativeRESET, textNativeIRQ},
            {textEmuCOP, textEmuBRK, textEmuABORT, textEmuNMI, textEmuRESET, textEmuIRQ},
        };
        private readonly string[,] vectorNames = {
            {"Native_COP", "Native_BRK", "Native_ABORT", "Native_NMI", "Native_RESET", "Native_IRQ"},
            {"Emulation_COP", "Emulation_Unknown", "Emulation_ABORT", "Emulation_NMI", "Emulation_RESET", "Emulation_IRQBRK"}
        };
        private readonly TextBox[,] vectors;
        private readonly CheckBox[,] checkboxes;

        public ImportRomSettings ImportSettings { get; protected set; }
        private int RomSettingsOffset = -1;
        private Data.ROMMapMode? DetectedMapMode = null;

        public ImportRomDialog()
        {
            InitializeComponent();
            vectors = GetVectorsTextBoxes();
            checkboxes = GetVectorsCheckboxes();
        }

        private void DataBind()
        {
            Debug.Assert(ImportSettings != null);
            GuiUtil.BindListControlToEnum<Data.ROMMapMode>(cmbRomMapMode, ImportSettings, "ROMMapMode");
            ImportSettings.PropertyChanged += ImportSettingsOnPropertyChanged;
        }

        public ImportRomSettings PromptForImportSettings(string filename)
        {
            CreateNewRomImportSettingsFor(filename);
            DataBind();
            UpdateUI();

            if (ShowDialog() != DialogResult.OK)
                return null;

            ImportSettings.PropertyChanged -= ImportSettingsOnPropertyChanged;
            ImportSettings.InitialLabels = GenerateVectorLabels();
            ImportSettings.InitialHeaderFlags = GenerateHeaderFlags();

            return ImportSettings;
        }

        private void ImportSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            UpdateUI();
        }

        private void CreateNewRomImportSettingsFor(string filename)
        {
            var romBytes = RomUtil.ReadAllRomBytesFromFile(filename);
            ImportSettings = new ImportRomSettings
            {
                RomFilename = filename,
                RomBytes = romBytes,
                ROMMapMode = RomUtil.DetectROMMapMode(romBytes, out var detectedMapModeSuccess)
            };

            if (detectedMapModeSuccess)
            {
                DetectedMapMode = ImportSettings.ROMMapMode;
                detectMessage.Text = Util.GetEnumDescription(ImportSettings.ROMMapMode);
            }
            else
            {
                detectMessage.Text = "Couldn't auto detect ROM Map Mode!";
            }
        }

        private void UpdateUI()
        {
            UpdateOffsetAndSpeed();
            UpdateTextboxes();
        }

        private Dictionary<int, Label> GenerateVectorLabels()
        {
            // TODO: bounds check that generated addresses are inside the rom.

            var labels = new Dictionary<int, Label>();

            for (int i = 0; i < checkboxes.GetLength(0); i++)
            {
                for (int j = 0; j < checkboxes.GetLength(1); j++)
                {
                    if (!checkboxes[i, j].Checked)
                        continue;

                    int index = RomSettingsOffset + 15 + 0x10 * i + 2 * j;
                    int offset = ImportSettings.RomBytes[index] + (ImportSettings.RomBytes[index + 1] << 8);
                    int pc = RomUtil.ConvertSNESToPC(offset, ImportSettings.ROMMapMode, ImportSettings.RomBytes.Length);
                    if (pc >= 0 && pc < ImportSettings.RomBytes.Length && !labels.ContainsKey(offset))
                        labels.Add(offset, new Label() {name = vectorNames[i, j]});
                }
            }

            return labels;
        }

        private Dictionary<int, Data.FlagType> GenerateHeaderFlags()
        {
            var flags = new Dictionary<int, Data.FlagType>();

            if (checkHeader.Checked)
                RomUtil.GenerateHeaderFlags(RomSettingsOffset, flags, ImportSettings.RomBytes);

            return flags;
        }

        private void UpdateOffsetAndSpeed()
        {
            RomSettingsOffset = RomUtil.GetRomSettingOffset(ImportSettings.ROMMapMode);
            ImportSettings.ROMSpeed = RomUtil.GetRomSpeed(RomSettingsOffset, ImportSettings.RomBytes);

            UpdateOkayButtonEnabled();
        }

        private void UpdateOkayButtonEnabled()
        {
            okay.Enabled = ImportSettings.ROMSpeed != Data.ROMSpeed.Unknown;
        }

        private void UpdateTextboxes()
        {
            if (IsProbablyValidDetection())
            {
                try
                {
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

        private bool IsOffsetInRange(int offset, int count = 0) =>
            offset > 0 && offset <= ImportSettings.RomBytes.Length;

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
            for (var i = 0; i < vectors.GetLength(0); i++)
            for (var j = 0; j < vectors.GetLength(1); j++)
                vectors[i, j].Text = "????";
        }

        private void UpdateDetectedValues()
        {
            // caution: things can go wrong here if we didn't guess settings correctly,
            // you usually want to call this function with a try/catch around it.
            for (var i = 0; i < vectors.GetLength(0); i++)
            {
                for (var j = 0; j < vectors.GetLength(1); j++)
                {
                    var index = RomSettingsOffset + 15 + 0x10 * i + 2 * j;
                    var val = ImportSettings.RomBytes[index] + (ImportSettings.RomBytes[index + 1] << 8);
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
            var romSpeedStr = Util.GetEnumDescription(ImportSettings.ROMSpeed);
            var romTitleName = RomUtil.GetRomTitleName(ImportSettings.RomBytes, RomSettingsOffset);

            romspeed.Text = romSpeedStr;
            romtitle.Text = romTitleName;
        }

        private void ImportROMDialog_Load(object sender, EventArgs e) => UpdateUI();

        private void okay_Click(object sender, EventArgs e)
        {
            static bool Warn(string msg)
            {
                return PromptToConfirmAction(msg +
                                             "\nIf you proceed with this import, settings might be wrong.\n" +
                                             "Proceed anyway?\n\n (Experts only, otherwise say No here and fix import settings)");
            }

            if (!DetectedMapMode.HasValue)
            {
                if (!Warn("ROM Map type couldn't be detected."))
                    return;
            } else if (DetectedMapMode.Value != ImportSettings.ROMMapMode) {
                if (!Warn("The ROM map type selected is different than what was detected."))
                    return;
            }

            SetFinished();
        }

        private static bool PromptToConfirmAction(string msg)
        {
            return GuiUtil.PromptToConfirmAction("Warning", msg, () => true);
        }

        private void SetFinished()
        {
            DialogResult = DialogResult.OK;
        }

        private void cancel_Click(object sender, EventArgs e) => Close();
    }
}
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.serialization;
using Diz.Core.util;

namespace DiztinGUIsh.window.dialog
{
    public partial class ImportRomDialog : Form, IImportRomDialogView
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
        private readonly TextBox[,] vectors;
        private readonly CheckBox[,] checkboxes;

        private ImportRomDialogController controller;
        public ImportRomDialogController Controller
        {
            get => controller;
            set
            {
                if (controller != null)
                    controller.SettingsCreated -= Controller_SettingsCreated;

                controller = value;
                
                if (controller != null)
                    controller.SettingsCreated += Controller_SettingsCreated;
            }
        }
        public ImportRomSettings ImportSettings => Controller?.ImportSettings;

        public ImportRomDialog()
        {
            InitializeComponent();

            vectors = GetVectorsTextBoxes();
            checkboxes = GetVectorsCheckboxes();
        }

        private void DataBind()
        {
            Debug.Assert(ImportSettings != null);
            GuiUtil.BindListControlToEnum<RomMapMode>(cmbRomMapMode, ImportSettings, "ROMMapMode");

            checkHeader.Checked = Controller.ShouldCheckHeader; // todo: databind this instead.
            ImportSettings.PropertyChanged += ImportSettingsOnPropertyChanged;
        }

        public bool ShowAndWaitForUserToConfirmSettings()
        {
            return ShowDialog() == DialogResult.OK;
        }

        private void Controller_SettingsCreated()
        {
            DataBind();
            RefreshUi();
        }

        private void ImportSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            RefreshUi();
        }

        private void RefreshUi()
        {
            UpdateOffsetAndSpeed();
            UpdateTextboxes();
            UpdateOkayButtonEnabled();
            detectMessage.Text = GetDetectionMessage();
            UpdateVectorTable();
        }

        private string GetDetectionMessage() => 
            Controller.DetectedMapMode.HasValue ? Util.GetEnumDescription(ImportSettings.RomMapMode) : "Couldn't auto detect ROM Map Mode!";

        public void UpdateVectorTable()
        {
            // it'd probably be easier to just setup a lookup table for this stuff
            // it's probably also better to databind all this, but might be complex. this is OK for now.

            string[,] vectorNames = {
                {"Native_COP", "Native_BRK", "Native_ABORT", "Native_NMI", "Native_RESET", "Native_IRQ"},
                {"Emulation_COP", "Emulation_Unknown", "Emulation_ABORT", "Emulation_NMI", "Emulation_RESET", "Emulation_IRQBRK"}
            };

            Controller.VectorTableEntriesEnabled.Clear();
            Debug.Assert(checkboxes.GetLength(0) == vectorNames.GetLength(0));
            Debug.Assert(checkboxes.GetLength(1) == vectorNames.GetLength(1));
            for (var i = 0; i < checkboxes.GetLength(0); i++)
            {
                for (var j = 0; j < checkboxes.GetLength(1); j++)
                {
                    Controller.VectorTableEntriesEnabled.Add(vectorNames[i, j], checkboxes[i,j].Checked);
                }
            }
        }

        private void UpdateOffsetAndSpeed()
        {
            Controller.RomSettingsOffset = RomUtil.GetRomSettingOffset(ImportSettings.RomMapMode);
            ImportSettings.RomSpeed = RomUtil.GetRomSpeed(Controller.RomSettingsOffset, ImportSettings.RomBytes);
        }

        private void UpdateOkayButtonEnabled()
        {
            okay.Enabled = ImportSettings.RomSpeed != RomSpeed.Unknown;
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

        private bool IsOffsetInRange(int offset) =>
            offset > 0 && offset <= ImportSettings.RomBytes.Length;

        private bool IsProbablyValidDetection() => 
            ImportSettings.RomSpeed != RomSpeed.Unknown && IsOffsetInRange(Controller.RomSettingsOffset);

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
                    var index = Controller.RomSettingsOffset + 15 + 0x10 * i + 2 * j;
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
            var romSpeedStr = Util.GetEnumDescription(ImportSettings.RomSpeed);
            var romTitleName = RomUtil.GetRomTitleName(ImportSettings.RomBytes, Controller.RomSettingsOffset);

            romspeed.Text = romSpeedStr;
            romtitle.Text = romTitleName;
        }

        private void ImportROMDialog_Load(object sender, EventArgs e) => RefreshUi();

        private void okay_Click(object sender, EventArgs e)
        {
            if (!Controller.Submit())
                return;

            SetFinished();
        }

        public bool PromptToConfirmAction(string msg)
        {
            return GuiUtil.PromptToConfirmAction("Warning", msg, () => true);
        }

        private void SetFinished()
        {
            DialogResult = DialogResult.OK;
        }

        private void cancel_Click(object sender, EventArgs e) => Close();

        private void checkHeader_CheckedChanged(object sender, EventArgs e)
        {
            Controller.ShouldCheckHeader = checkHeader.Checked; // todo: databind this instead.
        }

        private void ImportRomDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (ImportSettings != null)
                ImportSettings.PropertyChanged -= ImportSettingsOnPropertyChanged;

            Controller = null;
        }
    }
}
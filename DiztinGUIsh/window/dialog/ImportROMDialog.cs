using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core.serialization;
using Diz.Core.util;
using DiztinGUIsh.util;

namespace DiztinGUIsh.window.dialog;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
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

    private IImportRomDialogController controller;
    public IImportRomDialogController Controller
    {
        get => controller;
        set
        {
            if (controller != null)
                controller.OnBuilderInitialized -= ControllerOnBuilderInitialized;

            controller = value;
                
            if (controller != null)
                controller.OnBuilderInitialized += ControllerOnBuilderInitialized;
        }
    }

    private ImportRomSettings ImportSettings => Controller?.Builder.ImportSettings;

    public ImportRomDialog()
    {
        InitializeComponent();

        vectors = GetVectorsTextBoxes();
        checkboxes = GetVectorsCheckboxes();
            
        // it'd probably be easier to just setup a lookup table for these checkbox values
        // it's probably also better to databind all this, but might be complex. this works OK for now, but it's fragile
        // if the GUi or underlying data changes.
        // TODO: make it stopppp :)
        Debug.Assert(checkboxes.GetLength(0) == ImportRomSettingsBuilder.VectorNames.GetLength(0));
        Debug.Assert(checkboxes.GetLength(1) == ImportRomSettingsBuilder.VectorNames.GetLength(1));
    }

    private void DataBind()
    {
        Debug.Assert(ImportSettings != null);
        GuiUtil.BindListControlToEnum<RomMapMode>(cmbRomMapMode, ImportSettings, "ROMMapMode");

        checkHeader.Checked = Controller.Builder.ShouldCheckHeader; // todo: databind this instead.
    }

    public bool ShowAndWaitForUserToConfirmSettings()
    {
        return ShowDialog() == DialogResult.OK;
    }

    private void ControllerOnBuilderInitialized()
    {
        DataBind();
        RefreshUi();
    }

    public void ImportSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e) => 
        RefreshUi();

    public void RefreshUi()
    {
        UpdateTextboxes();
        UpdateOkayButtonEnabled();
        detectMessage.Text = GetDetectionMessage();
    }

    private string GetDetectionMessage() => 
        Controller.Builder.DetectedMapMode.HasValue ? Util.GetEnumDescription(ImportSettings.RomMapMode) : "Couldn't auto detect ROM Map Mode!";

    public bool GetVectorValue(int i, int j) => checkboxes[i,j].Checked;

    private void UpdateOkayButtonEnabled() => okay.Enabled = ImportSettings.RomSpeed != RomSpeed.Unknown;

    private void UpdateTextboxes()
    {
        if (UpdateTextboxesIfDetectionWorked()) 
            return;

        SetDefaultsIfDetectionFailed();
    }

    private bool UpdateTextboxesIfDetectionWorked()
    {
        if (!IsProbablyValidDetection()) 
            return false;
            
        try
        {
            UpdateUiFromDetectedValues();
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }

    private bool IsOffsetInRange(int offset) =>
        offset > 0 && offset <= ImportSettings.RomBytes.Length;

    private bool IsProbablyValidDetection() => 
        ImportSettings.RomSpeed != RomSpeed.Unknown && IsOffsetInRange(ImportSettings.RomSettingsOffset);

    private void SetDefaultsIfDetectionFailed()
    {
        romspeed.Text = "????";
        romtitle.Text = "?????????????????????";
        for (var i = 0; i < vectors.GetLength(0); i++)
        for (var j = 0; j < vectors.GetLength(1); j++)
            vectors[i, j].Text = "????";
    }

    private void UpdateUiFromDetectedValues()
    {
        // caution: things can go wrong here if we didn't guess settings correctly,
        // you usually want to call this function with a try/catch around it.
        for (var i = 0; i < vectors.GetLength(0); i++)
        {
            for (var j = 0; j < vectors.GetLength(1); j++)
            {
                var index = ImportSettings.RomSettingsOffset + 15 + 0x10 * i + 2 * j;
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
        var romTitleName = RomUtil.GetCartridgeTitleFromRom(ImportSettings.RomBytes, ImportSettings.RomSettingsOffset);

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

    private void SetFinished() => DialogResult = DialogResult.OK;

    private void cancel_Click(object sender, EventArgs e) => Close();

    // todo: databind this instead.
    private void checkHeader_CheckedChanged(object sender, EventArgs e) => 
        Controller.Builder.ShouldCheckHeader = checkHeader.Checked;

    private void ImportRomDialog_FormClosing(object sender, FormClosingEventArgs e) => Controller = null;
}
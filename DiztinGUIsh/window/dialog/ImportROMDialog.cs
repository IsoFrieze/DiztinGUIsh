using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core.model;
using Diz.Core.serialization;
using Diz.Core.util;
using Diz.Cpu._65816.import;
using Diz.Ui.Winforms.util;
using DiztinGUIsh.util;
using JetBrains.Annotations;

namespace DiztinGUIsh.window.dialog;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public partial class ImportRomDialog : Form, IImportRomDialogView
{
    public record VectorControls(string Name, CheckBox Check, TextBox Text);

    // NOTE: all this could be converted to use databinding and be easier to deal with, but,
    // probably more work than its worth. this is fine, if a bit manual. it's unlikely to ever need to change.
    private List<VectorControls> GetVectorGuiMappings()
    {
        return new List<VectorControls>
        {
            new(SnesVectorNames.Native_COP, checkboxNativeCOP, textNativeCOP),
            new(SnesVectorNames.Native_BRK, checkboxNativeBRK, textNativeBRK),
            new(SnesVectorNames.Native_ABORT, checkboxNativeABORT, textNativeABORT),
            new(SnesVectorNames.Native_NMI,checkboxNativeNMI, textNativeNMI),
            new(SnesVectorNames.Native_RESET,checkboxNativeRESET, textNativeRESET),
            new(SnesVectorNames.Native_IRQ, checkboxNativeIRQ, textNativeIRQ),
            new(SnesVectorNames.Emulation_COP, checkboxEmuCOP, textEmuCOP),
            new(SnesVectorNames.Emulation_Unknown, checkboxEmuBRK, textEmuBRK),
            new(SnesVectorNames.Emulation_ABORT, checkboxEmuABORT, textEmuABORT),
            new(SnesVectorNames.Emulation_NMI, checkboxEmuNMI, textEmuNMI),
            new(SnesVectorNames.Emulation_RESET, checkboxEmuRESET, textEmuRESET),
            new(SnesVectorNames.Emulation_IRQBRK, checkboxEmuIRQ, textEmuIRQ)
        };
    }

    private IImportRomDialogController controller;
    private readonly List<VectorControls> vectorTableGui;

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

    public ImportRomDialog()
    {
        InitializeComponent();
        
        vectorTableGui = GetVectorGuiMappings();
        foreach (var vector in vectorTableGui)
        {
            vector.Check.CheckedChanged += OnVectorCheckboxCheckedChanged;
            vector.Check.Tag = vector;
        }
    }

    private void OnVectorCheckboxCheckedChanged([CanBeNull] object sender, EventArgs e)
    {
        if (sender is not CheckBox checkbox)
            return;
        
        var vector = checkbox.Tag as VectorControls;
        Debug.Assert(vector != null);
        if (vector == null)
            return;
        
        Controller.Builder.OptionSetGenerateVectorTableLabelFor(vector.Name, vector.Check.Checked);
    }

    private void DataBind()
    {
        // Debug.Assert(Controller.Builder.Input.AnalysisResults != null); // needed?
        
        // this is the better way to do this but... we need better hooks for knowing when stuff changes, it's a mess
        GuiUtil.BindListControlToEnum<RomMapMode>(cmbRomMapMode, 
            Controller.Builder, 
            nameof(ISnesRomImportSettingsBuilder.OptionSelectedRomMapMode));
        
        checkHeader.Checked = Controller.Builder.OptionGenerateHeaderFlags;
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

    public void RefreshUi()
    {
        if (Controller == null)
            return;
        
        UpdateTextboxes();
        UpdateOkayButtonEnabled();
        detectMessage.Text = Controller.GetDetectionMessage();
    }

    private void UpdateOkayButtonEnabled() => 
        okay.Enabled = (Controller?.Builder?.Input?.AnalysisResults?.RomSpeed ?? RomSpeed.Unknown) != RomSpeed.Unknown;

    private void UpdateTextboxes()
    {
        if (Controller.IsProbablyValidDetection())
        {
            try
            {
                UpdateUiFromDetectedValues();
                return;
            }
            catch (Exception)
            {
                // fall through
            }
        }

        SetDefaultsIfDetectionFailed();
    }

    private void SetDefaultsIfDetectionFailed()
    {
        romspeed.Text = "????";
        romtitle.Text = "?????????????????????";
        
        foreach (var (_, checkBox, textBox) in vectorTableGui)
        {
            checkBox.Checked = true;
            textBox.Text = "????";
        }
    }

    public List<string> EnabledVectorTableEntries 
    {
        get 
        {
            var enabledVectors = new List<string>();

            foreach (var (vectorName, checkBox, _) in vectorTableGui)
            {
                if (checkBox.Checked)
                    enabledVectors.Add(vectorName);
            }

            return enabledVectors;
        }
    }

    // caution: things can go wrong here if we didn't guess settings correctly,
    // you usually want to call this function with a try/catch around it.
    private void UpdateUiFromDetectedValues()
    {
        SyncGuiVectorTableEntriesFromController();
        romspeed.Text = Controller.RomSpeedText;
        romtitle.Text = Controller.CartridgeTitle;
    }

    private void SyncGuiVectorTableEntriesFromController()
    {
        var i = 0;
        foreach (var (_, checkBox, textBox) in vectorTableGui)
        {
            Debug.Assert(i is >= 0 and < 12);
            var whichTable = i / 6;
            var whichEntry = i % 6;
            var vectorValue = Controller.GetVectorTableValue(whichTable, whichEntry);
            SetGuiForVectorEntry(vectorValue, textBox, checkBox);
            ++i;
        }
    }

    private static void SetGuiForVectorEntry(int vectorValue, Control textBox, CheckBox checkBox)
    {
        textBox.Text = Util.NumberToBaseString(vectorValue, Util.NumberBase.Hexadecimal, 4);

        var enabled = vectorValue >= 0x8000;
        checkBox.Enabled = enabled;
        if (!enabled)
            checkBox.Checked = false;
    }

    private void ImportROMDialog_Load(object sender, EventArgs e) => 
        RefreshUi();

    private void okay_Click(object sender, EventArgs e)
    {
        if (!Controller.Submit())
            return;

        SetFinished();
    }

    private void SetFinished() => 
        DialogResult = DialogResult.OK;

    private void cancel_Click(object sender, EventArgs e) => 
        Close();

    // todo: databind this instead.
    private void checkHeader_CheckedChanged(object sender, EventArgs e) => 
        Controller.Builder.OptionGenerateHeaderFlags = checkHeader.Checked;

    private void ImportRomDialog_FormClosing(object sender, FormClosingEventArgs e) => 
        Controller = null;
}
#nullable enable
using System;
using System.ComponentModel;
using System.Windows.Forms;
using Diz.Controllers.interfaces;
using Diz.Core.export;
using Diz.LogWriter;
using Diz.Ui.Winforms.util;
using DiztinGUIsh.util;

namespace DiztinGUIsh.window.dialog;

public partial class LogCreatorSettingsEditorForm : Form, ILogCreatorSettingsEditorView
{
    private ILogCreatorSettingsEditorController? controller;

    public LogWriterSettings Settings
    {
        get => Controller?.Settings ?? new LogWriterSettings();
        private set { if (Controller != null) Controller.Settings = value; }
    }

    public ILogCreatorSettingsEditorController? Controller
    {
        get => controller;
        set
        {
            if (controller != null)
            {
                controller.PropertyChanged -= ControllerOnPropertyChanged;
                controller.View = null;
            }

            controller = value;

            if (controller == null) 
                return;
            
            controller.PropertyChanged += ControllerOnPropertyChanged;
            controller.View = this;
        }
    }

    public LogCreatorSettingsEditorForm()
    {
        InitializeComponent();
    }

    // TODO: in the future, replace this with databinding so we don't have to do it manually
    private void RefreshUi()
    {
        textFormat.Text = Settings.Format;
        numData.Value = Settings.DataPerLine;
        comboUnlabeled.SelectedIndex = (int)Settings.Unlabeled;
        comboStructure.SelectedIndex = (int)Settings.Structure;
        chkIncludeUnusedLabels.Checked = Settings.IncludeUnusedLabels;
        chkPrintLabelSpecificComments.Checked = Settings.PrintLabelSpecificComments;
        txtExportPath.Text = Settings.FileOrFolderOutPath;
        
        var validFormat = LogCreatorLineFormatter.Validate(Settings.Format);
        
        disassembleButton.Enabled = validFormat;

        textSample.Text = validFormat ? Controller?.GetSampleOutput() : "Invalid format!";
    }

    private void ControllerOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ILogCreatorSettingsEditorController.Settings))
        {
            RefreshUi();
        }
    }
    
    public bool PromptCreatePath(string path, string extraMsg = "") => 
        GuiUtil.PromptToConfirmAction("Output Directory", 
            $"Output Directory does not exist.\nWould you like to create it now?\n{path}\n\n{extraMsg}",
            () => true);

    public bool PromptEditAndConfirmSettings() => 
        ShowDialog() == DialogResult.OK;

    public string? PromptForLogPathFromFileOrFolderDialog(bool askForFile)
    {
        var outputPath = Settings.BuildFullOutputPath();
        
        return askForFile
            ? saveLogSingleFile.PromptSaveFileDialog(outputPath)
            : chooseLogFolder.PromptSelectFolder(outputPath);
    }
    
    private void disassembleButton_Click(object sender, EventArgs e)
    {
        if (!Controller?.EnsureSelectRealOutputDirectory() ?? false)
            return;

        DialogResult = DialogResult.OK;
    }
    
    private void btnBrowseOutputPath_Click(object sender, EventArgs e) => 
        Controller?.EnsureSelectRealOutputDirectory(true);

    private LogWriterSettings.FormatUnlabeled UnlabeledFormat => 
        (LogWriterSettings.FormatUnlabeled)comboUnlabeled.SelectedIndex;
    
    private LogWriterSettings.FormatStructure StructureFormat => 
        (LogWriterSettings.FormatStructure)comboStructure.SelectedIndex;
    
    private void cancel_Click(object sender, EventArgs e) => 
        Close();
    
    private void textFormat_TextChanged(object sender, EventArgs e) => 
        Settings = Settings with {Format = textFormat.Text.ToLower()};

    private void numData_ValueChanged(object sender, EventArgs e) => 
        Settings = Settings with { DataPerLine = (int)numData.Value };

    private void comboUnlabeled_SelectedIndexChanged(object sender, EventArgs e) => 
        Settings = Settings with {Unlabeled = UnlabeledFormat};

    private void comboStructure_SelectedIndexChanged(object sender, EventArgs e) =>
        Settings = Settings with {Structure = StructureFormat};
        
    private void chkPrintLabelSpecificComments_CheckedChanged(object sender, EventArgs e) => 
        Settings = Settings with {PrintLabelSpecificComments = chkPrintLabelSpecificComments.Checked};

    private void chkIncludeUnusedLabels_CheckedChanged(object sender, EventArgs e) => 
        Settings = Settings with {IncludeUnusedLabels = chkIncludeUnusedLabels.Checked};
    
    private void txtExportPath_TextChanged(object sender, EventArgs e) => 
        Settings = Settings with {FileOrFolderOutPath = txtExportPath.Text};
}
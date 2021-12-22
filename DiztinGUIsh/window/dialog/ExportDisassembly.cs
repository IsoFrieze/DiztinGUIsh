using System;
using System.IO;
using System.Windows.Forms;
using Diz.Core.export;
using Diz.Core.model;
using Diz.LogWriter;
using Diz.LogWriter.util;
using JetBrains.Annotations;

namespace DiztinGUIsh.window.dialog
{
    // TODO: rename? this class is mostly about editing settings, with a 'save' button at the end.
    public partial class ExportDisassembly : Form
    {
        #region Data
        // Our copy. At the end, if everything is correct, we'll return this.
        public LogWriterSettings ProposedSettings
        {
            get => proposedSettings;
            private set
            {
                proposedSettings = value;
                RegenerateSampleOutput();
            }
        }
        
        public string KeepPathsRelativeToThisPath { get; set; }
        
        private LogWriterSettings proposedSettings = new();
        #endregion

        public ExportDisassembly([CanBeNull] LogWriterSettings startingSettings = null)
        {
            ProposedSettings = startingSettings;
            UseDefaultsIfInvalidSettings();
            
            InitializeComponent();
            UpdateUiFromProjectSettings();
            RegenerateSampleOutput();
        }

        private void UseDefaultsIfInvalidSettings() => 
            ProposedSettings = ProposedSettings.GetDefaultsIfInvalid();
        
        private bool ValidateFormat() => LogCreatorLineFormatter.Validate(textFormat.Text);

        public void UpdateUiFromProjectSettings()
        {
            // TODO: in the future, replace this with databinding so we don't have to do it manually
            numData.Value = ProposedSettings.DataPerLine;
            textFormat.Text = ProposedSettings.Format;
            comboUnlabeled.SelectedIndex = (int)ProposedSettings.Unlabeled;
            comboStructure.SelectedIndex = (int)ProposedSettings.Structure;
            chkIncludeUnusedLabels.Checked = ProposedSettings.IncludeUnusedLabels;
            chkPrintLabelSpecificComments.Checked = ProposedSettings.PrintLabelSpecificComments;
        }

        private void RegenerateSampleOutput()
        {
            if (textSample == null || ProposedSettings == null)
                return;

            textSample.Text = LogUtil.GetSampleAssemblyOutput(ProposedSettings).OutputStr;
        }
        
        private string PromptForLogPathFromFileOrFolderDialog(bool askForFile) => 
            askForFile ? PromptSaveLogFile() : PromptSaveLogPath();

        private string PromptSaveLogPath()
        {
            chooseLogFolder.SelectedPath = proposedSettings.BuildFullOutputPath();
            return chooseLogFolder.ShowDialog() == DialogResult.OK && 
                   chooseLogFolder.SelectedPath != "" ? chooseLogFolder.SelectedPath : null;
        }

        private string PromptSaveLogFile()
        {
            saveLogSingleFile.InitialDirectory = proposedSettings.BuildFullOutputPath();
            return saveLogSingleFile.ShowDialog() == DialogResult.OK && 
                   saveLogSingleFile.FileName != "" ? saveLogSingleFile.FileName : null;
        }

        private bool PromptForPath()
        {
            var askForFile = ProposedSettings.Structure == LogWriterSettings.FormatStructure.SingleFile;
            var selectedFileOrFolderOutPath = PromptForLogPathFromFileOrFolderDialog(askForFile);

            if (string.IsNullOrEmpty(selectedFileOrFolderOutPath))
                return false;

            ProposedSettings = ProposedSettings.WithPathRelativeTo(selectedFileOrFolderOutPath, KeepPathsRelativeToThisPath);

            return true;
        }

        public static void ShowExportResults(LogCreatorOutput.OutputResult result)
        {
            if (result.ErrorCount > 0)
                MessageBox.Show("Disassembly created with errors. See errors.txt for details.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
                MessageBox.Show("Disassembly created successfully!", "Complete", MessageBoxButtons.OK,
                    MessageBoxIcon.Asterisk);
        }
        
        private void cancel_Click(object sender, EventArgs e) => Close();

        private void disassembleButton_Click(object sender, EventArgs e)
        {
            if (!PromptForPath())
                return;

            DialogResult = DialogResult.OK;
        }

        private void textFormat_TextChanged(object sender, EventArgs e)
        {
            if (ValidateFormat())
            {
                ProposedSettings = ProposedSettings with {Format = textFormat.Text.ToLower()};
                disassembleButton.Enabled = true;
            } else {
                textSample.Text = "Invalid format!";
                disassembleButton.Enabled = false;
            }
        }

        private void numData_ValueChanged(object sender, EventArgs e) => 
            ProposedSettings = ProposedSettings with {DataPerLine = (int)numData.Value};

        private void comboUnlabeled_SelectedIndexChanged(object sender, EventArgs e) => 
            ProposedSettings = ProposedSettings with {Unlabeled = UnlabeledFormat};

        private void comboStructure_SelectedIndexChanged(object sender, EventArgs e) =>
            ProposedSettings = ProposedSettings with {Structure = StructureFormat};
        
        private void chkPrintLabelSpecificComments_CheckedChanged(object sender, EventArgs e) => 
            ProposedSettings = ProposedSettings with {PrintLabelSpecificComments = chkPrintLabelSpecificComments.Checked};

        private void chkIncludeUnusedLabels_CheckedChanged(object sender, EventArgs e) => 
            ProposedSettings = ProposedSettings with {IncludeUnusedLabels = chkIncludeUnusedLabels.Checked};

        private LogWriterSettings.FormatUnlabeled UnlabeledFormat => 
            (LogWriterSettings.FormatUnlabeled)comboUnlabeled.SelectedIndex;
        private LogWriterSettings.FormatStructure StructureFormat => 
            (LogWriterSettings.FormatStructure)comboStructure.SelectedIndex;
    }
}

using System;
using System.IO;
using System.Windows.Forms;
using Diz.Core.export;
using Diz.Core.model;
using Diz.Core.util;

namespace DiztinGUIsh
{
    // consider renaming? this class is mostly about editing settings, with a 'save' button at the
    // end.
    public partial class ExportDisassembly : Form
    {
        private readonly Project project;

        // Our copy. At the end, if everything is correct, we'll return this.
        private LogWriterSettings settings;

        // shows the UI and returns non-null settings if everything went OK in the
        // setup process.
        public static LogWriterSettings? ConfirmSettingsAndAskToStart(Project project)
        {
            var export = new ExportDisassembly(project);
            if (export.ShowDialog() != DialogResult.OK)
                return null;

            return export.settings;
        }

        public ExportDisassembly(Project project)
        {
            this.project = project;
            settings = project.LogWriterSettings; // copy

            if (settings.Validate() != null)
                settings.SetDefaults();

            InitializeComponent();
            UpdateUiFromProjectSettings();
            RegenerateSampleOutput();
        }

        public void UpdateUiFromProjectSettings()
        {
            // TODO: in the future, replace this with databinding so we don't have to do it manually
            numData.Value = settings.DataPerLine;
            textFormat.Text = settings.Format;
            comboUnlabeled.SelectedIndex = (int)settings.Unlabeled;
            comboStructure.SelectedIndex = (int)settings.Structure;
            chkIncludeUnusedLabels.Checked = settings.IncludeUnusedLabels;
            chkPrintLabelSpecificComments.Checked = settings.PrintLabelSpecificComments;
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void disassembleButton_Click(object sender, EventArgs e)
        {
            if (!PromptForPath())
                return;

            this.DialogResult = DialogResult.OK;
        }

        // Prompt user for either a filename to save, or a folder location
        private string PromptForLogPathFromFileOrFolderDialog(bool askForFile)
        {
            return askForFile ? PromptSaveLogFile() : PromptSaveLogPath();
        }

        private string PromptSaveLogPath()
        {
            chooseLogFolder.SelectedPath = Path.GetDirectoryName(project.ProjectFileName);
            return chooseLogFolder.ShowDialog() == DialogResult.OK && chooseLogFolder.SelectedPath != ""
                ? chooseLogFolder.SelectedPath : null;
        }

        private string PromptSaveLogFile()
        {
            saveLogSingleFile.InitialDirectory = project.ProjectFileName;
            return saveLogSingleFile.ShowDialog() == DialogResult.OK && saveLogSingleFile.FileName != ""
                ? saveLogSingleFile.FileName : null;
        }

        private bool PromptForPath()
        {
            var singleFile = settings.Structure == LogCreator.FormatStructure.SingleFile;
            var fileOrFolderPath = PromptForLogPathFromFileOrFolderDialog(singleFile);

            if (string.IsNullOrEmpty(fileOrFolderPath))
                return false;

            settings.FileOrFolderOutPath = fileOrFolderPath;

            return true;
        }

        private void textFormat_TextChanged(object sender, EventArgs e)
        {
            if (ValidateFormat())
            {
                settings.Format = textFormat.Text.ToLower();
                RegenerateSampleOutput();
                disassembleButton.Enabled = true;
            } else {
                textSample.Text = "Invalid format!";
                disassembleButton.Enabled = false;
            }
        }

        private void numData_ValueChanged(object sender, EventArgs e)
        {
            settings.DataPerLine = (int)numData.Value;
            RegenerateSampleOutput();
        }

        private void comboUnlabeled_SelectedIndexChanged(object sender, EventArgs e)
        {
            settings.Unlabeled = (LogCreator.FormatUnlabeled)comboUnlabeled.SelectedIndex;
            RegenerateSampleOutput();
        }

        private void comboStructure_SelectedIndexChanged(object sender, EventArgs e)
        {
            settings.Structure = (LogCreator.FormatStructure)comboStructure.SelectedIndex;
        }

        private bool ValidateFormat()
        {
            return LogCreator.ValidateFormat(textFormat.Text);
        }

        private void RegenerateSampleOutput()
        {
            var result = RomUtil.GetSampleAssemblyOutput(settings);
            textSample.Text = result.OutputStr;
        }

        private void chkPrintLabelSpecificComments_CheckedChanged(object sender, EventArgs e)
        {
            settings.PrintLabelSpecificComments = chkPrintLabelSpecificComments.Checked;
        }

        private void chkIncludeUnusedLabels_CheckedChanged(object sender, EventArgs e)
        {
            settings.IncludeUnusedLabels = chkIncludeUnusedLabels.Checked;
        }

        public static void ShowExportResults(LogCreator.OutputResult result)
        {
            if (result.ErrorCount > 0)
                MessageBox.Show("Disassembly created with errors. See errors.txt for details.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
                MessageBox.Show("Disassembly created successfully!", "Complete", MessageBoxButtons.OK,
                    MessageBoxIcon.Asterisk);
        }
    }
}

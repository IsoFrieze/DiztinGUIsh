using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Diz.Core;
using Diz.Core.export;
using Diz.Core.model;
using Diz.Core.util;

namespace DiztinGUIsh
{
    // consider renaming? this class is mostly about editing settings, with a 'save' button at the
    // end.
    public partial class ExportDisassembly : Form
    {
        private readonly Project Project;

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
            Project = project;
            settings = project.LogWriterSettings; // copy

            if (settings.Validate() != "")
                settings.SetDefaults();

            InitializeComponent();
            UpdateUiFromProjectSettings();
            RegenerateSampleOutput();
        }

        public void UpdateUiFromProjectSettings()
        {
            // TODO: in the future, replace this with databinding so we don't have to do it manually
            numData.Value = settings.dataPerLine;
            textFormat.Text = settings.format;
            comboUnlabeled.SelectedIndex = (int)settings.unlabeled;
            comboStructure.SelectedIndex = (int)settings.structure;
            chkIncludeUnusedLabels.Checked = settings.includeUnusedLabels;
            chkPrintLabelSpecificComments.Checked = settings.printLabelSpecificComments;
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button2_Click(object sender, EventArgs e)
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
            chooseLogFolder.SelectedPath = Path.GetDirectoryName(Project.ProjectFileName);
            return chooseLogFolder.ShowDialog() == DialogResult.OK && chooseLogFolder.SelectedPath != ""
                ? chooseLogFolder.SelectedPath : null;
        }

        private string PromptSaveLogFile()
        {
            saveLogSingleFile.InitialDirectory = Project.ProjectFileName;
            return saveLogSingleFile.ShowDialog() == DialogResult.OK && saveLogSingleFile.FileName != ""
                ? saveLogSingleFile.FileName : null;
        }

        private bool PromptForPath()
        {
            var singleFile = settings.structure == LogCreator.FormatStructure.SingleFile;
            var fileOrFolderPath = PromptForLogPathFromFileOrFolderDialog(singleFile);

            if (string.IsNullOrEmpty(fileOrFolderPath))
                return false;

            settings.fileOrFolderOutPath = fileOrFolderPath;

            return true;
        }

        private void textFormat_TextChanged(object sender, EventArgs e)
        {
            if (ValidateFormat())
            {
                settings.format = textFormat.Text.ToLower();
                RegenerateSampleOutput();
                button2.Enabled = true;
            } else {
                textSample.Text = "Invalid format!";
                button2.Enabled = false;
            }
        }

        private void numData_ValueChanged(object sender, EventArgs e)
        {
            settings.dataPerLine = (int)numData.Value;
            RegenerateSampleOutput();
        }

        private void comboUnlabeled_SelectedIndexChanged(object sender, EventArgs e)
        {
            settings.unlabeled = (LogCreator.FormatUnlabeled)comboUnlabeled.SelectedIndex;
            RegenerateSampleOutput();
        }

        private void comboStructure_SelectedIndexChanged(object sender, EventArgs e)
        {
            settings.structure = (LogCreator.FormatStructure)comboStructure.SelectedIndex;
        }

        private bool ValidateFormat()
        {
            return LogCreator.ValidateFormat(textFormat.Text);
        }

        private void RegenerateSampleOutput()
        {
            var result = RomUtil.GetSampleAssemblyOutput(settings);
            textSample.Text = result.outputStr;
        }

        private void chkPrintLabelSpecificComments_CheckedChanged(object sender, EventArgs e)
        {
            settings.printLabelSpecificComments = chkPrintLabelSpecificComments.Checked;
        }

        private void chkIncludeUnusedLabels_CheckedChanged(object sender, EventArgs e)
        {
            settings.includeUnusedLabels = chkIncludeUnusedLabels.Checked;
        }
    }
}

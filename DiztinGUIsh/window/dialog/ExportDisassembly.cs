using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace DiztinGUIsh
{
    // consider renaming? this class is mostly about editing settings, with a 'save' button at the
    // end.
    public partial class ExportDisassembly : Form
    {
        //private readonly LogCreator LogCreator;
        private Data Data => Project.Data;
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
            if (askForFile)
            {
                saveLogSingleFile.InitialDirectory = Project.ProjectFileName;
                if (saveLogSingleFile.ShowDialog() == DialogResult.OK && saveLogSingleFile.FileName != "")
                    return saveLogSingleFile.FileName;
            }
            else
            {
                chooseLogFolder.SelectedPath = Path.GetDirectoryName(Project.ProjectFileName);
                if (chooseLogFolder.ShowDialog() == DialogResult.OK && chooseLogFolder.SelectedPath != "")
                    return saveLogSingleFile.FileName;
            }

            return null;
        }

        private bool PromptForPath()
        {
            var singleFile = settings.structure == LogCreator.FormatStructure.SingleFile;
            var path = PromptForLogPathFromFileOrFolderDialog(singleFile);

            if (string.IsNullOrEmpty(path))
                return false;

            // kinda weird. we should probably just pass
            // the containing folder name and let LogCreator handle these details
            if (!singleFile)
                path += "/main.asm";

            settings.file = path;
            settings.error = Path.GetDirectoryName(path) + "/error.txt";

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
            textSample.Text = CreateSampleOutput();
        }

        private void chkPrintLabelSpecificComments_CheckedChanged(object sender, EventArgs e)
        {
            settings.printLabelSpecificComments = chkPrintLabelSpecificComments.Checked;
        }

        private void chkIncludeUnusedLabels_CheckedChanged(object sender, EventArgs e)
        {
            settings.includeUnusedLabels = chkIncludeUnusedLabels.Checked;
        }
        private string CreateSampleOutput()
        {
            using var mem = new MemoryStream();
            using var sw = new StreamWriter(mem);

            // make a copy, but override the FormatStructure so it's all in one file
            var sampleSettings = settings;
            sampleSettings.structure = LogCreator.FormatStructure.SingleFile;

            Data sampleData = SampleRomData.SampleData;

            var lc = new LogCreator()
            {
                Settings = sampleSettings,
                Data = SampleRomData.SampleData,
                StreamOutput = sw,
                StreamError = StreamWriter.Null,
            };

            lc.CreateLog();

            sw.Flush();
            mem.Seek(0, SeekOrigin.Begin);
            return Encoding.UTF8.GetString(mem.ToArray(), 0, (int)mem.Length);
        }
    }
}

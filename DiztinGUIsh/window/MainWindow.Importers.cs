using System;
using System.Windows.Forms;
using Diz.Controllers.interfaces;
using DiztinGUIsh.window.dialog;

namespace DiztinGUIsh.window
{
    public partial class MainWindow
    {
        public IImportRomDialogView GetImportView() => new ImportRomDialog();

        private void ImportBizhawkCDL()
        {
            var filename = PromptOpenBizhawkCDLFile();
            if (filename != null && filename == "") return;
            ImportBizHawkCdl(filename);
            UpdateSomeUI2();
        }

        private void ImportBizHawkCdl(string filename)
        {
            try
            {
                ProjectController.ImportBizHawkCdl(filename);
            }
            catch (Exception ex)
            {
                ShowError(ex.Message, "Error");
            }
        }

        private void ImportBsnesTraceLogText()
        {
            if (!PromptForImportBSNESTraceLogFile()) return;
            var (numModifiedFlags, numFiles) = ImportBSNESTraceLogs();
            ReportNumberFlagsModified(numModifiedFlags, numFiles);
        }

        private void ImportBSNESUsageMap()
        {
            if (openUsageMapFile.ShowDialog() != DialogResult.OK)
                return;

            var numModifiedFlags = ProjectController.ImportBsnesUsageMap(openUsageMapFile.FileName);

            ShowInfo($"Modified total {numModifiedFlags} flags!", "Done");
        }

        private (long numBytesModified, int numFiles) ImportBSNESTraceLogs()
        {
            var numBytesModified = ProjectController.ImportBsnesTraceLogs(openTraceLogDialog.FileNames);
            return (numBytesModified, openTraceLogDialog.FileNames.Length);
        }

        private void ImportBsnesBinaryTraceLog()
        {
            new BsnesTraceLogBinaryMonitorForm(this).ShowDialog();
            RefreshUi();
        }

        private void OnImportedProjectSuccess()
        {
            UpdateSaveOptionStates(saveEnabled: false, saveAsEnabled: true, closeEnabled: true);
            RefreshUi();
        }
    }
}
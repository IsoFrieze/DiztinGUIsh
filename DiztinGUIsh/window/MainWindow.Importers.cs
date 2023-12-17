using System;
using System.Windows.Forms;
using DiztinGUIsh.window.dialog;

namespace DiztinGUIsh.window
{
    public partial class MainWindow
    {
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
            if (!PromptForImportBSNESTraceLogFile()) 
                return;
            
            var (numModifiedFlags, numFiles) = ImportBsnesTraceLogs();
            
            RefreshUi();
            ReportNumberFlagsModified(numModifiedFlags, numFiles);
        }

        private void UiImportBsnesUsageMap()
        {
            if (openUsageMapFile.ShowDialog() != DialogResult.OK)
                return;

            var numModifiedFlags = ProjectController.ImportBsnesUsageMap(openUsageMapFile.FileName);
            
            RefreshUi();
            ShowInfo($"Modified total {numModifiedFlags} flags!", "Done");
        }

        private (long numBytesModified, int numFiles) ImportBsnesTraceLogs()
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
using System.IO;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;
using DiztinGUIsh.Properties;

namespace DiztinGUIsh.window
{
    // This file is mostly about various controls reacting to state changes
    //
    // The direction we need to take this is being driven almost 100% by INotifyChanged events coming off 
    // DizDocument, Data, and Project classes --- instead of being explicitly pushed.
    public partial class MainWindow
    {
        private void RebindProject()
        {
            AliasList?.RebindProject();
            if (visualForm != null) 
                visualForm.Project = Project;
        }

        private void UpdatePanels()
        {
            table.Height = this.Height - 85;
            table.Width = this.Width - 33;
            vScrollBar1.Height = this.Height - 85;
            vScrollBar1.Left = this.Width - 33;
            if (WindowState == FormWindowState.Maximized) UpdateDataGridView();
        }

        public void UpdateWindowTitle()
        {
            Text =
                (Project.UnsavedChanges ? "*" : "") +
                (Project.ProjectFileName ?? "New Project") +
                " - DiztinGUIsh";
        }

        private void UpdateUiFromSettings()
        {
            var lastOpenedFilePresent = Settings.Default.LastOpenedFile != "";

            toolStripOpenLast.Enabled = lastOpenedFilePresent;
            toolStripOpenLast.Text = "Open Last File";
            if (lastOpenedFilePresent)
                toolStripOpenLast.Text += $" ({Path.GetFileNameWithoutExtension(Settings.Default.LastOpenedFile)})";

            openLastProjectAutomaticallyToolStripMenuItem.Checked = Settings.Default.OpenLastFileAutomatically;
        }

        private void RefreshUi()
        {
            importCDLToolStripMenuItem.Enabled = true;
            UpdateWindowTitle();
            UpdateDataGridView();
            UpdatePercent();
            table.Invalidate();
            EnableSubWindows();
        }

        private void UpdateBase(Util.NumberBase noBase)
        {
            displayBase = noBase;
            decimalToolStripMenuItem.Checked = noBase == Util.NumberBase.Decimal;
            hexadecimalToolStripMenuItem.Checked = noBase == Util.NumberBase.Hexadecimal;
            binaryToolStripMenuItem.Checked = noBase == Util.NumberBase.Binary;
            InvalidateTable();
        }

        public void UpdatePercent()
        {
            if (Project?.Data == null || Project.Data.GetRomSize() <= 0)
                return;

            int totalUnreached = 0, size = Project.Data.GetRomSize();
            for (int i = 0; i < size; i++)
                if (Project.Data.GetFlag(i) == FlagType.Unreached)
                    totalUnreached++;
            int reached = size - totalUnreached;
            percentComplete.Text = $"{reached * 100.0 / size:N3}% ({reached:D}/{size:D})";
        }

        public void UpdateMarkerLabel()
        {
            currentMarker.Text = $"Marker: {markFlag.ToString()}";
        }

        private void UpdateDataGridView()
        {
            if (Project?.Data == null || Project.Data.GetRomSize() <= 0)
                return;

            rowsToShow = ((table.Height - table.ColumnHeadersHeight) / table.RowTemplate.Height);

            if (ViewOffset + rowsToShow > Project.Data.GetRomSize())
                ViewOffset = Project.Data.GetRomSize() - rowsToShow;

            if (ViewOffset < 0)
                ViewOffset = 0;

            vScrollBar1.Enabled = true;
            vScrollBar1.Maximum = Project.Data.GetRomSize() - rowsToShow;
            vScrollBar1.Value = ViewOffset;
            table.RowCount = rowsToShow;

            importerMenuItemsEnabled = true;
            UpdateImporterEnabledStatus();
        }

        private void UpdateImporterEnabledStatus()
        {
            importUsageMapToolStripMenuItem.Enabled = importerMenuItemsEnabled;
            importCDLToolStripMenuItem.Enabled = importerMenuItemsEnabled;
            importTraceLogBinary.Enabled = importerMenuItemsEnabled;
            importTraceLogText.Enabled = importerMenuItemsEnabled;
        }

        private void EnableSubWindows()
        {
            labelListToolStripMenuItem.Enabled = true;
        }

        public void UpdateSaveOptionStates(bool saveEnabled, bool saveAsEnabled, bool closeEnabled)
        {
            saveProjectToolStripMenuItem.Enabled = saveEnabled;
            saveProjectAsToolStripMenuItem.Enabled = saveAsEnabled;
            closeProjectToolStripMenuItem.Enabled = closeEnabled;

            exportLogToolStripMenuItem.Enabled = true;
        }

        private void UpdateSomeUI2()
        {
            // refactor this somewhere else
            UpdateUI_Tmp3();
            InvalidateTable();
        }

        private void UpdateUI_Tmp3()
        {
            // refactor this somewhere else
            UpdatePercent();
            UpdateWindowTitle();
        }
    }
}
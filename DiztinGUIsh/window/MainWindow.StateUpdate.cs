using System.IO;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;
using Diz.Cpu._65816;
using DiztinGUIsh.Properties;

namespace DiztinGUIsh.window;

// This file is mostly about various controls reacting to state changes
//
// The direction we need to take this is being driven almost 100% by INotifyChanged events coming off 
// DizDocument, Data, and Project classes --- instead of being explicitly pushed.
public partial class MainWindow
{
    private void RebindProject()
    {
        aliasList?.RebindProject();
            
        if (Project?.Data.Labels != null) 
            Project.Data.Labels.OnLabelChanged += LabelsOnOnLabelChanged;
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
            (Project.Session?.UnsavedChanges ?? true ? "*" : "") +
            (string.IsNullOrEmpty(Project.ProjectFileName) ? "New Project" : Project.ProjectFileName) +
            " - DiztinGUIsh";
    }

    private void UpdateUiFromSettings()
    {
        var lastOpenedFilePresent = appSettings.LastOpenedFile != "";

        toolStripOpenLast.Enabled = lastOpenedFilePresent;
        toolStripOpenLast.Text = "Open Last File";
        if (lastOpenedFilePresent)
            toolStripOpenLast.Text += $" ({Path.GetFileNameWithoutExtension(appSettings.LastOpenedFile)})";

        openLastProjectAutomaticallyToolStripMenuItem.Checked = appSettings.OpenLastFileAutomatically;
    }

    private void RefreshUi()
    {
        importCDLToolStripMenuItem.Enabled = true;
        UpdateWindowTitle();
        UpdateDataGridView();
        ScheduleUpdatePercentageUncovered();
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

    public void UpdatePercent(bool forceRecalculate = false)
    {
        if (Project?.Data == null || Project.Data.GetRomSize() <= 0)
            return;

        var size = Project.Data.GetRomSize();
        var shouldRecalcNow = forceRecalculate || _cachedReached == -1; 
        var reached = shouldRecalcNow ? CalculateTotalBytesReached() : _cachedReached;
        var recalculatingInProgress = _cooldownForPercentUpdate != -1;

        var reCalcMsg  = recalculatingInProgress ? "[recalculating...]" : "";
        if (reached == -1)
        {
            percentComplete.Text = reCalcMsg;
        }
        else
        {
            percentComplete.Text = $"{reached * 100.0 / size:N3}% ({reached:D}/{size:D}) {reCalcMsg}";
        }

        _cachedReached = reached;
    }

    private int _cachedReached = -1;

    // CAUTION: very expensive method. be careful using in UI performance-critical places
    private int CalculateTotalBytesReached()
    {
        int totalUnreached = 0, size = Project.Data.GetRomSize();
        for (int i = 0; i < size; i++)
            if (Project.Data.GetSnesApi().GetFlag(i) == FlagType.Unreached)
                totalUnreached++;
        int reached = size - totalUnreached;
        return reached;
    }

    public void UpdateMarkerLabel()
    {
        currentMarker.Text = $"Marker: {markFlag.ToString()}";
    }

    private void UpdateDataGridView()
    {
        if (Project?.Data == null || Project.Data.GetRomSize() <= 0)
            return;

        rowsToShow = (table.Height - table.ColumnHeadersHeight) / table.RowTemplate.Height;

        ClampViewOffsetToRomSize();

        vScrollBar1.Enabled = true;
        vScrollBar1.Maximum = Project.Data.GetRomSize() - rowsToShow;
        vScrollBar1.Value = ViewOffset;
            
        table.RowCount = rowsToShow;

        importerMenuItemsEnabled = true;
        UpdateImporterEnabledStatus();
    }

    private void ClampViewOffsetToRomSize()
    {
        if (ViewOffset + rowsToShow > Project.Data.GetRomSize())
            ViewOffset = Project.Data.GetRomSize() - rowsToShow;

        if (ViewOffset < 0)
            ViewOffset = 0;
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
        projectSettingsToolStripMenuItem.Enabled = true;
    }

    public void UpdateSaveOptionStates(bool saveEnabled, bool saveAsEnabled, bool closeEnabled)
    {
        saveProjectToolStripMenuItem.Enabled = saveEnabled;
        saveProjectAsToolStripMenuItem.Enabled = saveAsEnabled;
        closeProjectToolStripMenuItem.Enabled = closeEnabled;

        toolStrip_exportDisassemblyUseCurrentSettings.Enabled = true;
        toolStrip_exportDisassemblyEditSettingsFirst.Enabled = true;
        toolStrip_openExportDirectory.Enabled = true;
    }

    private void UpdateSomeUI2()
    {
        // refactor this somewhere else
        UpdateUi_TimerAndPercent();
        InvalidateTable();
    }

    public int _cooldownForPercentUpdate = 0;

    private void ScheduleUpdatePercentageUncovered()
    {
        // UpdatePercent() with a force refresh is very expensive so only do it when we're idle for a bit.
        _cooldownForPercentUpdate = 3; // 3 times x 2 seconds = 6 seconds.
        UpdatePercent(); // less expensive invocation that just updates UI 
    }

    private void UpdateUi_TimerAndPercent()
    {
        ScheduleUpdatePercentageUncovered();
        UpdateWindowTitle();
    }
}
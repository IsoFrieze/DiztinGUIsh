using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Diz.Controllers.controllers;
using Diz.Core.commands;
using Diz.Cpu._65816;
using Diz.LogWriter;
using Diz.Ui.Winforms;
using Diz.Ui.Winforms.dialogs;
using Diz.Ui.Winforms.util;
using DiztinGUIsh.window.dialog;

namespace DiztinGUIsh.window;

public partial class MainWindow
{
    private bool PromptContinueEvenIfUnsavedChanges()
    {
        if (Project == null || !(Project.Session?.UnsavedChanges ?? true))
            return true;

        var result = MessageBox.Show(
            "You have unsaved changes; they will be lost if you continue.\nDo you want to save changes?",
            "Unsaved Changes", MessageBoxButtons.YesNoCancel);

        if (result == DialogResult.Yes)
            SaveProject(askFilenameIfNotSet: true, alwaysAsk: false);
            
        return result != DialogResult.Cancel;
    }

    private string PromptForOpenFilename()
    {
        // TODO: combine with another function here that does similar
        openFileDialog.InitialDirectory = Project?.ProjectFileName ?? "";
        return openFileDialog.ShowDialog() == DialogResult.OK ? openFileDialog.FileName : "";
    }

    private static void ShowExportResults(LogCreatorOutput.OutputResult result)
    {
        if (result.ErrorCount > 0)
            MessageBox.Show("Disassembly files exported, but contains errors (but... it will probably still assemble correctly. try it). See generated errors.txt for further details.", "Warning",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        else
            MessageBox.Show("Disassembly files exported successfully!", "Complete", MessageBoxButtons.OK,
                MessageBoxIcon.Asterisk);
    }

    private bool PromptForOpenProjectFilename()
    {
        if (!PromptContinueEvenIfUnsavedChanges())
            return false;

        openProjectFile.InitialDirectory = Project?.ProjectFileName;
        return openProjectFile.ShowDialog() == DialogResult.OK;
    }

    private void viewHelpToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var helpUrl = "https://github.com/IsoFrieze/DiztinGUIsh/blob/master/DiztinGUIsh/dist/HELP.md";
        try
        {
            // System.Diagnostics.Process.Start(Directory.GetCurrentDirectory() + "/help.html");
            GuiUtil.OpenExternalProcess(helpUrl);
        }
        catch (Exception)
        {
            MessageBox.Show("Failed to open help url:\r\n"+helpUrl+"", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void githubToolStripMenuItem_Click(object sender, EventArgs e) =>
        GuiUtil.OpenExternalProcess("https://github.com/Isofrieze/DiztinGUIsh");

    private string PromptOpenBizhawkCDLFile()
    {
        openCDLDialog.InitialDirectory = Project.ProjectFileName;
        if (openCDLDialog.ShowDialog() != DialogResult.OK)
            return "";

        return !PromptContinueEvenIfUnsavedChanges() ? "" : openCDLDialog.FileName;
    }

    private static void ReportNumberFlagsModified(long numModifiedFlags, int numFiles = 1)
    {
        MessageBox.Show($"Modified total {numModifiedFlags} flags from {numFiles} files!",
            "Done",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private bool PromptForImportBSNESTraceLogFile()
    {
        openTraceLogDialog.Multiselect = true;
        return openTraceLogDialog.ShowDialog() == DialogResult.OK;
    }
        
    private static void ShowOffsetOutOfRangeMsg()
    {
        ShowError("That offset is out of range.", "Error");
    }

    private int PromptForGotoOffset()
    {
        if (!RomDataPresent())
            return -1;

        var go = new GotoDialog(ViewOffset + table.CurrentCell.RowIndex, Project.Data);
        var result = go.ShowDialog();
        if (result != DialogResult.OK)
            return -1;
            
        return go.GetPcOffset();
    }

    private static void ShowError(string errorMsg, string caption = "Error")
    {
        MessageBox.Show(errorMsg, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private bool PromptHarshAutoStep(int offset, out int newOffset, out int count)
    {
        newOffset = count = -1;
            
        var harsh = new HarshAutoStep(offset, Project.Data);
        if (harsh.ShowDialog() != DialogResult.OK)
            return false;
            
        newOffset = harsh.StartRomOffset;
        count = harsh.Count;
        return true;
    }

    public MarkCommand PromptMarkMany(int offset, MarkCommand.MarkManyProperty property)
    {
        var markManyController = CreateMarkManyController(offset, property);
        var markCommand = markManyController.GetMarkCommand();

        if (markCommand != null) 
            SavedMarkManySettings = markManyController.Settings;

        return markCommand;
    }

    private Dictionary<MarkCommand.MarkManyProperty, object> SavedMarkManySettings { get; set; } = new();
        
    private MarkManyController<ISnesData> CreateMarkManyController(int offset, MarkCommand.MarkManyProperty property)
    {
        // NOTE: in upstream 3.0 branch, replace this with dependency injection
        var view = new MarkManyView<ISnesData>();
        var markManyController = new MarkManyController<ISnesData>(offset, property, Project.Data.GetSnesApi(), view)
        {
            Settings = SavedMarkManySettings
        };
        markManyController.MarkManyView.Controller = markManyController;
        return markManyController;
    }

    private bool PromptForMisalignmentCheck()
    {
        if (!RomDataPresent())
            return false;

        return new MisalignmentChecker(Project.Data).ShowDialog() == DialogResult.OK;
    }

    private static void ShowInfo(string s, string caption)
    {
        MessageBox.Show(s, caption,
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private bool PromptForInOutChecking()
    {
        if (!RomDataPresent())
            return false;

        return new InOutPointChecker().ShowDialog() == DialogResult.OK;
    }

    public string AskToSelectNewRomFilename(string promptSubject, string promptText)
    {
        string initialDir = null; // TODO: Project.ProjectFileName
        return GuiUtil.PromptToConfirmAction(promptSubject, promptText, 
            () => GuiUtil.PromptToSelectFile(initialDir)
        );
    }

    public void OnProjectOpenWarnings(IEnumerable<string> warnings)
    {
        foreach (var warningMsg in warnings) {
            MessageBox.Show(warningMsg, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
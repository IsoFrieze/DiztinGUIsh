using System.Windows.Forms;
using Diz.Controllers.controllers;
using Diz.Core.commands;
using Diz.Core.model;
using Diz.Core.util;
using Diz.Cpu._65816;
using DiztinGUIsh.Properties;

namespace DiztinGUIsh.window;

public partial class MainWindow
{
    private void OpenLastProject()
    {
        if (Document.LastProjectFilename == "")
            return;

        // safeguard: if we crash opening this project,
        // then next time we load make sure we don't try it again.
        // this will be reset later
        var projectToOpen = Document.LastProjectFilename;
        Document.LastProjectFilename = "";

        ProjectController.OpenProject(projectToOpen);
    }

    private void OpenProject()
    {
        if (!PromptForOpenProjectFilename()) 
            return;

        ProjectController.OpenProject(openProjectFile.FileName);
    }

    private void CreateNewProject()
    {
        if (!PromptContinueEvenIfUnsavedChanges())
            return;

        var romFilename = PromptForOpenFilename();
        if (romFilename == "")
            return;

        ProjectController.ImportRomAndCreateNewProject(openFileDialog.FileName);
    }

    private void Step(int offset)
    {
        if (!RomDataPresent()) 
            return;
            
        ProjectController.MarkChanged();
        var newOffset = Project.Data.GetSnesApi().Step(offset, false, false, offset - 1);
        SelectOffset(newOffset, new ISnesNavigation.HistoryArgs {Description = "Step Over"});
        UpdateUi_TimerAndPercent();
    }

    private void StepIn(int offset)
    {
        if (!RomDataPresent()) 
            return;
            
        ProjectController.MarkChanged();
        var newOffset = Project.Data.GetSnesApi().Step(offset, true, false, offset - 1);
        SelectOffset(newOffset, new ISnesNavigation.HistoryArgs {Description = "Step Into"});
        UpdateUi_TimerAndPercent();
    }

    private void AutoStepSafe(int offset)
    {
        if (!RomDataPresent()) 
            return;
            
        ProjectController.MarkChanged();
        var destination = Project.Data.GetSnesApi().AutoStepSafe(offset);
        if (moveWithStep) 
            SelectOffset(destination, new ISnesNavigation.HistoryArgs {Description = "AutoStep (Safe)"});
            
        UpdateUi_TimerAndPercent();
    }

    private void AutoStepHarsh(int offset)
    {
        if (!RomDataPresent()) 
            return;
            
        if (!PromptHarshAutoStep(offset, out var newOffset, out var count))
            return;

        ProjectController.MarkChanged();
        var destination = Project.Data.GetSnesApi().AutoStepHarsh(newOffset, count);
            
        if (moveWithStep) 
            SelectOffset(destination, new ISnesNavigation.HistoryArgs {Description = "AutoStep (Harsh)"});

        UpdateUi_TimerAndPercent();
    }

    private void Mark(int offset)
    {
        if (!RomDataPresent()) 
            return;
            
        ProjectController.MarkChanged();
        var newOffset = Project.Data.GetSnesApi().MarkTypeFlag(offset, markFlag, RomUtil.GetByteLengthForFlag(markFlag));
            
        SelectOffset(newOffset, new ISnesNavigation.HistoryArgs {Description = "Mark (single)"});
            
        UpdateUi_TimerAndPercent();
    }

    private void MarkMany(int offset, MarkCommand.MarkManyProperty property)
    {
        if (!RomDataPresent()) 
            return;

        var mark = PromptMarkMany(offset, property);
        if (mark == null)
            return;

        MarkMany(mark.Property, mark.Start, mark.Value, mark.Count);

        UpdateSomeUI2();
    }

    private void MarkMany(MarkCommand.MarkManyProperty markProperty, int markStart, object markValue, int markCount)
    {
        var snesApi = Project.Data.GetSnesApi();
        if (snesApi == null)
            return;
            
        var newNavigatedOffset = markProperty switch
        {
            MarkCommand.MarkManyProperty.Flag => snesApi.MarkTypeFlag(markStart, (FlagType) markValue, markCount),
            MarkCommand.MarkManyProperty.DataBank => snesApi.MarkDataBank(markStart, (int) markValue, markCount),
            MarkCommand.MarkManyProperty.DirectPage => snesApi.MarkDirectPage(markStart, (int) markValue, markCount),
            MarkCommand.MarkManyProperty.MFlag => snesApi.MarkMFlag(markStart, (bool) markValue, markCount),
            MarkCommand.MarkManyProperty.XFlag => snesApi.MarkXFlag(markStart, (bool) markValue, markCount),
            MarkCommand.MarkManyProperty.CpuArch => snesApi.MarkArchitecture(markStart, (Architecture) markValue, markCount),
            _ => -1
        };

        ProjectController.MarkChanged();

        if (moveWithStep && newNavigatedOffset != -1)
            SelectOffset(newNavigatedOffset, new ISnesNavigation.HistoryArgs {Description = "Mark (multi)"});
    }

    private void GoToIntermediateAddress(int offset)
    {
        var snesOffset = FindIntermediateAddress(offset);
        if (snesOffset == -1)
            return;
            
        SelectOffset(snesOffset, 1, new ISnesNavigation.HistoryArgs {Description = "GoTo Intermediate Addr"});
    }

    public void GoTo(int offset)
    {
        if (IsOffsetInRange(offset))
            SelectOffset(offset, new ISnesNavigation.HistoryArgs {Description = "Goto"});
        else
            ShowOffsetOutOfRangeMsg();
    }

    /// <summary>
    /// Navigate to unreached (unmarked) regions of the ROM
    /// </summary>
    /// <param name="fromStartOrEnd">If false, use current offset as starting point. If true, start at beginning (for forward) or end (for backwards) of ROM instead</param>
    /// <param name="forwardDirection">True to search forward, false to search backwards</param>
    private void GoToUnreached(bool fromStartOrEnd, bool forwardDirection)
    {
        if (!FindUnreached(SelectedOffset, fromStartOrEnd, forwardDirection, out var unreached))
            return;

        SelectOffset(unreached, 1, BuildUnreachedHistoryArgs(fromStartOrEnd, forwardDirection));
    }

    private static ISnesNavigation.HistoryArgs BuildUnreachedHistoryArgs(bool fromStartOrEnd, bool forwardDirection)
    {
        var dirStr = forwardDirection ? "Forward" : "Previous";
        var endStr = !fromStartOrEnd ? "" 
            : forwardDirection 
                ? " (From ROM start)" 
                : " (From ROM end)";
            
        return new ISnesNavigation.HistoryArgs
        {
            Description = $"GoTo Unreached: {dirStr} {endStr}"
        };
    }


    private void FixMisalignedInstructions()
    {
        if (!PromptForMisalignmentCheck())
            return;

        var count = Project.Data.GetSnesApi().FixMisalignedFlags();

        if (count > 0)
            ProjectController.MarkChanged();
        InvalidateTable();
            
        ShowInfo($"Modified {count} flags!", "Done!");
    }

    private void RescanForInOut()
    {
        if (!PromptForInOutChecking()) 
            return;

        Project.Data.GetSnesApi().RescanInOutPoints();
        ProjectController.MarkChanged();
            
        InvalidateTable();
        ShowInfo("Scan complete!", "Done!");
    }

    private void SaveProject(bool askFilenameIfNotSet = true, bool alwaysAsk = false)
    {
        var showPrompt = 
            askFilenameIfNotSet && string.IsNullOrEmpty(Project.ProjectFileName) || 
            alwaysAsk;

        var promptedFilename = "";
        if (showPrompt)
        {
            saveProjectFile.InitialDirectory = Project.AttachedRomFilename;
            if (saveProjectFile.ShowDialog() != DialogResult.OK || string.IsNullOrEmpty(saveProjectFile.FileName))
                return;

            promptedFilename = saveProjectFile.FileName;
        }

        var origFilename = Project.ProjectFileName;
        if (!string.IsNullOrEmpty(promptedFilename))
            Project.ProjectFileName = promptedFilename;

        var err = ProjectController.SaveProject(Project.ProjectFileName);

        if (err == null) 
            return;
            
        Project.ProjectFileName = origFilename;
        ShowError($"Couldn't save: {err}");
    }

    private void ShowVisualizerForm()
    {
        visualForm ??= new VisualizerForm(this);
        visualForm.Show();
    }

    private void ShowCommentList()
    {
        AliasList.Show();
    }

    private void SetMarkerLabel(FlagType flagType)
    {
        markFlag = flagType;
        UpdateMarkerLabel();
    }

    private void ToggleMoveWithStep()
    {
        moveWithStep = !moveWithStep;
        moveWithStepToolStripMenuItem.Checked = moveWithStep;
    }

    private void ToggleOpenLastProjectEnabled()
    {
        Settings.Default.OpenLastFileAutomatically = openLastProjectAutomaticallyToolStripMenuItem.Checked;
        Settings.Default.Save();
        UpdateUiFromSettings();
    }
}
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core.commands;
using Diz.Core.model;
using Diz.Core.util;
using Diz.Cpu._65816;
using Diz.Ui.Winforms.dialogs;

namespace DiztinGUIsh.window;

public partial class MainWindow
{
    // when navigating near the edge of the grid, this controls how close we'll allow getting to the bottom row before
    // scrolling the screen a bit (because it's helpful to be able to see where you're going and not always be operating on
    // the edge of the grid)
    // arbitrary: increase if using bigger grid sizes. ideally, this would be dynamically generated in the future based on a % of the grid
    private const int standardOvershootAmount = 12;
    
    private void OpenLastProject()
    {
        if (Document.LastProjectFilename == "")
            return;

        // safeguard: during automatic loading of projects at startup,
        // temporarily un-set the "last project opened filename".
        // this is because, if we crash, the next time the app starts it won't crash again :)
        // after this is loaded successfully, we'll set the filename again so this loads successfully.
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

    private void OpenExportDirectory()
    {
        var projectSettings = ProjectController.Project.LogWriterSettings;
        var exportDirectory = Path.Combine(projectSettings.BaseOutputPath, projectSettings.FileOrFolderOutPath);

        OpenDirectory(exportDirectory);
    }

    private void OpenDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                Arguments = path,
                FileName = "explorer.exe",
            };
            Process.Start(startInfo);
        }
        else
        {
            MessageBox.Show(string.Format("{0} does not exist!", path));
        }
    }

    private void Step(int offset)
    {
        if (!RomDataPresent()) 
            return;
        
        var snesData = Project.Data.GetSnesApi();
        if (snesData == null)
            return;
            
        ProjectController.MarkChanged();
        var newOffset = snesData.Step(offset, false, false, offset - 1);
        SelectOffset(newOffset, -1, new ISnesNavigation.HistoryArgs {Description = "Step Over"}, overshootAmount: standardOvershootAmount);
        UpdateUi_TimerAndPercent();
    }

    private void StepIn(int offset)
    {
        if (!RomDataPresent()) 
            return;
            
        ProjectController.MarkChanged();
        var newOffset = Project.Data.GetSnesApi().Step(offset, true, false, offset - 1);
        SelectOffset(newOffset, -1, new ISnesNavigation.HistoryArgs {Description = "Step Into"}, overshootAmount: standardOvershootAmount);
        UpdateUi_TimerAndPercent();
    }

    private void AutoStepSafe(int offset)
    {
        if (!RomDataPresent()) 
            return;
            
        ProjectController.MarkChanged();
        var destination = Project.Data.GetSnesApi().AutoStepSafe(offset);
        if (moveWithStep) 
            SelectOffset(destination, -1, new ISnesNavigation.HistoryArgs {Description = "AutoStep (Safe)"}, overshootAmount: standardOvershootAmount);
            
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
            SelectOffset(destination, -1, new ISnesNavigation.HistoryArgs {Description = "AutoStep (Harsh)"}, overshootAmount: standardOvershootAmount);

        UpdateUi_TimerAndPercent();
    }

    private void Mark(int offset)
    {
        if (!RomDataPresent()) 
            return;
            
        ProjectController.MarkChanged();
        var newOffset = Project.Data.GetSnesApi().MarkTypeFlag(offset, markFlag, RomUtil.GetByteLengthForFlag(markFlag));
            
        SelectOffset(newOffset, -1, new ISnesNavigation.HistoryArgs {Description = "Mark (single)"}, overshootAmount: standardOvershootAmount);
            
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
            SelectOffset(offset, -1, new ISnesNavigation.HistoryArgs {Description = "Goto"}, overshootAmount: standardOvershootAmount);
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
    
    private void GoToNextUnreachedBranchPoint(int offset)
    {
        // experimental.
        // jump to next instruction, marked "unknown" still, that is meets one of the following conditions:
        // 1. is an "in point" (meaning something known jumps to it), or
        // 2. is directly after a branch statement (e.g. it's a branch not yet taken) 
        
        // these are often small branches not taken during tracelog runs, 
        // and are easy targets for filling them in relatively risk-free since you know the M and X flags of
        // the instruction of where you jumped FROM.
        var snesData = Project.Data.GetSnesApi();
        if (snesData == null)
            return;
        
        var (foundOffset, iaSourceOffsetPc) = snesData.FindNextUnreachedBranchPointAfter(offset);
        if (foundOffset == -1)
        {
            ShowInfo("Can't jump to next unreached InPoint (none found)", "Woops");
            return;
        }

        // now, if we want to get real fancy.... copy the MX flags from the previous place to here so when we step from here, it works.

        // less aggressive way:
        // if (iaSource != -1)
        // {
        //     snesData.SetMxFlags(iaSource, snesData.GetMxFlags(iaSource));
        // }
        // SelectOffset(foundOffset, -1);

        // more aggressive way (fine, unless your source data is a lie)
        // do this instead of SelectOffset so we copy the MX flags from the previous location to here.
        //if (iaSource != -1)
        //    StepIn(iaSource);
        
        if (iaSourceOffsetPc != -1)
        {
            // in this case,
            // iaSourceOffsetPc is where we came FROM           (the originating BRA, BEQ, JSR, etc)
            // foundOffset is where we are jumped/branched TO,  (where we were branched to, the destination)

            // this below actions are vaguely doing what Step() does without marking the bytes we're on as opcode+operands.
            // we'll set the flags and such but, leave the actual marking to be an explicit decision+action by the user
            // after all, we can get weird false positives. let the user make this choice
            var (opcode, directPage, dataBank, xFlag, mFlag) =
                snesData.GetCpuStateFor(iaSourceOffsetPc, -1);

            // set the place we're GOING to with the jump point's MX flags
            snesData.SetDataBank(foundOffset, dataBank);
            snesData.SetDirectPage(foundOffset, directPage);
            snesData.SetXFlag(foundOffset, xFlag);
            snesData.SetMFlag(foundOffset, mFlag);

            // very optional. just to create an entry in the history.
            MarkHistoryPoint(iaSourceOffsetPc,
                new ISnesNavigation.HistoryArgs { Description = "Find next unreached: branch origin" }, "origin");
        }
        else
        {
            // in this case,
            // iaSourceOffsetPc is always -1
            // foundOffset is the address of what we suspect is the very next instruction for a conditional branch or JSR/JSL
            //              IF it WASN'T taken
            
            // we shouldn't need to set any flags/etc, just position the user in the right spot so they can hit the "Step" button
            // and keep going.
            
            // very optional. just to create an entry in the history.
            MarkHistoryPoint(foundOffset,
                new ISnesNavigation.HistoryArgs { Description = "Find next unreached: untaken branch point" }, "origin");
        }

        // now, set the real position
        SelectOffset(foundOffset, -1, new ISnesNavigation.HistoryArgs { Description = "Find next unreached branch point" }, overshootAmount: standardOvershootAmount);
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
    
    private void UiFixMisalignedInstructions()
    {
        if (!PromptForMisalignmentCheck())
            return;

        var countModified = ProjectController.FixMisalignedFlags();
        
        RefreshUi();
        ShowInfo($"Modified {countModified} flags!", "Done!");
    }

    private void UiRescanForInOut()
    {
        if (!PromptForInOutChecking())
            return;

        if (!ProjectController.RescanForInOut()) 
            return;
            
        RefreshUi();
        ShowInfo("Scan complete!", "Done!");
    }

    private bool SaveProject(bool askFilenameIfNotSet = true, bool alwaysAsk = false)
    {
        var showPrompt = 
            askFilenameIfNotSet && string.IsNullOrEmpty(Project.ProjectFileName) || 
            alwaysAsk;

        var promptedFilename = "";
        if (showPrompt)
        {
            saveProjectFile.InitialDirectory = Project.AttachedRomFilename;
            
            // if it doesn't already have a filename set, make a reasonable guess based on the ROM
            if (string.IsNullOrEmpty(Project.ProjectFileName))
                saveProjectFile.FileName = Path.GetFileNameWithoutExtension(Project.AttachedRomFilename) + ".diz";
            
            if (saveProjectFile.ShowDialog() != DialogResult.OK || string.IsNullOrEmpty(saveProjectFile.FileName))
                return false;

            promptedFilename = saveProjectFile.FileName;
        }

        var origFilename = Project.ProjectFileName;
        if (!string.IsNullOrEmpty(promptedFilename))
            Project.ProjectFileName = promptedFilename;

        var err = ProjectController.SaveProject(Project.ProjectFileName);

        if (err == null) 
            return true;
            
        Project.ProjectFileName = origFilename;
        ShowError($"Couldn't save: {err}");
        return false;
    }

    private void ShowVisualizerForm()
    {
        visualForm ??= new VisualizerForm(this);
        visualForm.Show();
    }

    private void ShowCommentList()
    {
        aliasList.Show();
        aliasList.BringFormToTop();
    }

    private void ShowProjectSettings()
    {
        // this property grid is generic and can display anything. here we'll use it to show the project settings.
        var propertyEditorForm = new GenericPropertyEditorForm(ProjectController.Project.ProjectSettings);
        propertyEditorForm.ShowDialog();
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

    private readonly IDizAppSettings appSettings; 

    private void ToggleOpenLastProjectEnabled()
    {
        appSettings.OpenLastFileAutomatically = openLastProjectAutomaticallyToolStripMenuItem.Checked;
        UpdateUiFromSettings();
    }
}
﻿using System;
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
using DiztinGUIsh.Properties;

namespace DiztinGUIsh.window;

public partial class MainWindow
{
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
        SelectOffset(newOffset, -1, new ISnesNavigation.HistoryArgs {Description = "Step Over"}, overshootAmount: 20);
        UpdateUi_TimerAndPercent();
    }

    private void StepIn(int offset)
    {
        if (!RomDataPresent()) 
            return;
            
        ProjectController.MarkChanged();
        var newOffset = Project.Data.GetSnesApi().Step(offset, true, false, offset - 1);
        SelectOffset(newOffset, -1, new ISnesNavigation.HistoryArgs {Description = "Step Into"}, overshootAmount: 20);
        UpdateUi_TimerAndPercent();
    }

    private void AutoStepSafe(int offset)
    {
        if (!RomDataPresent()) 
            return;
            
        ProjectController.MarkChanged();
        var destination = Project.Data.GetSnesApi().AutoStepSafe(offset);
        if (moveWithStep) 
            SelectOffset(destination, -1, new ISnesNavigation.HistoryArgs {Description = "AutoStep (Safe)"}, overshootAmount: 20);
            
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
            SelectOffset(destination, -1, new ISnesNavigation.HistoryArgs {Description = "AutoStep (Harsh)"}, overshootAmount:20);

        UpdateUi_TimerAndPercent();
    }

    private void Mark(int offset)
    {
        if (!RomDataPresent()) 
            return;
            
        ProjectController.MarkChanged();
        var newOffset = Project.Data.GetSnesApi().MarkTypeFlag(offset, markFlag, RomUtil.GetByteLengthForFlag(markFlag));
            
        SelectOffset(newOffset, -1, new ISnesNavigation.HistoryArgs {Description = "Mark (single)"}, overshootAmount: 20);
            
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
            SelectOffset(offset, -1, new ISnesNavigation.HistoryArgs {Description = "Goto"}, overshootAmount: 20);
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
    
    private void GoToNextUnreachedInPoint(int offset)
    {
        // experimental. jump to next instruction that is an in point (something known jumps to it)
        // AND is also marked as "unknown"
        // these are often small branches not taken during tracelog runs, 
        // and are easy targets for filling them in relatively risk-free since you know the M and X flags of
        // the instruction of where you jumped FROM.
        var snesData = Project.Data.GetSnesApi();
        if (snesData == null)
            return;
        
        var (foundOffset, iaSourceOffsetPc) = snesData.FindNextUnreachedInPointAfter(offset);
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
            // this is vaguely doing what Step() does without marking the bytes we're on as opcode+operands.
            // we'll set the flags and such but, leave the actual marking to be an explicit action by the user
            var (opcode, directPage, dataBank, xFlag, mFlag) =
                snesData.GetCpuStateFor(iaSourceOffsetPc, -1);

            // set the place we're GOING to with the jump point's MX flags
            snesData.SetDataBank(foundOffset, dataBank);
            snesData.SetDirectPage(foundOffset, directPage);
            snesData.SetXFlag(foundOffset, xFlag);
            snesData.SetMFlag(foundOffset, mFlag);

            // very optional. just to create an entry in the history.
            // SelectOffset(iaSource, -1, new ISnesNavigation.HistoryArgs {Description = "Find next unreached: origin point"});
            MarkHistoryPoint(iaSourceOffsetPc,
                new ISnesNavigation.HistoryArgs { Description = "Find next unreached: branch origin" }, "origin");
        }

        // now do the real thing
        SelectOffset(foundOffset, -1, new ISnesNavigation.HistoryArgs { Description = "Find next unreached in-point" }, overshootAmount: 20);
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

    private void ShowCommentList() => aliasList.Show();

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
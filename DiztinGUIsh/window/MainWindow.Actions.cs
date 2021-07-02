using Diz.Core.model;
using Diz.Core.util;
using DiztinGUIsh.Properties;

namespace DiztinGUIsh.window
{
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
        
        private void ExportAssembly()
        {
            var adjustedSettings = PromptForExportSettingsAndConfirmation();
            if (!adjustedSettings.HasValue)
                return;

            ProjectController.UpdateExportSettings(adjustedSettings.Value);
            ProjectController.WriteAssemblyOutput();
        }


        private void Step(int offset)
        {
            if (!RomDataPresent()) 
                return;
            
            ProjectController.MarkChanged();
            SelectOffset(Project.Data.Step(offset, false, false, offset - 1));
            UpdateUI_Tmp3();
        }

        private void StepIn(int offset)
        {
            if (!RomDataPresent()) 
                return;
            
            ProjectController.MarkChanged();
            SelectOffset(Project.Data.Step(offset, true, false, offset - 1));
            UpdateUI_Tmp3();
        }

        private void AutoStepSafe(int offset)
        {
            if (!RomDataPresent()) 
                return;
            
            ProjectController.MarkChanged();
            var destination = Project.Data.AutoStep(offset, false, 0);
            if (moveWithStep) 
                SelectOffset(destination);
            
            UpdateUI_Tmp3();
        }

        private void AutoStepHarsh(int offset)
        {
            if (!RomDataPresent()) 
                return;
            
            if (!PromptHarshAutoStep(offset, out var newOffset, out var count))
                return;

            ProjectController.MarkChanged();
            var destination = Project.Data.AutoStep(newOffset, true, count);
            
            if (moveWithStep) 
                SelectOffset(destination);

            UpdateUI_Tmp3();
        }

        private void Mark(int offset)
        {
            if (!RomDataPresent()) 
                return;
            
            ProjectController.MarkChanged();
            var newOffset = Project.Data.MarkTypeFlag(offset, markFlag, RomUtil.GetByteLengthForFlag(markFlag));
            
            SelectOffset(newOffset);
            
            UpdateUI_Tmp3();
        }

        private int _lastMarkPropertyIndex = -1;

        private void MarkMany(int offset, int column)
        {
            if (!RomDataPresent()) 
                return;
            
            var mark = PromptMarkMany(offset, column, _lastMarkPropertyIndex);
            if (mark == null)
                return;

            MarkMany(mark.PropertyIndex, mark.Start, mark.Value, mark.Count);
            _lastMarkPropertyIndex = mark.PropertyIndex;

            UpdateSomeUI2();
        }

        private void MarkMany(int markProperty, int markStart, object markAs, int markCount)
        {
            // need to rework markAs and markProperty, this works but it's view-dependent
            // and fragile as hell.
            
            var destination = markProperty switch
            {
                0 => Project.Data.MarkTypeFlag(markStart, (FlagType) markAs, markCount),
                1 => Project.Data.MarkDataBank(markStart, (int) markAs, markCount),
                2 => Project.Data.MarkDirectPage(markStart, (int) markAs, markCount),
                3 => Project.Data.MarkMFlag(markStart, (bool) markAs, markCount),
                4 => Project.Data.MarkXFlag(markStart, (bool) markAs, markCount),
                5 => Project.Data.MarkArchitecture(markStart, (Architecture) markAs, markCount),
                _ => 0
            };

            ProjectController.MarkChanged();

            if (moveWithStep)
                SelectOffset(destination);
        }

        private void GoToIntermediateAddress(int offset)
        {
            var snesOffset = FindIntermediateAddress(offset);
            if (snesOffset == -1)
                return;
            
            SelectOffset(snesOffset, 1);
        }

        private void GoTo(int offset)
        {
            if (IsOffsetInRange(offset))
                SelectOffset(offset);
            else
                ShowOffsetOutOfRangeMsg();
        }

        private void GoToUnreached(bool end, bool direction)
        {
            if (!FindUnreached(SelectedOffset, end, direction, out var unreached))
                return;
            
            SelectOffset(unreached, 1);
        }


        private void FixMisalignedInstructions()
        {
            if (!PromptForMisalignmentCheck())
                return;

            var count = Project.Data.FixMisalignedFlags();

            if (count > 0)
                ProjectController.MarkChanged();
            InvalidateTable();
            
            ShowInfo($"Modified {count} flags!", "Done!");
        }

        private void RescanForInOut()
        {
            if (!PromptForInOutChecking()) 
                return;

            Project.Data.RescanInOutPoints();
            ProjectController.MarkChanged();
            
            InvalidateTable();
            ShowInfo("Scan complete!", "Done!");
        }

        private void SaveProject()
        {
            ProjectController.SaveProject(Project.ProjectFileName);
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
}
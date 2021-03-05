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
            RefreshPercentAndWindowTitle();
        }

        private void StepIn(int offset)
        {
            if (!RomDataPresent()) 
                return;
            
            ProjectController.MarkChanged();
            SelectOffset(Project.Data.Step(offset, true, false, offset - 1));
            RefreshPercentAndWindowTitle();
        }

        private void AutoStepSafe(int offset)
        {
            if (!RomDataPresent()) 
                return;
            
            ProjectController.MarkChanged();
            var destination = Project.Data.AutoStep(offset, false, 0);
            if (moveWithStep) 
                SelectOffset(destination);
            
            RefreshPercentAndWindowTitle();
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

            RefreshPercentAndWindowTitle();
        }

        private void Mark(int offset)
        {
            if (!RomDataPresent()) 
                return;

            var newOffset = ProjectController.MarkTypeFlag(offset, markFlag, RomUtil.GetByteLengthForFlag(markFlag));

            SelectOffset(newOffset);
            
            RefreshPercentAndWindowTitle();
        }

        private void MarkMany(int offset, int column)
        {
            if (!RomDataPresent()) 
                return;
            
            var mark = PromptMarkMany(offset, column);
            if (mark == null)
                return;

            var destination = ProjectController.MarkMany(mark.Property, mark.Start, mark.Value, mark.Count);

            if (moveWithStep)
                SelectOffset(destination);

            RefreshTablePercentAndWindowTitle();
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

        private void SetMarkerLabel(Data.FlagType flagType)
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
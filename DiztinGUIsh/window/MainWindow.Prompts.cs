using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Diz.Controllers.controllers;
using Diz.Core.commands;
using Diz.Core.export;
using DiztinGUIsh.controller;
using DiztinGUIsh.window.dialog;

namespace DiztinGUIsh.window
{
    public partial class MainWindow
    {
        private bool PromptContinueEvenIfUnsavedChanges()
        {
            if (Project == null || !Project.UnsavedChanges)
                return true;

            return DialogResult.OK == MessageBox.Show(
                "You have unsaved changes. They will be lost if you continue.",
                "Unsaved Changes", MessageBoxButtons.OKCancel);
        }

        private string PromptForOpenFilename()
        {
            // TODO: combine with another function here that does similar
            openFileDialog.InitialDirectory = Project?.ProjectFileName ?? "";
            return openFileDialog.ShowDialog() == DialogResult.OK ? openFileDialog.FileName : "";
        }

        private static void ShowExportResults(LogCreator.OutputResult result)
        {
            if (result.ErrorCount > 0)
                MessageBox.Show("Disassembly created with errors. See errors.txt for details.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
                MessageBox.Show("Disassembly created successfully!", "Complete", MessageBoxButtons.OK,
                    MessageBoxIcon.Asterisk);
        }

        private void PromptForFilenameToSave()
        {
            saveProjectFile.InitialDirectory = Project.AttachedRomFilename;
            if (saveProjectFile.ShowDialog() == DialogResult.OK && saveProjectFile.FileName != "")
            {
                ProjectController.SaveProject(saveProjectFile.FileName);
            }
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
            try
            {
                System.Diagnostics.Process.Start(Directory.GetCurrentDirectory() + "/help.html");
            }
            catch (Exception)
            {
                MessageBox.Show("Can't find the help file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void githubToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/Dotsarecool/DiztinGUIsh");
        }

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

        private Dictionary<MarkCommand.MarkManyProperty, object> SavedMarkManySettings { get; set; } = new Dictionary<MarkCommand.MarkManyProperty, object>();
        
        private IMarkManyController CreateMarkManyController(int offset, MarkCommand.MarkManyProperty property)
        {
            // NOTE: in upstream 3.0 branch, replace this with dependency injection
            var view = new MarkManyView();
            var markManyController = new MarkManyController(offset, property, Project.Data, view)
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

        public void OnProjectOpenWarning(string warningMsg)
        {
            MessageBox.Show(warningMsg, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
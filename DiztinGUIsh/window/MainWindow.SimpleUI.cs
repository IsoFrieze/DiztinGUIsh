using System;
using System.Windows.Forms;
using Diz.Core.model;
using Diz.Core.util;
using DiztinGUIsh.window.dialog;

namespace DiztinGUIsh.window
{
    public partial class MainWindow
    {
        private void MainWindow_FormClosing(object sender, FormClosingEventArgs e) =>
            e.Cancel = !PromptContinueEvenIfUnsavedChanges();

        private void MainWindow_SizeChanged(object sender, EventArgs e) => UpdatePanels();
        private void MainWindow_ResizeEnd(object sender, EventArgs e) => UpdateDataGridView();
        private void MainWindow_Load(object sender, EventArgs e) => Init();
        private void newProjectToolStripMenuItem_Click(object sender, EventArgs e) => CreateNewProject();
        private void openProjectToolStripMenuItem_Click(object sender, EventArgs e) => OpenProject();

        private void saveProjectToolStripMenuItem_Click(object sender, EventArgs e) => SaveProject();

        private void saveProjectAsToolStripMenuItem_Click(object sender, EventArgs e) => PromptForFilenameToSave();
        private void exportLogToolStripMenuItem_Click(object sender, EventArgs e) => ExportAssembly();
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e) => new About().ShowDialog();
        private void exitToolStripMenuItem_Click(object sender, EventArgs e) => Application.Exit();
        
        private void decimalToolStripMenuItem_Click(object sender, EventArgs e) => 
            UpdateBase(Util.NumberBase.Decimal);

        private void hexadecimalToolStripMenuItem_Click(object sender, EventArgs e) =>
            UpdateBase(Util.NumberBase.Hexadecimal);

        private void binaryToolStripMenuItem_Click(object sender, EventArgs e) => 
            UpdateBase(Util.NumberBase.Binary);
        
        private void importTraceLogBinary_Click(object sender, EventArgs e) => ImportBsnesBinaryTraceLog();
        private void addLabelToolStripMenuItem_Click(object sender, EventArgs e) => BeginAddingLabel();
        private void visualMapToolStripMenuItem_Click(object sender, EventArgs e) => ShowVisualizerForm();
        private void stepOverToolStripMenuItem_Click(object sender, EventArgs e) => Step(SelectedOffset);
        private void stepInToolStripMenuItem_Click(object sender, EventArgs e) => StepIn(SelectedOffset);
        private void autoStepSafeToolStripMenuItem_Click(object sender, EventArgs e) => AutoStepSafe(SelectedOffset);
        private void autoStepHarshToolStripMenuItem_Click(object sender, EventArgs e) => AutoStepHarsh(SelectedOffset);
        private void gotoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var gotoOffset = PromptForGotoOffset();
            if (gotoOffset != -1)
                GoTo(gotoOffset);
        }

        private void gotoIntermediateAddressToolStripMenuItem_Click(object sender, EventArgs e) =>
            GoToIntermediateAddress(SelectedOffset);

        private void gotoFirstUnreachedToolStripMenuItem_Click(object sender, EventArgs e) => 
            GoToUnreached(true, true);

        private void gotoNearUnreachedToolStripMenuItem_Click(object sender, EventArgs e) =>
            GoToUnreached(false, false);

        private void gotoNextUnreachedToolStripMenuItem_Click(object sender, EventArgs e) => 
            GoToUnreached(false, true);
        
        private void markOneToolStripMenuItem_Click(object sender, EventArgs e) => Mark(SelectedOffset);
        private void markManyToolStripMenuItem_Click(object sender, EventArgs e) => MarkMany(SelectedOffset, 7);
        private void setDataBankToolStripMenuItem_Click(object sender, EventArgs e) => MarkMany(SelectedOffset, 8);
        private void setDirectPageToolStripMenuItem_Click(object sender, EventArgs e) => MarkMany(SelectedOffset, 9);

        private void toggleAccumulatorSizeMToolStripMenuItem_Click(object sender, EventArgs e) => MarkMany(SelectedOffset, 10);

        private void toggleIndexSizeToolStripMenuItem_Click(object sender, EventArgs e) => MarkMany(SelectedOffset, 11);
        private void addCommentToolStripMenuItem_Click(object sender, EventArgs e) => BeginEditingComment();

        private void unreachedToolStripMenuItem_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Unreached);

        private void opcodeToolStripMenuItem_Click(object sender, EventArgs e) => SetMarkerLabel(FlagType.Opcode);

        private void operandToolStripMenuItem_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Operand);

        private void bitDataToolStripMenuItem_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Data8Bit);

        private void graphicsToolStripMenuItem_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Graphics);

        private void musicToolStripMenuItem_Click(object sender, EventArgs e) => SetMarkerLabel(FlagType.Music);
        private void emptyToolStripMenuItem_Click(object sender, EventArgs e) => SetMarkerLabel(FlagType.Empty);

        private void bitDataToolStripMenuItem1_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Data16Bit);

        private void wordPointerToolStripMenuItem_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Pointer16Bit);

        private void bitDataToolStripMenuItem2_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Data24Bit);

        private void longPointerToolStripMenuItem_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Pointer24Bit);

        private void bitDataToolStripMenuItem3_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Data32Bit);

        private void dWordPointerToolStripMenuItem_Click(object sender, EventArgs e) =>
            SetMarkerLabel(FlagType.Pointer32Bit);

        private void textToolStripMenuItem_Click(object sender, EventArgs e) => SetMarkerLabel(FlagType.Text);

        private void fixMisalignedInstructionsToolStripMenuItem_Click(object sender, EventArgs e) =>
            FixMisalignedInstructions();

        private void moveWithStepToolStripMenuItem_Click(object sender, EventArgs e) => ToggleMoveWithStep();
        private void labelListToolStripMenuItem_Click(object sender, EventArgs e) => ShowCommentList();

        private void openLastProjectAutomaticallyToolStripMenuItem_Click(object sender, EventArgs e) =>
            ToggleOpenLastProjectEnabled();

        private void closeProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
        }

        private void importCDLToolStripMenuItem_Click_1(object sender, EventArgs e) => ImportBizhawkCDL();

        private void importBsnesTracelogText_Click(object sender, EventArgs e) => ImportBsnesTraceLogText();

        private void graphicsWindowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO
            // graphics view window
        }

        private void toolStripOpenLast_Click(object sender, EventArgs e)
        {
            OpenLastProject();
        }

        private void rescanForInOutPointsToolStripMenuItem_Click(object sender, EventArgs e) => RescanForInOut();
        private void importUsageMapToolStripMenuItem_Click_1(object sender, EventArgs e) => ImportBSNESUsageMap();
        private void table_MouseWheel(object sender, MouseEventArgs e) => ScrollTableBy(e.Delta);

        public NavigationForm NavigationForm { get; }

        private void showHistoryToolStripMenuItem_Click(object sender, System.EventArgs e)
        {
            if (!NavigationForm.Visible)
                NavigationForm.Show();
            else
                NavigationForm.BringToFront();
        }

        private void goBackToolStripMenuItem_Click(object sender, System.EventArgs e) => 
            NavigationForm.Navigate(forwardDirection: false);

        private void goForwardToolStripMenuItem_Click(object sender, System.EventArgs e) => 
            NavigationForm.Navigate(forwardDirection: true);
    }
}
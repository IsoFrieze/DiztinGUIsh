using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace DiztinGUIsh.window.dialog
{
    partial class LogCreatorSettingsEditorForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LogCreatorSettingsEditorForm));
            this.cancel = new System.Windows.Forms.Button();
            this.disassembleButton = new System.Windows.Forms.Button();
            this.textFormat = new System.Windows.Forms.TextBox();
            this.textSample = new System.Windows.Forms.TextBox();
            this.comboStructure = new System.Windows.Forms.ComboBox();
            this.comboUnlabeled = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.numData = new System.Windows.Forms.NumericUpDown();
            this.chkNewLine = new System.Windows.Forms.CheckBox();
            this.chkOutputExtraWhitespace = new System.Windows.Forms.CheckBox();
            this.chkGenerateFullLine = new System.Windows.Forms.CheckBox();
            this.chkPrintLabelSpecificComments = new System.Windows.Forms.CheckBox();
            this.chkIncludeUnusedLabels = new System.Windows.Forms.CheckBox();            
            this.saveLogSingleFile = new System.Windows.Forms.SaveFileDialog();
            this.chooseLogFolder = new System.Windows.Forms.FolderBrowserDialog();
            this.label7 = new System.Windows.Forms.Label();
            this.txtExportPath = new System.Windows.Forms.TextBox();
            this.btnBrowseOutputPath = new System.Windows.Forms.Button();
            this.label8 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.numData)).BeginInit();
            this.SuspendLayout();
            // 
            // cancel
            // 
            this.cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancel.Location = new System.Drawing.Point(14, 613);
            this.cancel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.cancel.Name = "cancel";
            this.cancel.Size = new System.Drawing.Size(88, 27);
            this.cancel.TabIndex = 20;
            this.cancel.TabStop = false;
            this.cancel.Text = "Cancel";
            this.cancel.UseVisualStyleBackColor = true;
            this.cancel.Click += new System.EventHandler(this.cancel_Click);
            // 
            // disassembleButton
            // 
            this.disassembleButton.Location = new System.Drawing.Point(762, 613);
            this.disassembleButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.disassembleButton.Name = "disassembleButton";
            this.disassembleButton.Size = new System.Drawing.Size(132, 27);
            this.disassembleButton.TabIndex = 21;
            this.disassembleButton.Text = "Start Export!";
            this.disassembleButton.UseVisualStyleBackColor = true;
            this.disassembleButton.Click += new System.EventHandler(this.disassembleButton_Click);
            // 
            // textFormat
            // 
            this.textFormat.Location = new System.Drawing.Point(103, 194);
            this.textFormat.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.textFormat.Name = "textFormat";
            this.textFormat.Size = new System.Drawing.Size(791, 23);
            this.textFormat.TabIndex = 17;
            this.textFormat.TextChanged += new System.EventHandler(this.textFormat_TextChanged);
            // 
            // textSample
            // 
            this.textSample.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.textSample.Location = new System.Drawing.Point(13, 249);
            this.textSample.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.textSample.Multiline = true;
            this.textSample.Name = "textSample";
            this.textSample.ReadOnly = true;
            this.textSample.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.textSample.Size = new System.Drawing.Size(881, 358);
            this.textSample.TabIndex = 19;
            this.textSample.TabStop = false;
            this.textSample.WordWrap = false;
            // 
            // comboStructure
            // 
            this.comboStructure.FormattingEnabled = true;
            this.comboStructure.Items.AddRange(new object[] {
            "All in one file",
            "One bank per file"});
            this.comboStructure.Location = new System.Drawing.Point(754, 39);
            this.comboStructure.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.comboStructure.Name = "comboStructure";
            this.comboStructure.Size = new System.Drawing.Size(140, 23);
            this.comboStructure.TabIndex = 4;
            this.comboStructure.SelectedIndexChanged += new System.EventHandler(this.comboStructure_SelectedIndexChanged);
            // 
            // comboUnlabeled
            // 
            this.comboUnlabeled.FormattingEnabled = true;
            this.comboUnlabeled.Items.AddRange(new object[] {
            "Create All",
            "In points only",
            "None"});
            this.comboUnlabeled.Location = new System.Drawing.Point(754, 12);
            this.comboUnlabeled.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.comboUnlabeled.Name = "comboUnlabeled";
            this.comboUnlabeled.Size = new System.Drawing.Size(140, 23);
            this.comboUnlabeled.TabIndex = 2;
            this.comboUnlabeled.SelectedIndexChanged += new System.EventHandler(this.comboUnlabeled_SelectedIndexChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(8, 197);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(87, 15);
            this.label1.TabIndex = 16;
            this.label1.Text = "Output format:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(8, 231);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(90, 15);
            this.label2.TabIndex = 18;
            this.label2.Text = "Sample Output:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(613, 69);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(132, 15);
            this.label3.TabIndex = 5;
            this.label3.Text = "Max data bytes per line:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(659, 42);
            this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(86, 15);
            this.label4.TabIndex = 3;
            this.label4.Text = "Bank structure:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(619, 15);
            this.label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(128, 15);
            this.label5.TabIndex = 1;
            this.label5.Text = "Unlabeled instructions:";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(14, 15);
            this.label6.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(334, 75);
            this.label6.TabIndex = 0;
            this.label6.Text = resources.GetString("label6.Text");
            // 
            // numData
            // 
            this.numData.Location = new System.Drawing.Point(753, 67);
            this.numData.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.numData.Maximum = new decimal(new int[] {
            16,
            0,
            0,
            0});
            this.numData.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numData.Name = "numData";
            this.numData.Size = new System.Drawing.Size(44, 23);
            this.numData.TabIndex = 6;
            this.numData.Value = new decimal(new int[] {
            8,
            0,
            0,
            0});
            this.numData.ValueChanged += new System.EventHandler(this.numData_ValueChanged);
                        // 
            // chkNewLine
            // 
            this.chkNewLine.AutoSize = true;
            this.chkNewLine.Checked = false;
            this.chkNewLine.Location = new System.Drawing.Point(103, 97);
            this.chkNewLine.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.chkNewLine.Name = "chkNewLine";
            this.chkNewLine.Size = new System.Drawing.Size(211, 19);
            this.chkNewLine.TabIndex = 7;
            this.chkNewLine.Text = "Print labels on their own lines";
            this.chkNewLine.UseVisualStyleBackColor = true;
            this.chkNewLine.CheckedChanged += new System.EventHandler(this.chkNewLine_CheckedChanged);
            // 
            // chkOutputExtraWhitespace
            // 
            this.chkOutputExtraWhitespace.AutoSize = true;
            this.chkOutputExtraWhitespace.Checked = true;
            this.chkOutputExtraWhitespace.Location = new System.Drawing.Point(359, 97);
            this.chkOutputExtraWhitespace.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.chkOutputExtraWhitespace.Name = "chkOutputExtraWhitespace";
            this.chkOutputExtraWhitespace.Size = new System.Drawing.Size(211, 19);
            this.chkOutputExtraWhitespace.TabIndex = 8;
            this.chkOutputExtraWhitespace.Text = "Output extra whitespace in assembly code";
            this.chkOutputExtraWhitespace.UseVisualStyleBackColor = true;
            this.chkOutputExtraWhitespace.CheckedChanged += new System.EventHandler(this.chkOutputExtraWhitespace_CheckedChanged);
            // 
            // chkGenerateFullLine
            // 
            this.chkGenerateFullLine.AutoSize = true;
            this.chkGenerateFullLine.Checked = true;
            this.chkGenerateFullLine.Location = new System.Drawing.Point(359, 114);
            this.chkGenerateFullLine.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.chkGenerateFullLine.Name = "chkGenerateFullLine";
            this.chkGenerateFullLine.Size = new System.Drawing.Size(211, 19);
            this.chkGenerateFullLine.TabIndex = 9;
            this.chkGenerateFullLine.Text = "Generate full line on special lines";
            this.chkGenerateFullLine.UseVisualStyleBackColor = true;
            this.chkGenerateFullLine.CheckedChanged += new System.EventHandler(this.chkGenerateFullLine_CheckedChanged);
            // 
            // chkPrintLabelSpecificComments
            // 
            this.chkPrintLabelSpecificComments.AutoSize = true;
            this.chkPrintLabelSpecificComments.Checked = true;
            this.chkPrintLabelSpecificComments.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkPrintLabelSpecificComments.Location = new System.Drawing.Point(613, 97);
            this.chkPrintLabelSpecificComments.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.chkPrintLabelSpecificComments.Name = "chkPrintLabelSpecificComments";
            this.chkPrintLabelSpecificComments.Size = new System.Drawing.Size(255, 19);
            this.chkPrintLabelSpecificComments.TabIndex = 10;
            this.chkPrintLabelSpecificComments.Text = "Print label-specific comments in labels.asm";
            this.chkPrintLabelSpecificComments.UseVisualStyleBackColor = true;
            this.chkPrintLabelSpecificComments.CheckedChanged += new System.EventHandler(this.chkPrintLabelSpecificComments_CheckedChanged);
            // 
            // chkIncludeUnusedLabels
            // 
            this.chkIncludeUnusedLabels.AutoSize = true;
            this.chkIncludeUnusedLabels.Location = new System.Drawing.Point(613, 114);
            this.chkIncludeUnusedLabels.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.chkIncludeUnusedLabels.Name = "chkIncludeUnusedLabels";
            this.chkIncludeUnusedLabels.Size = new System.Drawing.Size(211, 19);
            this.chkIncludeUnusedLabels.TabIndex = 11;
            this.chkIncludeUnusedLabels.Text = "Include unused labels in labels.asm";
            this.chkIncludeUnusedLabels.UseVisualStyleBackColor = true;
            this.chkIncludeUnusedLabels.CheckedChanged += new System.EventHandler(this.chkIncludeUnusedLabels_CheckedChanged);
            // 
            // saveLogSingleFile
            // 
            this.saveLogSingleFile.Filter = "Assembly Files|*.asm|All Files|*.*";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(13, 137);
            this.label7.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(57, 45);
            this.label7.TabIndex = 12;
            this.label7.Text = "Output \r\ndirectory \r\nor file:";
            // 
            // txtExportPath
            // 
            this.txtExportPath.Location = new System.Drawing.Point(103, 138);
            this.txtExportPath.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.txtExportPath.Name = "txtExportPath";
            this.txtExportPath.Size = new System.Drawing.Size(717, 23);
            this.txtExportPath.TabIndex = 13;
            this.txtExportPath.TextChanged += new System.EventHandler(this.txtExportPath_TextChanged);
            // 
            // btnBrowseOutputPath
            // 
            this.btnBrowseOutputPath.Location = new System.Drawing.Point(819, 138);
            this.btnBrowseOutputPath.Name = "btnBrowseOutputPath";
            this.btnBrowseOutputPath.Size = new System.Drawing.Size(75, 24);
            this.btnBrowseOutputPath.TabIndex = 14;
            this.btnBrowseOutputPath.Text = "Browse...";
            this.btnBrowseOutputPath.UseVisualStyleBackColor = true;
            this.btnBrowseOutputPath.Click += new System.EventHandler(this.btnBrowseOutputPath_Click);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(103, 164);
            this.label8.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(482, 15);
            this.label8.TabIndex = 15;
            this.label8.Text = "(By default, path is relative to the project file\'s directory. it will be created" +
    " if it doesn\'t exist)";
            // 
            // ExportDisassembly
            // 
            this.AcceptButton = this.disassembleButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancel;
            this.ClientSize = new System.Drawing.Size(907, 652);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.btnBrowseOutputPath);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.txtExportPath);
            this.Controls.Add(this.chkNewLine);
            this.Controls.Add(this.chkOutputExtraWhitespace);
            this.Controls.Add(this.chkGenerateFullLine);
            this.Controls.Add(this.chkIncludeUnusedLabels);
            this.Controls.Add(this.chkPrintLabelSpecificComments);
            this.Controls.Add(this.numData);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.comboUnlabeled);
            this.Controls.Add(this.comboStructure);
            this.Controls.Add(this.textSample);
            this.Controls.Add(this.textFormat);
            this.Controls.Add(this.disassembleButton);
            this.Controls.Add(this.cancel);
            this.Controls.Add(this.label6);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.Name = "LogCreatorSettingsEditorForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Export Disassembly";
            ((System.ComponentModel.ISupportInitialize)(this.numData)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Button cancel;
        private Button disassembleButton;
        private TextBox textFormat;
        private TextBox textSample;
        private ComboBox comboStructure;
        private ComboBox comboUnlabeled;
        private Label label1;
        private Label label2;
        private Label label3;
        private Label label4;
        private Label label5;
        private Label label6;
        private NumericUpDown numData;
        private CheckBox chkNewLine;
        private CheckBox chkOutputExtraWhitespace;
        private CheckBox chkGenerateFullLine;
        private CheckBox chkPrintLabelSpecificComments;
        private CheckBox chkIncludeUnusedLabels;
        private SaveFileDialog saveLogSingleFile;
        private FolderBrowserDialog chooseLogFolder;
        private Label label7;
        private TextBox txtExportPath;
        private Button btnBrowseOutputPath;
        private Label label8;
    }
}
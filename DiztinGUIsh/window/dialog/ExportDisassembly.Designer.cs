using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Diz.Core.export;

namespace DiztinGUIsh
{
    partial class ExportDisassembly
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
            ComponentResourceManager resources = new ComponentResourceManager(typeof(ExportDisassembly));
            this.cancel = new Button();
            this.disassembleButton = new Button();
            this.textFormat = new TextBox();
            this.textSample = new TextBox();
            this.comboStructure = new ComboBox();
            this.comboUnlabeled = new ComboBox();
            this.label1 = new Label();
            this.label2 = new Label();
            this.label3 = new Label();
            this.label4 = new Label();
            this.label5 = new Label();
            this.label6 = new Label();
            this.numData = new NumericUpDown();
            this.chkPrintLabelSpecificComments = new CheckBox();
            this.chkIncludeUnusedLabels = new CheckBox();
            this.saveLogSingleFile = new SaveFileDialog();
            this.chooseLogFolder = new FolderBrowserDialog();
            ((ISupportInitialize)(this.numData)).BeginInit();
            this.SuspendLayout();
            // 
            // cancel
            // 
            this.cancel.DialogResult = DialogResult.Cancel;
            this.cancel.Location = new Point(9, 491);
            this.cancel.Name = "cancel";
            this.cancel.Size = new Size(75, 23);
            this.cancel.TabIndex = 12;
            this.cancel.TabStop = false;
            this.cancel.Text = "Cancel";
            this.cancel.UseVisualStyleBackColor = true;
            this.cancel.Click += new EventHandler(this.cancel_Click);
            // 
            // button2
            // 
            this.disassembleButton.Location = new Point(487, 491);
            this.disassembleButton.Name = "disassembleButton";
            this.disassembleButton.Size = new Size(113, 23);
            this.disassembleButton.TabIndex = 11;
            this.disassembleButton.Text = "Disassemble";
            this.disassembleButton.UseVisualStyleBackColor = true;
            this.disassembleButton.Click += new EventHandler(this.disassembleButton_Click);
            // 
            // textFormat
            // 
            this.textFormat.Location = new Point(88, 139);
            this.textFormat.Name = "textFormat";
            this.textFormat.Size = new Size(512, 20);
            this.textFormat.TabIndex = 8;
            this.textFormat.TextChanged += new EventHandler(this.textFormat_TextChanged);
            // 
            // textSample
            // 
            this.textSample.Font = new Font("Courier New", 8.25F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0)));
            this.textSample.Location = new Point(88, 166);
            this.textSample.Multiline = true;
            this.textSample.Name = "textSample";
            this.textSample.ReadOnly = true;
            this.textSample.ScrollBars = ScrollBars.Both;
            this.textSample.Size = new Size(512, 319);
            this.textSample.TabIndex = 10;
            this.textSample.TabStop = false;
            this.textSample.WordWrap = false;
            // 
            // comboStructure
            // 
            this.comboStructure.FormattingEnabled = true;
            this.comboStructure.Items.AddRange(new object[] {
            "All in one file",
            "One bank per file"});
            this.comboStructure.Location = new Point(479, 39);
            this.comboStructure.Name = "comboStructure";
            this.comboStructure.Size = new Size(121, 21);
            this.comboStructure.TabIndex = 4;
            this.comboStructure.SelectedIndexChanged += new EventHandler(this.comboStructure_SelectedIndexChanged);
            // 
            // comboUnlabeled
            // 
            this.comboUnlabeled.FormattingEnabled = true;
            this.comboUnlabeled.Items.AddRange(new object[] {
            "Create All",
            "In points only",
            "None"});
            this.comboUnlabeled.Location = new Point(479, 12);
            this.comboUnlabeled.Name = "comboUnlabeled";
            this.comboUnlabeled.Size = new Size(121, 21);
            this.comboUnlabeled.TabIndex = 2;
            this.comboUnlabeled.SelectedIndexChanged += new EventHandler(this.comboUnlabeled_SelectedIndexChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new Point(12, 142);
            this.label1.Name = "label1";
            this.label1.Size = new Size(74, 13);
            this.label1.TabIndex = 7;
            this.label1.Text = "Output format:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new Point(6, 169);
            this.label2.Name = "label2";
            this.label2.Size = new Size(80, 13);
            this.label2.TabIndex = 9;
            this.label2.Text = "Sample Output:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new Point(441, 68);
            this.label3.Name = "label3";
            this.label3.Size = new Size(119, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "Max data bytes per line:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new Point(398, 42);
            this.label4.Name = "label4";
            this.label4.Size = new Size(79, 13);
            this.label4.TabIndex = 3;
            this.label4.Text = "Bank structure:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new Point(363, 15);
            this.label5.Name = "label5";
            this.label5.Size = new Size(114, 13);
            this.label5.TabIndex = 1;
            this.label5.Text = "Unlabeled instructions:";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new Point(12, 13);
            this.label6.Name = "label6";
            this.label6.Size = new Size(295, 65);
            this.label6.TabIndex = 0;
            this.label6.Text = resources.GetString("label6.Text");
            // 
            // numData
            // 
            this.numData.Location = new Point(562, 66);
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
            this.numData.Size = new Size(38, 20);
            this.numData.TabIndex = 6;
            this.numData.Value = new decimal(new int[] {
            8,
            0,
            0,
            0});
            this.numData.ValueChanged += new EventHandler(this.numData_ValueChanged);
            // 
            // chkPrintLabelSpecificComments
            // 
            this.chkPrintLabelSpecificComments.AutoSize = true;
            this.chkPrintLabelSpecificComments.Checked = true;
            this.chkPrintLabelSpecificComments.CheckState = CheckState.Checked;
            this.chkPrintLabelSpecificComments.Location = new Point(376, 93);
            this.chkPrintLabelSpecificComments.Name = "chkPrintLabelSpecificComments";
            this.chkPrintLabelSpecificComments.Size = new Size(225, 17);
            this.chkPrintLabelSpecificComments.TabIndex = 13;
            this.chkPrintLabelSpecificComments.Text = "Print label-specific comments in labels.asm";
            this.chkPrintLabelSpecificComments.UseVisualStyleBackColor = true;
            this.chkPrintLabelSpecificComments.CheckedChanged += new EventHandler(this.chkPrintLabelSpecificComments_CheckedChanged);
            // 
            // chkIncludeUnusedLabels
            // 
            this.chkIncludeUnusedLabels.AutoSize = true;
            this.chkIncludeUnusedLabels.Location = new Point(376, 115);
            this.chkIncludeUnusedLabels.Name = "chkIncludeUnusedLabels";
            this.chkIncludeUnusedLabels.Size = new Size(192, 17);
            this.chkIncludeUnusedLabels.TabIndex = 14;
            this.chkIncludeUnusedLabels.Text = "Include unused labels in labels.asm";
            this.chkIncludeUnusedLabels.UseVisualStyleBackColor = true;
            this.chkIncludeUnusedLabels.CheckedChanged += new EventHandler(this.chkIncludeUnusedLabels_CheckedChanged);
            // 
            // saveLogSingleFile
            // 
            this.saveLogSingleFile.Filter = "Assembly Files|*.asm|All Files|*.*";
            // 
            // ExportDisassembly
            // 
            this.AcceptButton = this.disassembleButton;
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.CancelButton = this.cancel;
            this.ClientSize = new Size(611, 525);
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
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.Name = "ExportDisassembly";
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Export Disassembly";
            ((ISupportInitialize)(this.numData)).EndInit();
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
        private CheckBox chkPrintLabelSpecificComments;
        private CheckBox chkIncludeUnusedLabels;
        private SaveFileDialog saveLogSingleFile;
        private FolderBrowserDialog chooseLogFolder;
    }
}
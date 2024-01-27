﻿namespace DiztinGUIsh.window.dialog
{
    partial class BsnesTraceLogBinaryMonitorForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BsnesTraceLogBinaryMonitorForm));
            this.btnStart = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.lblQueueSize = new System.Windows.Forms.Label();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.btnFinish = new System.Windows.Forms.Button();
            this.lblTotalProcessed = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.lblStatus = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.lblNumberModified = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.lblModifiedXFlags = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.lblModifiedMFlags = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.lblModifiedDPs = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.lblModifiedDBs = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.lblModifiedFlags = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.label16 = new System.Windows.Forms.Label();
            this.label17 = new System.Windows.Forms.Label();
            this.lblResultStatus = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.pictureGreenSpinner = new System.Windows.Forms.PictureBox();
            this.button1 = new System.Windows.Forms.Button();
            this.chkAddTLComments = new System.Windows.Forms.CheckBox();
            this.chkRemoveTLComments = new System.Windows.Forms.CheckBox();
            this.txtTracelogComment = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.chkCaptureLabelsOnly = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureGreenSpinner)).BeginInit();
            this.SuspendLayout();
            // 
            // btnStart
            // 
            this.btnStart.Location = new System.Drawing.Point(558, 14);
            this.btnStart.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(75, 44);
            this.btnStart.TabIndex = 0;
            this.btnStart.Text = "Start Capture";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(18, 51);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(64, 15);
            this.label1.TabIndex = 1;
            this.label1.Text = "Queue size";
            // 
            // lblQueueSize
            // 
            this.lblQueueSize.AutoSize = true;
            this.lblQueueSize.Location = new System.Drawing.Point(174, 51);
            this.lblQueueSize.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblQueueSize.Name = "lblQueueSize";
            this.lblQueueSize.Size = new System.Drawing.Size(17, 15);
            this.lblQueueSize.TabIndex = 2;
            this.lblQueueSize.Text = "--";
            // 
            // timer1
            // 
            this.timer1.Interval = 250;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // btnFinish
            // 
            this.btnFinish.Enabled = false;
            this.btnFinish.Location = new System.Drawing.Point(482, 14);
            this.btnFinish.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.btnFinish.Name = "btnFinish";
            this.btnFinish.Size = new System.Drawing.Size(69, 44);
            this.btnFinish.TabIndex = 3;
            this.btnFinish.Text = "Stop Capture";
            this.btnFinish.UseVisualStyleBackColor = true;
            this.btnFinish.Click += new System.EventHandler(this.btnFinish_Click);
            // 
            // lblTotalProcessed
            // 
            this.lblTotalProcessed.AutoSize = true;
            this.lblTotalProcessed.Location = new System.Drawing.Point(446, 228);
            this.lblTotalProcessed.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblTotalProcessed.Name = "lblTotalProcessed";
            this.lblTotalProcessed.Size = new System.Drawing.Size(17, 15);
            this.lblTotalProcessed.TabIndex = 6;
            this.lblTotalProcessed.Text = "--";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(300, 228);
            this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(130, 15);
            this.label4.TabIndex = 5;
            this.label4.Text = "# Instructions Analyzed";
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(174, 28);
            this.lblStatus.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(17, 15);
            this.lblStatus.TabIndex = 8;
            this.lblStatus.Text = "--";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(15, 28);
            this.label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(39, 15);
            this.label5.TabIndex = 7;
            this.label5.Text = "Status";
            // 
            // lblNumberModified
            // 
            this.lblNumberModified.AutoSize = true;
            this.lblNumberModified.Location = new System.Drawing.Point(446, 257);
            this.lblNumberModified.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblNumberModified.Name = "lblNumberModified";
            this.lblNumberModified.Size = new System.Drawing.Size(17, 15);
            this.lblNumberModified.TabIndex = 10;
            this.lblNumberModified.Text = "--";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(300, 257);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(130, 15);
            this.label3.TabIndex = 9;
            this.label3.Text = "# Instructions Modified";
            // 
            // lblModifiedXFlags
            // 
            this.lblModifiedXFlags.AutoSize = true;
            this.lblModifiedXFlags.Location = new System.Drawing.Point(174, 136);
            this.lblModifiedXFlags.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblModifiedXFlags.Name = "lblModifiedXFlags";
            this.lblModifiedXFlags.Size = new System.Drawing.Size(17, 15);
            this.lblModifiedXFlags.TabIndex = 12;
            this.lblModifiedXFlags.Text = "--";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(18, 136);
            this.label6.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(44, 15);
            this.label6.TabIndex = 11;
            this.label6.Text = "X Flags";
            // 
            // lblModifiedMFlags
            // 
            this.lblModifiedMFlags.AutoSize = true;
            this.lblModifiedMFlags.Location = new System.Drawing.Point(174, 166);
            this.lblModifiedMFlags.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblModifiedMFlags.Name = "lblModifiedMFlags";
            this.lblModifiedMFlags.Size = new System.Drawing.Size(17, 15);
            this.lblModifiedMFlags.TabIndex = 14;
            this.lblModifiedMFlags.Text = "--";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(18, 166);
            this.label8.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(48, 15);
            this.label8.TabIndex = 13;
            this.label8.Text = "M Flags";
            // 
            // lblModifiedDPs
            // 
            this.lblModifiedDPs.AutoSize = true;
            this.lblModifiedDPs.Location = new System.Drawing.Point(174, 194);
            this.lblModifiedDPs.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblModifiedDPs.Name = "lblModifiedDPs";
            this.lblModifiedDPs.Size = new System.Drawing.Size(17, 15);
            this.lblModifiedDPs.TabIndex = 16;
            this.lblModifiedDPs.Text = "--";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(18, 194);
            this.label10.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(67, 15);
            this.label10.TabIndex = 15;
            this.label10.Text = "Direct Page";
            // 
            // lblModifiedDBs
            // 
            this.lblModifiedDBs.AutoSize = true;
            this.lblModifiedDBs.Location = new System.Drawing.Point(174, 223);
            this.lblModifiedDBs.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblModifiedDBs.Name = "lblModifiedDBs";
            this.lblModifiedDBs.Size = new System.Drawing.Size(17, 15);
            this.lblModifiedDBs.TabIndex = 18;
            this.lblModifiedDBs.Text = "--";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(18, 223);
            this.label12.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(60, 15);
            this.label12.TabIndex = 17;
            this.label12.Text = "Data Bank";
            // 
            // lblModifiedFlags
            // 
            this.lblModifiedFlags.AutoSize = true;
            this.lblModifiedFlags.Location = new System.Drawing.Point(174, 108);
            this.lblModifiedFlags.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblModifiedFlags.Name = "lblModifiedFlags";
            this.lblModifiedFlags.Size = new System.Drawing.Size(17, 15);
            this.lblModifiedFlags.TabIndex = 20;
            this.lblModifiedFlags.Text = "--";
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(18, 108);
            this.label14.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(139, 15);
            this.label14.TabIndex = 19;
            this.label14.Text = "# Instructions Uncovered";
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label15.Location = new System.Drawing.Point(14, 84);
            this.label15.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(194, 13);
            this.label15.TabIndex = 21;
            this.label15.Text = "Data Uncovered (#modifications)";
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label16.Location = new System.Drawing.Point(14, 8);
            this.label16.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(116, 13);
            this.label16.TabIndex = 22;
            this.label16.Text = "BSNES Connection";
            // 
            // label17
            // 
            this.label17.AutoSize = true;
            this.label17.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label17.Location = new System.Drawing.Point(296, 203);
            this.label17.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(57, 13);
            this.label17.TabIndex = 23;
            this.label17.Text = "Summary";
            // 
            // lblResultStatus
            // 
            this.lblResultStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblResultStatus.Location = new System.Drawing.Point(395, 99);
            this.lblResultStatus.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblResultStatus.Name = "lblResultStatus";
            this.lblResultStatus.Size = new System.Drawing.Size(340, 104);
            this.lblResultStatus.TabIndex = 26;
            this.lblResultStatus.Text = "--jhgasdjhfgasjhdgfhjasdfgasdf kfhfjhf";
            this.lblResultStatus.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label7.Location = new System.Drawing.Point(296, 84);
            this.label7.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(43, 13);
            this.label7.TabIndex = 25;
            this.label7.Text = "Result";
            // 
            // pictureGreenSpinner
            // 
            this.pictureGreenSpinner.Location = new System.Drawing.Point(354, 14);
            this.pictureGreenSpinner.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.pictureGreenSpinner.Name = "pictureGreenSpinner";
            this.pictureGreenSpinner.Size = new System.Drawing.Size(54, 43);
            this.pictureGreenSpinner.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureGreenSpinner.TabIndex = 27;
            this.pictureGreenSpinner.TabStop = false;
            // 
            // button1
            // 
            this.button1.Enabled = false;
            this.button1.Location = new System.Drawing.Point(414, 14);
            this.button1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(61, 44);
            this.button1.TabIndex = 28;
            this.button1.Text = "Help";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.btnTracelogHelpClick);
            // 
            // chkAddTLComments
            // 
            this.chkAddTLComments.AutoSize = true;
            this.chkAddTLComments.Location = new System.Drawing.Point(12, 441);
            this.chkAddTLComments.Name = "chkAddTLComments";
            this.chkAddTLComments.Size = new System.Drawing.Size(285, 19);
            this.chkAddTLComments.TabIndex = 30;
            this.chkAddTLComments.Text = "Add below comment to any instruction executed";
            this.chkAddTLComments.UseVisualStyleBackColor = true;
            this.chkAddTLComments.CheckedChanged += new System.EventHandler(this.chkAddTLComments_CheckedChanged);
            // 
            // chkRemoveTLComments
            // 
            this.chkRemoveTLComments.AutoSize = true;
            this.chkRemoveTLComments.Location = new System.Drawing.Point(12, 400);
            this.chkRemoveTLComments.Name = "chkRemoveTLComments";
            this.chkRemoveTLComments.Size = new System.Drawing.Size(359, 19);
            this.chkRemoveTLComments.TabIndex = 31;
            this.chkRemoveTLComments.Text = "Remove ANY tracelog comment from any executed instruction";
            this.chkRemoveTLComments.UseVisualStyleBackColor = true;
            this.chkRemoveTLComments.CheckedChanged += new System.EventHandler(this.chkRemoveTLComments_CheckedChanged);
            // 
            // txtTracelogComment
            // 
            this.txtTracelogComment.Location = new System.Drawing.Point(12, 466);
            this.txtTracelogComment.Name = "txtTracelogComment";
            this.txtTracelogComment.Size = new System.Drawing.Size(348, 23);
            this.txtTracelogComment.TabIndex = 32;
            this.txtTracelogComment.TextChanged += new System.EventHandler(this.txtTracelogComment_TextChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(46, 492);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(307, 90);
            this.label2.TabIndex = 33;
            this.label2.Text = resources.GetString("label2.Text");
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Font = new System.Drawing.Font("Segoe UI", 11.25F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Underline))), System.Drawing.GraphicsUnit.Point);
            this.label9.Location = new System.Drawing.Point(11, 341);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(110, 20);
            this.label9.TabIndex = 34;
            this.label9.Text = "---GOODIES---";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(14, 422);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(68, 15);
            this.label11.TabIndex = 35;
            this.label11.Text = "(and then..)";
            // 
            // chkCaptureLabelsOnly
            // 
            this.chkCaptureLabelsOnly.AutoSize = true;
            this.chkCaptureLabelsOnly.Location = new System.Drawing.Point(11, 375);
            this.chkCaptureLabelsOnly.Name = "chkCaptureLabelsOnly";
            this.chkCaptureLabelsOnly.Size = new System.Drawing.Size(200, 19);
            this.chkCaptureLabelsOnly.TabIndex = 36;
            this.chkCaptureLabelsOnly.Text = "Capture labels only (ignore flags)";
            this.chkCaptureLabelsOnly.UseVisualStyleBackColor = true;
            this.chkCaptureLabelsOnly.CheckedChanged += new System.EventHandler(this.chkCaptureLabelsOnly_CheckedChanged);
            // 
            // BsnesTraceLogBinaryMonitorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(749, 620);
            this.Controls.Add(this.chkCaptureLabelsOnly);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.txtTracelogComment);
            this.Controls.Add(this.chkRemoveTLComments);
            this.Controls.Add(this.chkAddTLComments);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.pictureGreenSpinner);
            this.Controls.Add(this.lblResultStatus);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label17);
            this.Controls.Add(this.label16);
            this.Controls.Add(this.label15);
            this.Controls.Add(this.lblModifiedFlags);
            this.Controls.Add(this.label14);
            this.Controls.Add(this.lblModifiedDBs);
            this.Controls.Add(this.label12);
            this.Controls.Add(this.lblModifiedDPs);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.lblModifiedMFlags);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.lblModifiedXFlags);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.lblNumberModified);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.lblTotalProcessed);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnFinish);
            this.Controls.Add(this.lblQueueSize);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnStart);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.Name = "BsnesTraceLogBinaryMonitorForm";
            this.Text = "BSNES Live Tracelog Capture";
            this.Load += new System.EventHandler(this.BSNESTraceLogBinaryMonitorForm_Load);
            this.Shown += new System.EventHandler(this.BSNESTraceLogBinaryMonitorForm_Shown);
            ((System.ComponentModel.ISupportInitialize)(this.pictureGreenSpinner)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label lblQueueSize;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.Button btnFinish;
        private System.Windows.Forms.Label lblTotalProcessed;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label lblNumberModified;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label lblModifiedXFlags;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label lblModifiedMFlags;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label lblModifiedDPs;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label lblModifiedDBs;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label lblModifiedFlags;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.Label lblResultStatus;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.PictureBox pictureGreenSpinner;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.CheckBox chkAddTLComments;
        private System.Windows.Forms.CheckBox chkRemoveTLComments;
        private System.Windows.Forms.TextBox txtTracelogComment;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.CheckBox chkCaptureLabelsOnly;
        // private LiveCharts.WinForms.CartesianChart cartesianChart1;
    }
}
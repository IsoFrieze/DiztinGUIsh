namespace DiztinGUIsh.window.dialog
{
    partial class BSNESTraceLogBinaryMonitor
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
            this.button1 = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.lblQueuSize = new System.Windows.Forms.Label();
            this.backgroundWorker1_pipeReader = new System.ComponentModel.BackgroundWorker();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.backgroundWorker2_processQueue = new System.ComponentModel.BackgroundWorker();
            this.btnCancel = new System.Windows.Forms.Button();
            this.lblTotalProcessed = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.lblStatus = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.lblNumberModified = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(12, 130);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(94, 23);
            this.button1.TabIndex = 0;
            this.button1.Text = "Start importing";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 32);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(60, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Queue size";
            // 
            // lblQueuSize
            // 
            this.lblQueuSize.AutoSize = true;
            this.lblQueuSize.Location = new System.Drawing.Point(112, 32);
            this.lblQueuSize.Name = "lblQueuSize";
            this.lblQueuSize.Size = new System.Drawing.Size(13, 13);
            this.lblQueuSize.TabIndex = 2;
            this.lblQueuSize.Text = "--";
            // 
            // backgroundWorker1_pipeReader
            // 
            this.backgroundWorker1_pipeReader.WorkerSupportsCancellation = true;
            this.backgroundWorker1_pipeReader.DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorker1_DoWork);
            this.backgroundWorker1_pipeReader.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.backgroundWorker1_pipeReader_RunWorkerCompleted);
            // 
            // timer1
            // 
            this.timer1.Interval = 500;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // backgroundWorker2_processQueue
            // 
            this.backgroundWorker2_processQueue.WorkerSupportsCancellation = true;
            this.backgroundWorker2_processQueue.DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorker2_processQueue_DoWork);
            this.backgroundWorker2_processQueue.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.backgroundWorker2_processQueue_RunWorkerCompleted_1);
            // 
            // btnCancel
            // 
            this.btnCancel.Enabled = false;
            this.btnCancel.Location = new System.Drawing.Point(112, 130);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(66, 23);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "Finish";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // lblTotalProcessed
            // 
            this.lblTotalProcessed.AutoSize = true;
            this.lblTotalProcessed.Location = new System.Drawing.Point(113, 52);
            this.lblTotalProcessed.Name = "lblTotalProcessed";
            this.lblTotalProcessed.Size = new System.Drawing.Size(13, 13);
            this.lblTotalProcessed.TabIndex = 6;
            this.lblTotalProcessed.Text = "--";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(10, 52);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(89, 13);
            this.label4.TabIndex = 5;
            this.label4.Text = "# Bytes Analyzed";
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(113, 12);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(13, 13);
            this.lblStatus.TabIndex = 8;
            this.lblStatus.Text = "--";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(10, 12);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(37, 13);
            this.label5.TabIndex = 7;
            this.label5.Text = "Status";
            // 
            // lblNumberModified
            // 
            this.lblNumberModified.AutoSize = true;
            this.lblNumberModified.Location = new System.Drawing.Point(112, 104);
            this.lblNumberModified.Name = "lblNumberModified";
            this.lblNumberModified.Size = new System.Drawing.Size(13, 13);
            this.lblNumberModified.TabIndex = 10;
            this.lblNumberModified.Text = "--";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(9, 104);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(86, 13);
            this.label3.TabIndex = 9;
            this.label3.Text = "# Bytes Modified";
            // 
            // BSNESTraceLogBinaryMonitor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(199, 163);
            this.Controls.Add(this.lblNumberModified);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.lblTotalProcessed);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.lblQueuSize);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.button1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "BSNESTraceLogBinaryMonitor";
            this.Text = "BSNES Live Tracelog Capture";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label lblQueuSize;
        private System.ComponentModel.BackgroundWorker backgroundWorker1_pipeReader;
        private System.Windows.Forms.Timer timer1;
        private System.ComponentModel.BackgroundWorker backgroundWorker2_processQueue;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label lblTotalProcessed;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label lblNumberModified;
        private System.Windows.Forms.Label label3;
    }
}
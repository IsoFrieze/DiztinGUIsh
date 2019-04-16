namespace DiztinGUIsh
{
    partial class ImportROMDialog
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
            this.detectMessage = new System.Windows.Forms.Label();
            this.checkData = new System.Windows.Forms.Label();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.mapmode = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.cancel = new System.Windows.Forms.Button();
            this.okay = new System.Windows.Forms.Button();
            this.romspeed = new System.Windows.Forms.Label();
            this.romtitle = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // detectMessage
            // 
            this.detectMessage.AutoSize = true;
            this.detectMessage.Location = new System.Drawing.Point(36, 7);
            this.detectMessage.Name = "detectMessage";
            this.detectMessage.Size = new System.Drawing.Size(187, 13);
            this.detectMessage.TabIndex = 0;
            this.detectMessage.Text = "Couldn\'t auto detect ROM Map Mode!";
            this.detectMessage.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // checkData
            // 
            this.checkData.AutoSize = true;
            this.checkData.Location = new System.Drawing.Point(39, 23);
            this.checkData.Name = "checkData";
            this.checkData.Size = new System.Drawing.Size(179, 13);
            this.checkData.TabIndex = 1;
            this.checkData.Text = "Does the following info look correct?";
            this.checkData.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // comboBox1
            // 
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Items.AddRange(new object[] {
            "LoROM",
            "HiROM",
            "ExHiROM"});
            this.comboBox1.Location = new System.Drawing.Point(103, 18);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(121, 21);
            this.comboBox1.TabIndex = 2;
            this.comboBox1.SelectedIndexChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged);
            // 
            // mapmode
            // 
            this.mapmode.AutoSize = true;
            this.mapmode.Location = new System.Drawing.Point(11, 21);
            this.mapmode.Name = "mapmode";
            this.mapmode.Size = new System.Drawing.Size(89, 13);
            this.mapmode.TabIndex = 3;
            this.mapmode.Text = "ROM Map Mode:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(31, 44);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(69, 13);
            this.label4.TabIndex = 4;
            this.label4.Text = "ROM Speed:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(34, 66);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(66, 13);
            this.label5.TabIndex = 5;
            this.label5.Text = "ROM Name:";
            // 
            // cancel
            // 
            this.cancel.Location = new System.Drawing.Point(12, 171);
            this.cancel.Name = "cancel";
            this.cancel.Size = new System.Drawing.Size(75, 23);
            this.cancel.TabIndex = 6;
            this.cancel.Text = "Cancel";
            this.cancel.UseVisualStyleBackColor = true;
            this.cancel.Click += new System.EventHandler(this.cancel_Click);
            // 
            // okay
            // 
            this.okay.Location = new System.Drawing.Point(172, 171);
            this.okay.Name = "okay";
            this.okay.Size = new System.Drawing.Size(75, 23);
            this.okay.TabIndex = 7;
            this.okay.Text = "OK";
            this.okay.UseVisualStyleBackColor = true;
            this.okay.Click += new System.EventHandler(this.okay_Click);
            // 
            // romspeed
            // 
            this.romspeed.AutoSize = true;
            this.romspeed.Location = new System.Drawing.Point(103, 44);
            this.romspeed.Name = "romspeed";
            this.romspeed.Size = new System.Drawing.Size(55, 13);
            this.romspeed.TabIndex = 8;
            this.romspeed.Text = "SlowROM";
            // 
            // romtitle
            // 
            this.romtitle.AutoSize = true;
            this.romtitle.Location = new System.Drawing.Point(103, 66);
            this.romtitle.Name = "romtitle";
            this.romtitle.Size = new System.Drawing.Size(27, 13);
            this.romtitle.TabIndex = 9;
            this.romtitle.Text = "Title";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(32, 39);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(198, 13);
            this.label3.TabIndex = 10;
            this.label3.Text = "If not, try changing the ROM Map Mode.";
            this.label3.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(3, 55);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(255, 13);
            this.label6.TabIndex = 11;
            this.label6.Text = "This cannot be changed once the project is created.";
            this.label6.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.mapmode);
            this.groupBox1.Controls.Add(this.comboBox1);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.romtitle);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.romspeed);
            this.groupBox1.Location = new System.Drawing.Point(12, 74);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(235, 89);
            this.groupBox1.TabIndex = 12;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "ROM Information";
            // 
            // ImportROMDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(261, 206);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.okay);
            this.Controls.Add(this.cancel);
            this.Controls.Add(this.checkData);
            this.Controls.Add(this.detectMessage);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "ImportROMDialog";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Text = "New Project";
            this.Load += new System.EventHandler(this.ImportROMDialog_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label detectMessage;
        private System.Windows.Forms.Label checkData;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.Label mapmode;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button cancel;
        private System.Windows.Forms.Button okay;
        private System.Windows.Forms.Label romspeed;
        private System.Windows.Forms.Label romtitle;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.GroupBox groupBox1;
    }
}
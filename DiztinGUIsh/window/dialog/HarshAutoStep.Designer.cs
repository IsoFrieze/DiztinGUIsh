namespace DiztinGUIsh.window.dialog
{
    partial class HarshAutoStep
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HarshAutoStep));
            this.label1 = new System.Windows.Forms.Label();
            this.radioHex = new System.Windows.Forms.RadioButton();
            this.cancel = new System.Windows.Forms.Button();
            this.go = new System.Windows.Forms.Button();
            this.group = new System.Windows.Forms.GroupBox();
            this.label5 = new System.Windows.Forms.Label();
            this.textStart = new System.Windows.Forms.TextBox();
            this.radioPC = new System.Windows.Forms.RadioButton();
            this.radioROM = new System.Windows.Forms.RadioButton();
            this.textEnd = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.textCount = new System.Windows.Forms.TextBox();
            this.radioDec = new System.Windows.Forms.RadioButton();
            this.group.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 10);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(316, 135);
            this.label1.TabIndex = 0;
            this.label1.Text = resources.GetString("label1.Text");
            // 
            // radioHex
            // 
            this.radioHex.AutoSize = true;
            this.radioHex.Checked = true;
            this.radioHex.Location = new System.Drawing.Point(121, 284);
            this.radioHex.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.radioHex.Name = "radioHex";
            this.radioHex.Size = new System.Drawing.Size(46, 19);
            this.radioHex.TabIndex = 3;
            this.radioHex.TabStop = true;
            this.radioHex.Text = "Hex";
            this.radioHex.UseVisualStyleBackColor = true;
            this.radioHex.CheckedChanged += new System.EventHandler(this.radioHex_CheckedChanged);
            // 
            // cancel
            // 
            this.cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancel.Location = new System.Drawing.Point(14, 280);
            this.cancel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.cancel.Name = "cancel";
            this.cancel.Size = new System.Drawing.Size(88, 27);
            this.cancel.TabIndex = 5;
            this.cancel.TabStop = false;
            this.cancel.Text = "Cancel";
            this.cancel.UseVisualStyleBackColor = true;
            this.cancel.Click += new System.EventHandler(this.cancel_Click);
            // 
            // go
            // 
            this.go.Location = new System.Drawing.Point(247, 280);
            this.go.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.go.Name = "go";
            this.go.Size = new System.Drawing.Size(88, 27);
            this.go.TabIndex = 2;
            this.go.Text = "Go";
            this.go.UseVisualStyleBackColor = true;
            this.go.Click += new System.EventHandler(this.go_Click);
            // 
            // group
            // 
            this.group.Controls.Add(this.label5);
            this.group.Controls.Add(this.textStart);
            this.group.Controls.Add(this.radioPC);
            this.group.Controls.Add(this.radioROM);
            this.group.Controls.Add(this.textEnd);
            this.group.Controls.Add(this.label4);
            this.group.Controls.Add(this.label6);
            this.group.Controls.Add(this.textCount);
            this.group.Location = new System.Drawing.Point(14, 152);
            this.group.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.group.Name = "group";
            this.group.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.group.Size = new System.Drawing.Size(321, 117);
            this.group.TabIndex = 1;
            this.group.TabStop = false;
            this.group.Text = "Disassemble Region";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(19, 31);
            this.label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(92, 15);
            this.label5.TabIndex = 2;
            this.label5.Text = "Start at Address:";
            // 
            // textStart
            // 
            this.textStart.Location = new System.Drawing.Point(120, 28);
            this.textStart.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.textStart.MaxLength = 6;
            this.textStart.Name = "textStart";
            this.textStart.Size = new System.Drawing.Size(70, 23);
            this.textStart.TabIndex = 3;
            this.textStart.TextChanged += new System.EventHandler(this.textStart_TextChanged);
            // 
            // radioPC
            // 
            this.radioPC.AutoSize = true;
            this.radioPC.Location = new System.Drawing.Point(205, 55);
            this.radioPC.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.radioPC.Name = "radioPC";
            this.radioPC.Size = new System.Drawing.Size(75, 19);
            this.radioPC.TabIndex = 1;
            this.radioPC.Text = "PC Offset";
            this.radioPC.UseVisualStyleBackColor = true;
            // 
            // radioROM
            // 
            this.radioROM.AutoSize = true;
            this.radioROM.Checked = true;
            this.radioROM.Location = new System.Drawing.Point(205, 29);
            this.radioROM.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.radioROM.Name = "radioROM";
            this.radioROM.Size = new System.Drawing.Size(97, 19);
            this.radioROM.TabIndex = 0;
            this.radioROM.TabStop = true;
            this.radioROM.Text = "ROM Address";
            this.radioROM.UseVisualStyleBackColor = true;
            this.radioROM.CheckedChanged += new System.EventHandler(this.radioROM_CheckedChanged);
            // 
            // textEnd
            // 
            this.textEnd.Location = new System.Drawing.Point(120, 55);
            this.textEnd.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.textEnd.MaxLength = 6;
            this.textEnd.Name = "textEnd";
            this.textEnd.Size = new System.Drawing.Size(70, 23);
            this.textEnd.TabIndex = 5;
            this.textEnd.TextChanged += new System.EventHandler(this.textEnd_TextChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(15, 87);
            this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(99, 15);
            this.label4.TabIndex = 6;
            this.label4.Text = "Number of Bytes:";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(28, 59);
            this.label6.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(84, 15);
            this.label6.TabIndex = 4;
            this.label6.Text = "Up to Address:";
            // 
            // textCount
            // 
            this.textCount.Location = new System.Drawing.Point(120, 83);
            this.textCount.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.textCount.MaxLength = 6;
            this.textCount.Name = "textCount";
            this.textCount.Size = new System.Drawing.Size(70, 23);
            this.textCount.TabIndex = 7;
            this.textCount.TextChanged += new System.EventHandler(this.textCount_TextChanged);
            // 
            // radioDec
            // 
            this.radioDec.AutoSize = true;
            this.radioDec.Location = new System.Drawing.Point(177, 284);
            this.radioDec.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.radioDec.Name = "radioDec";
            this.radioDec.Size = new System.Drawing.Size(45, 19);
            this.radioDec.TabIndex = 4;
            this.radioDec.Text = "Dec";
            this.radioDec.UseVisualStyleBackColor = true;
            // 
            // HarshAutoStep
            // 
            this.AcceptButton = this.go;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancel;
            this.ClientSize = new System.Drawing.Size(349, 321);
            this.Controls.Add(this.group);
            this.Controls.Add(this.go);
            this.Controls.Add(this.cancel);
            this.Controls.Add(this.radioHex);
            this.Controls.Add(this.radioDec);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.Name = "HarshAutoStep";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Harsh Auto Step";
            this.group.ResumeLayout(false);
            this.group.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.RadioButton radioHex;
        private System.Windows.Forms.Button cancel;
        private System.Windows.Forms.Button go;
        private System.Windows.Forms.GroupBox group;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textStart;
        private System.Windows.Forms.RadioButton radioPC;
        private System.Windows.Forms.RadioButton radioROM;
        private System.Windows.Forms.TextBox textEnd;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox textCount;
        private System.Windows.Forms.RadioButton radioDec;
    }
}
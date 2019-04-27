namespace DiztinGUIsh
{
    partial class MarkManyDialog
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
            this.label1 = new System.Windows.Forms.Label();
            this.property = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.flagCombo = new System.Windows.Forms.ComboBox();
            this.regValue = new System.Windows.Forms.TextBox();
            this.cancel = new System.Windows.Forms.Button();
            this.okay = new System.Windows.Forms.Button();
            this.textCount = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.textEnd = new System.Windows.Forms.TextBox();
            this.group = new System.Windows.Forms.GroupBox();
            this.label5 = new System.Windows.Forms.Label();
            this.textStart = new System.Windows.Forms.TextBox();
            this.mxCombo = new System.Windows.Forms.ComboBox();
            this.archCombo = new System.Windows.Forms.ComboBox();
            this.radioPC = new System.Windows.Forms.RadioButton();
            this.radioROM = new System.Windows.Forms.RadioButton();
            this.radioHex = new System.Windows.Forms.RadioButton();
            this.radioDec = new System.Windows.Forms.RadioButton();
            this.group.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(25, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(88, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Property to Mark:";
            // 
            // property
            // 
            this.property.FormattingEnabled = true;
            this.property.Items.AddRange(new object[] {
            "Flag",
            "Data Bank",
            "Direct Page",
            "M Flag",
            "X Flag"});
            this.property.Location = new System.Drawing.Point(115, 10);
            this.property.Name = "property";
            this.property.Size = new System.Drawing.Size(121, 21);
            this.property.TabIndex = 1;
            this.property.SelectedIndexChanged += new System.EventHandler(this.property_SelectedIndexChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(64, 25);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(37, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Value:";
            // 
            // flagCombo
            // 
            this.flagCombo.FormattingEnabled = true;
            this.flagCombo.Items.AddRange(new object[] {
            "Unreached",
            "Opcode",
            "Operand",
            "Data (8-Bit)",
            "Graphics",
            "Music",
            "Empty",
            "Data (16-Bit)",
            "Pointer (16-Bit)",
            "Data (24-Bit)",
            "Pointer (24-Bit)",
            "Data (32-Bit)",
            "Pointer (32-Bit)",
            "Text"});
            this.flagCombo.Location = new System.Drawing.Point(103, 22);
            this.flagCombo.Name = "flagCombo";
            this.flagCombo.Size = new System.Drawing.Size(121, 21);
            this.flagCombo.TabIndex = 3;
            // 
            // regValue
            // 
            this.regValue.Location = new System.Drawing.Point(103, 23);
            this.regValue.MaxLength = 5;
            this.regValue.Name = "regValue";
            this.regValue.Size = new System.Drawing.Size(61, 20);
            this.regValue.TabIndex = 4;
            this.regValue.TextChanged += new System.EventHandler(this.regValue_TextChanged);
            // 
            // cancel
            // 
            this.cancel.Location = new System.Drawing.Point(11, 172);
            this.cancel.Name = "cancel";
            this.cancel.Size = new System.Drawing.Size(75, 23);
            this.cancel.TabIndex = 5;
            this.cancel.Text = "Cancel";
            this.cancel.UseVisualStyleBackColor = true;
            this.cancel.Click += new System.EventHandler(this.cancel_Click);
            // 
            // okay
            // 
            this.okay.Location = new System.Drawing.Point(213, 172);
            this.okay.Name = "okay";
            this.okay.Size = new System.Drawing.Size(75, 23);
            this.okay.TabIndex = 6;
            this.okay.Text = "OK";
            this.okay.UseVisualStyleBackColor = true;
            this.okay.Click += new System.EventHandler(this.okay_Click);
            // 
            // textCount
            // 
            this.textCount.Location = new System.Drawing.Point(103, 95);
            this.textCount.MaxLength = 6;
            this.textCount.Name = "textCount";
            this.textCount.Size = new System.Drawing.Size(61, 20);
            this.textCount.TabIndex = 7;
            this.textCount.TextChanged += new System.EventHandler(this.textCount_TextChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(24, 74);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(77, 13);
            this.label3.TabIndex = 8;
            this.label3.Text = "Up to Address:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(13, 98);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(88, 13);
            this.label4.TabIndex = 9;
            this.label4.Text = "Number of Bytes:";
            // 
            // textEnd
            // 
            this.textEnd.Location = new System.Drawing.Point(103, 71);
            this.textEnd.MaxLength = 6;
            this.textEnd.Name = "textEnd";
            this.textEnd.Size = new System.Drawing.Size(61, 20);
            this.textEnd.TabIndex = 10;
            this.textEnd.TextChanged += new System.EventHandler(this.textEnd_TextChanged);
            // 
            // group
            // 
            this.group.Controls.Add(this.label5);
            this.group.Controls.Add(this.textStart);
            this.group.Controls.Add(this.mxCombo);
            this.group.Controls.Add(this.archCombo);
            this.group.Controls.Add(this.radioPC);
            this.group.Controls.Add(this.radioROM);
            this.group.Controls.Add(this.regValue);
            this.group.Controls.Add(this.textEnd);
            this.group.Controls.Add(this.label2);
            this.group.Controls.Add(this.label4);
            this.group.Controls.Add(this.flagCombo);
            this.group.Controls.Add(this.label3);
            this.group.Controls.Add(this.textCount);
            this.group.Location = new System.Drawing.Point(12, 36);
            this.group.Name = "group";
            this.group.Size = new System.Drawing.Size(275, 128);
            this.group.TabIndex = 11;
            this.group.TabStop = false;
            this.group.Text = "Mark Bytes";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(16, 50);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(85, 13);
            this.label5.TabIndex = 16;
            this.label5.Text = "Start at Address:";
            // 
            // textStart
            // 
            this.textStart.Location = new System.Drawing.Point(103, 47);
            this.textStart.MaxLength = 6;
            this.textStart.Name = "textStart";
            this.textStart.Size = new System.Drawing.Size(61, 20);
            this.textStart.TabIndex = 15;
            this.textStart.TextChanged += new System.EventHandler(this.textStart_TextChanged);
            // 
            // mxCombo
            // 
            this.mxCombo.FormattingEnabled = true;
            this.mxCombo.Items.AddRange(new object[] {
            "16-Bit",
            "8-Bit"});
            this.mxCombo.Location = new System.Drawing.Point(103, 22);
            this.mxCombo.Name = "mxCombo";
            this.mxCombo.Size = new System.Drawing.Size(121, 21);
            this.mxCombo.TabIndex = 14;
            // 
            // archCombo
            // 
            this.archCombo.FormattingEnabled = true;
            this.archCombo.Items.AddRange(new object[] {
            "65C816 (CPU)",
            "SPC700 (APU)",
            "SuperFX (GPU)"});
            this.archCombo.Location = new System.Drawing.Point(103, 22);
            this.archCombo.Name = "archCombo";
            this.archCombo.Size = new System.Drawing.Size(121, 21);
            this.archCombo.TabIndex = 13;
            // 
            // radioPC
            // 
            this.radioPC.AutoSize = true;
            this.radioPC.Location = new System.Drawing.Point(176, 71);
            this.radioPC.Name = "radioPC";
            this.radioPC.Size = new System.Drawing.Size(70, 17);
            this.radioPC.TabIndex = 12;
            this.radioPC.Text = "PC Offset";
            this.radioPC.UseVisualStyleBackColor = true;
            // 
            // radioROM
            // 
            this.radioROM.AutoSize = true;
            this.radioROM.Checked = true;
            this.radioROM.Location = new System.Drawing.Point(176, 48);
            this.radioROM.Name = "radioROM";
            this.radioROM.Size = new System.Drawing.Size(91, 17);
            this.radioROM.TabIndex = 11;
            this.radioROM.TabStop = true;
            this.radioROM.Text = "ROM Address";
            this.radioROM.UseVisualStyleBackColor = true;
            this.radioROM.CheckedChanged += new System.EventHandler(this.radioROM_CheckedChanged);
            // 
            // radioHex
            // 
            this.radioHex.AutoSize = true;
            this.radioHex.Checked = true;
            this.radioHex.Location = new System.Drawing.Point(104, 175);
            this.radioHex.Name = "radioHex";
            this.radioHex.Size = new System.Drawing.Size(44, 17);
            this.radioHex.TabIndex = 12;
            this.radioHex.TabStop = true;
            this.radioHex.Text = "Hex";
            this.radioHex.UseVisualStyleBackColor = true;
            this.radioHex.CheckedChanged += new System.EventHandler(this.radioHex_CheckedChanged);
            // 
            // radioDec
            // 
            this.radioDec.AutoSize = true;
            this.radioDec.Location = new System.Drawing.Point(152, 175);
            this.radioDec.Name = "radioDec";
            this.radioDec.Size = new System.Drawing.Size(45, 17);
            this.radioDec.TabIndex = 13;
            this.radioDec.Text = "Dec";
            this.radioDec.UseVisualStyleBackColor = true;
            // 
            // MarkManyDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(299, 206);
            this.Controls.Add(this.radioDec);
            this.Controls.Add(this.radioHex);
            this.Controls.Add(this.group);
            this.Controls.Add(this.okay);
            this.Controls.Add(this.cancel);
            this.Controls.Add(this.property);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "MarkManyDialog";
            this.ShowInTaskbar = false;
            this.Text = "Mark Many";
            this.group.ResumeLayout(false);
            this.group.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox property;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox flagCombo;
        private System.Windows.Forms.TextBox regValue;
        private System.Windows.Forms.Button cancel;
        private System.Windows.Forms.Button okay;
        private System.Windows.Forms.TextBox textCount;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textEnd;
        private System.Windows.Forms.GroupBox group;
        private System.Windows.Forms.ComboBox archCombo;
        private System.Windows.Forms.RadioButton radioPC;
        private System.Windows.Forms.RadioButton radioROM;
        private System.Windows.Forms.RadioButton radioHex;
        private System.Windows.Forms.RadioButton radioDec;
        private System.Windows.Forms.ComboBox mxCombo;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textStart;
    }
}
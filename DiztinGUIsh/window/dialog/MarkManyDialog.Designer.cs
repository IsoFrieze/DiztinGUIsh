namespace DiztinGUIsh.window.dialog
{
    partial class MarkManyView
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
            this.comboPropertyType = new System.Windows.Forms.ComboBox();
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
            this.radioSNES = new System.Windows.Forms.RadioButton();
            this.radioHex = new System.Windows.Forms.RadioButton();
            this.radioDec = new System.Windows.Forms.RadioButton();
            this.group.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(88, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Property to Mark:";
            // 
            // comboPropertyType
            // 
            this.comboPropertyType.FormattingEnabled = true;
            this.comboPropertyType.Items.AddRange(new object[] {
            "Flag",
            "Data Bank",
            "Direct Page",
            "M Flag",
            "X Flag"});
            this.comboPropertyType.Location = new System.Drawing.Point(115, 10);
            this.comboPropertyType.Name = "comboPropertyType";
            this.comboPropertyType.Size = new System.Drawing.Size(247, 21);
            this.comboPropertyType.TabIndex = 1;
            this.comboPropertyType.SelectedIndexChanged += new System.EventHandler(this.property_SelectedIndexChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(64, 25);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(37, 13);
            this.label2.TabIndex = 0;
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
            this.flagCombo.TabIndex = 4;
            // 
            // regValue
            // 
            this.regValue.Location = new System.Drawing.Point(103, 23);
            this.regValue.MaxLength = 5;
            this.regValue.Name = "regValue";
            this.regValue.Size = new System.Drawing.Size(61, 20);
            this.regValue.TabIndex = 3;
            this.regValue.TextChanged += new System.EventHandler(this.regValue_TextChanged);
            // 
            // cancel
            // 
            this.cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancel.Location = new System.Drawing.Point(11, 172);
            this.cancel.Name = "cancel";
            this.cancel.Size = new System.Drawing.Size(75, 23);
            this.cancel.TabIndex = 6;
            this.cancel.TabStop = false;
            this.cancel.Text = "Cancel";
            this.cancel.UseVisualStyleBackColor = true;
            this.cancel.Click += new System.EventHandler(this.cancel_Click);
            // 
            // okay
            // 
            this.okay.Location = new System.Drawing.Point(286, 168);
            this.okay.Name = "okay";
            this.okay.Size = new System.Drawing.Size(75, 23);
            this.okay.TabIndex = 3;
            this.okay.Text = "OK";
            this.okay.UseVisualStyleBackColor = true;
            this.okay.Click += new System.EventHandler(this.okay_Click);
            // 
            // textCount
            // 
            this.textCount.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textCount.Location = new System.Drawing.Point(103, 95);
            this.textCount.MaxLength = 6;
            this.textCount.Name = "textCount";
            this.textCount.Size = new System.Drawing.Size(114, 20);
            this.textCount.TabIndex = 12;
            this.textCount.TextChanged += new System.EventHandler(this.textCount_TextChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(3, 74);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(102, 13);
            this.label3.TabIndex = 9;
            this.label3.Text = "Up to and including:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(5, 98);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(46, 13);
            this.label4.TabIndex = 11;
            this.label4.Text = "# Bytes:";
            // 
            // textEnd
            // 
            this.textEnd.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textEnd.Location = new System.Drawing.Point(103, 71);
            this.textEnd.MaxLength = 6;
            this.textEnd.Name = "textEnd";
            this.textEnd.Size = new System.Drawing.Size(114, 20);
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
            this.group.Controls.Add(this.radioSNES);
            this.group.Controls.Add(this.regValue);
            this.group.Controls.Add(this.textEnd);
            this.group.Controls.Add(this.label2);
            this.group.Controls.Add(this.label4);
            this.group.Controls.Add(this.flagCombo);
            this.group.Controls.Add(this.label3);
            this.group.Controls.Add(this.textCount);
            this.group.Location = new System.Drawing.Point(12, 36);
            this.group.Name = "group";
            this.group.Size = new System.Drawing.Size(350, 128);
            this.group.TabIndex = 2;
            this.group.TabStop = false;
            this.group.Text = "Mark Bytes";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(3, 49);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(85, 13);
            this.label5.TabIndex = 7;
            this.label5.Text = "Start at Address:";
            // 
            // textStart
            // 
            this.textStart.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textStart.Location = new System.Drawing.Point(103, 47);
            this.textStart.MaxLength = 6;
            this.textStart.Name = "textStart";
            this.textStart.Size = new System.Drawing.Size(114, 20);
            this.textStart.TabIndex = 8;
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
            this.mxCombo.TabIndex = 1;
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
            this.archCombo.TabIndex = 2;
            // 
            // radioPC
            // 
            this.radioPC.AutoSize = true;
            this.radioPC.Location = new System.Drawing.Point(223, 71);
            this.radioPC.Name = "radioPC";
            this.radioPC.Size = new System.Drawing.Size(100, 17);
            this.radioPC.TabIndex = 6;
            this.radioPC.Text = "ROM File Offset";
            this.radioPC.UseVisualStyleBackColor = true;
            // 
            // radioSNES
            // 
            this.radioSNES.AutoSize = true;
            this.radioSNES.Checked = true;
            this.radioSNES.Location = new System.Drawing.Point(223, 48);
            this.radioSNES.Name = "radioSNES";
            this.radioSNES.Size = new System.Drawing.Size(95, 17);
            this.radioSNES.TabIndex = 5;
            this.radioSNES.TabStop = true;
            this.radioSNES.Text = "SNES Address";
            this.radioSNES.UseVisualStyleBackColor = true;
            this.radioSNES.CheckedChanged += new System.EventHandler(this.radioROM_CheckedChanged);
            // 
            // radioHex
            // 
            this.radioHex.AutoSize = true;
            this.radioHex.Checked = true;
            this.radioHex.Location = new System.Drawing.Point(140, 175);
            this.radioHex.Name = "radioHex";
            this.radioHex.Size = new System.Drawing.Size(44, 17);
            this.radioHex.TabIndex = 4;
            this.radioHex.TabStop = true;
            this.radioHex.Text = "Hex";
            this.radioHex.UseVisualStyleBackColor = true;
            this.radioHex.CheckedChanged += new System.EventHandler(this.radioHex_CheckedChanged);
            // 
            // radioDec
            // 
            this.radioDec.AutoSize = true;
            this.radioDec.Location = new System.Drawing.Point(196, 175);
            this.radioDec.Name = "radioDec";
            this.radioDec.Size = new System.Drawing.Size(45, 17);
            this.radioDec.TabIndex = 5;
            this.radioDec.Text = "Dec";
            this.radioDec.UseVisualStyleBackColor = true;
            // 
            // MarkManyView
            // 
            this.AcceptButton = this.okay;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancel;
            this.ClientSize = new System.Drawing.Size(373, 206);
            this.Controls.Add(this.radioDec);
            this.Controls.Add(this.radioHex);
            this.Controls.Add(this.group);
            this.Controls.Add(this.okay);
            this.Controls.Add(this.cancel);
            this.Controls.Add(this.comboPropertyType);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "MarkManyView";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Mark Many";
            this.group.ResumeLayout(false);
            this.group.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboPropertyType;
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
        private System.Windows.Forms.RadioButton radioSNES;
        private System.Windows.Forms.RadioButton radioHex;
        private System.Windows.Forms.RadioButton radioDec;
        private System.Windows.Forms.ComboBox mxCombo;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textStart;
    }
}

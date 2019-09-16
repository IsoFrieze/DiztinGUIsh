namespace DiztinGUIsh
{
    partial class GotoDialog
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
            this.label2 = new System.Windows.Forms.Label();
            this.textROM = new System.Windows.Forms.TextBox();
            this.textPC = new System.Windows.Forms.TextBox();
            this.radioHex = new System.Windows.Forms.RadioButton();
            this.radioDec = new System.Windows.Forms.RadioButton();
            this.cancel = new System.Windows.Forms.Button();
            this.go = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(25, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(76, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "ROM Address:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(46, 41);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(55, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "PC Offset:";
            // 
            // textROM
            // 
            this.textROM.Location = new System.Drawing.Point(103, 12);
            this.textROM.MaxLength = 6;
            this.textROM.Name = "textROM";
            this.textROM.Size = new System.Drawing.Size(46, 20);
            this.textROM.TabIndex = 1;
            this.textROM.Text = "000000";
            this.textROM.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.textROM.TextChanged += new System.EventHandler(this.textROM_TextChanged);
            this.textROM.KeyDown += new System.Windows.Forms.KeyEventHandler(this.textROM_KeyDown);
            // 
            // textPC
            // 
            this.textPC.Location = new System.Drawing.Point(103, 38);
            this.textPC.MaxLength = 6;
            this.textPC.Name = "textPC";
            this.textPC.Size = new System.Drawing.Size(46, 20);
            this.textPC.TabIndex = 3;
            this.textPC.Text = "00000";
            this.textPC.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.textPC.TextChanged += new System.EventHandler(this.textPC_TextChanged);
            this.textPC.KeyDown += new System.Windows.Forms.KeyEventHandler(this.textPC_KeyDown);
            // 
            // radioHex
            // 
            this.radioHex.AutoSize = true;
            this.radioHex.Checked = true;
            this.radioHex.Location = new System.Drawing.Point(43, 70);
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
            this.radioDec.Location = new System.Drawing.Point(93, 70);
            this.radioDec.Name = "radioDec";
            this.radioDec.Size = new System.Drawing.Size(45, 17);
            this.radioDec.TabIndex = 5;
            this.radioDec.Text = "Dec";
            this.radioDec.UseVisualStyleBackColor = true;
            // 
            // cancel
            // 
            this.cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancel.Location = new System.Drawing.Point(12, 105);
            this.cancel.Name = "cancel";
            this.cancel.Size = new System.Drawing.Size(75, 23);
            this.cancel.TabIndex = 7;
            this.cancel.TabStop = false;
            this.cancel.Text = "Cancel";
            this.cancel.UseVisualStyleBackColor = true;
            this.cancel.Click += new System.EventHandler(this.cancel_Click);
            // 
            // go
            // 
            this.go.Location = new System.Drawing.Point(93, 105);
            this.go.Name = "go";
            this.go.Size = new System.Drawing.Size(75, 23);
            this.go.TabIndex = 6;
            this.go.Text = "Go";
            this.go.UseVisualStyleBackColor = true;
            this.go.Click += new System.EventHandler(this.go_Click);
            // 
            // GotoDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancel;
            this.ClientSize = new System.Drawing.Size(180, 140);
            this.Controls.Add(this.go);
            this.Controls.Add(this.cancel);
            this.Controls.Add(this.radioDec);
            this.Controls.Add(this.radioHex);
            this.Controls.Add(this.textPC);
            this.Controls.Add(this.textROM);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "GotoDialog";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Goto";
            this.Load += new System.EventHandler(this.GotoDialog_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textROM;
        private System.Windows.Forms.TextBox textPC;
        private System.Windows.Forms.RadioButton radioHex;
        private System.Windows.Forms.RadioButton radioDec;
        private System.Windows.Forms.Button cancel;
        private System.Windows.Forms.Button go;
    }
}
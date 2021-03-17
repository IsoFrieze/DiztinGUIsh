using System.ComponentModel;

namespace DiztinGUIsh.window2
{
    partial class DataGridEditorFormTemp
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
            this.components = new System.ComponentModel.Container();
            this.label1 = new System.Windows.Forms.Label();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.chkFilter2 = new System.Windows.Forms.CheckBox();
            this.chkFilter1 = new System.Windows.Forms.CheckBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.dataGridEditorControl1 = new DiztinGUIsh.window2.DataGridEditorControl();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.flowLayoutPanel1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(52, 15);
            this.label1.TabIndex = 0;
            this.label1.Text = "lblName";
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.panel1);
            this.flowLayoutPanel1.Controls.Add(this.dataGridEditorControl1);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.flowLayoutPanel1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(1405, 584);
            this.flowLayoutPanel1.TabIndex = 1;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.chkFilter2);
            this.panel1.Controls.Add(this.chkFilter1);
            this.panel1.Controls.Add(this.label4);
            this.panel1.Controls.Add(this.label3);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Location = new System.Drawing.Point(4, 3);
            this.panel1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(196, 505);
            this.panel1.TabIndex = 0;
            // 
            // chkFilter2
            // 
            this.chkFilter2.AutoSize = true;
            this.chkFilter2.Location = new System.Drawing.Point(9, 96);
            this.chkFilter2.Name = "chkFilter2";
            this.chkFilter2.Size = new System.Drawing.Size(131, 19);
            this.chkFilter2.TabIndex = 5;
            this.chkFilter2.Text = "Filter2:  ROM subset";
            this.chkFilter2.UseVisualStyleBackColor = true;
            // 
            // chkFilter1
            // 
            this.chkFilter1.AutoSize = true;
            this.chkFilter1.Location = new System.Drawing.Point(9, 71);
            this.chkFilter1.Name = "chkFilter1";
            this.chkFilter1.Size = new System.Drawing.Size(135, 19);
            this.chkFilter1.TabIndex = 3;
            this.chkFilter1.Text = "Filter1: opcodes only";
            this.chkFilter1.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(0, 52);
            this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(26, 15);
            this.label4.TabIndex = 2;
            this.label4.Text = "lbl3";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(0, 27);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(26, 15);
            this.label3.TabIndex = 1;
            this.label3.Text = "lbl2";
            // 
            // dataGridEditorControl1
            // 
            this.dataGridEditorControl1.DataController = null;
            this.dataGridEditorControl1.DataGridNumberBase = Diz.Core.util.Util.NumberBase.Hexadecimal;
            this.dataGridEditorControl1.DataSource = null;
            this.dataGridEditorControl1.DisplayBase = Diz.Core.util.Util.NumberBase.Hexadecimal;
            this.dataGridEditorControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridEditorControl1.Location = new System.Drawing.Point(207, 3);
            this.dataGridEditorControl1.Name = "dataGridEditorControl1";
            this.dataGridEditorControl1.Size = new System.Drawing.Size(1130, 505);
            this.dataGridEditorControl1.TabIndex = 1;
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Interval = 500;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // DataGridEditorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1405, 584);
            this.Controls.Add(this.flowLayoutPanel1);
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.Name = "DataGridEditorFormTemp";
            this.Text = "DGEditor";
            this.Load += new System.EventHandler(this.DG_Load);
            this.flowLayoutPanel1.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckBox chkFilter2;
        private System.Windows.Forms.CheckBox chkFilter1;
        private DataGridEditorControl dataGridEditorControl1;
        private System.Windows.Forms.Timer timer1;
    }
}
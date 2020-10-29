namespace DiztinGUIsh.window
{
    partial class VisualizerForm
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
            this.panelTop = new System.Windows.Forms.Panel();
            this.panelLegend = new System.Windows.Forms.Panel();
            this.bankLegend1 = new DiztinGUIsh.window.usercontrols.BankLegend();
            this.panelBottom = new System.Windows.Forms.Panel();
            this.romFullVisualizer1 = new DiztinGUIsh.window.usercontrols.RomFullVisualizer();
            this.panelTop.SuspendLayout();
            this.panelLegend.SuspendLayout();
            this.panelBottom.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelTop
            // 
            this.panelTop.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelTop.Controls.Add(this.panelLegend);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(765, 154);
            this.panelTop.TabIndex = 1;
            // 
            // panelLegend
            // 
            this.panelLegend.Controls.Add(this.bankLegend1);
            this.panelLegend.Dock = System.Windows.Forms.DockStyle.Left;
            this.panelLegend.Location = new System.Drawing.Point(0, 0);
            this.panelLegend.Name = "panelLegend";
            this.panelLegend.Size = new System.Drawing.Size(567, 152);
            this.panelLegend.TabIndex = 0;
            // 
            // bankLegend1
            // 
            this.bankLegend1.AutoScroll = true;
            this.bankLegend1.AutoSize = true;
            this.bankLegend1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.bankLegend1.Location = new System.Drawing.Point(0, 0);
            this.bankLegend1.Name = "bankLegend1";
            this.bankLegend1.Size = new System.Drawing.Size(567, 152);
            this.bankLegend1.TabIndex = 0;
            // 
            // panelBottom
            // 
            this.panelBottom.AutoScroll = true;
            this.panelBottom.AutoSize = true;
            this.panelBottom.Controls.Add(this.romFullVisualizer1);
            this.panelBottom.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelBottom.Location = new System.Drawing.Point(0, 154);
            this.panelBottom.Name = "panelBottom";
            this.panelBottom.Size = new System.Drawing.Size(765, 363);
            this.panelBottom.TabIndex = 2;
            // 
            // romFullVisualizer1
            // 
            this.romFullVisualizer1.Location = new System.Drawing.Point(3, 0);
            this.romFullVisualizer1.MinimumSize = new System.Drawing.Size(100, 100);
            this.romFullVisualizer1.Name = "romFullVisualizer1";
            this.romFullVisualizer1.Project = null;
            this.romFullVisualizer1.Size = new System.Drawing.Size(666, 335);
            this.romFullVisualizer1.TabIndex = 0;
            // 
            // VisualizerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoScroll = true;
            this.ClientSize = new System.Drawing.Size(765, 517);
            this.Controls.Add(this.panelBottom);
            this.Controls.Add(this.panelTop);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Name = "VisualizerForm";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "ROM Visualizer";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.VisualizerForm_FormClosing);
            this.Load += new System.EventHandler(this.VisualizerForm_Load);
            this.panelTop.ResumeLayout(false);
            this.panelLegend.ResumeLayout(false);
            this.panelLegend.PerformLayout();
            this.panelBottom.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Panel panelLegend;
        private System.Windows.Forms.Panel panelBottom;
        private usercontrols.BankLegend bankLegend1;
        private usercontrols.RomFullVisualizer romFullVisualizer1;
    }
}
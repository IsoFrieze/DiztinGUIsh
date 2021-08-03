using System.ComponentModel;
using System.Windows.Forms;
using Avalonia.Win32.Embedding;

namespace DiztinGUIsh.window
{
    partial class LabelsListWinForm
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
            this.avaloniaHost = new WinFormsAvaloniaControlHost();
            this.SuspendLayout();
            // 
            // avaloniaHost
            // 
            this.avaloniaHost.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                                                                              | System.Windows.Forms.AnchorStyles.Left)
                                                                             | System.Windows.Forms.AnchorStyles.Right)));
            this.avaloniaHost.Content = null;
            this.avaloniaHost.Location = new System.Drawing.Point(6, 19);
            this.avaloniaHost.Name = "avaloniaHost";
            this.avaloniaHost.Size = new System.Drawing.Size(489, 393);
            this.avaloniaHost.TabIndex = 0;
            this.avaloniaHost.Text = "avaloniaHost";
            // 
            // LabelsList
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.avaloniaHost);
            this.Name = "LabelsListWinForm";
            this.Text = "LabelsList";
            this.ResumeLayout(false);
        }

        private WinFormsAvaloniaControlHost avaloniaHost;

        #endregion
    }
}

namespace DiztinGUIsh.window
{
    partial class NavigationForm
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
            this.navigationCtrl = new DiztinGUIsh.window.usercontrols.NavigationUserControl();
            this.SuspendLayout();
            // 
            // navigationCtrl
            // 
            this.navigationCtrl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.navigationCtrl.Document = null;
            this.navigationCtrl.Location = new System.Drawing.Point(0, 0);
            this.navigationCtrl.Name = "navigationCtrl";
            this.navigationCtrl.Size = new System.Drawing.Size(404, 450);
            this.navigationCtrl.SnesNavigation = null;
            this.navigationCtrl.TabIndex = 0;
            this.navigationCtrl.Load += new System.EventHandler(this.navigationCtrl_Load);
            // 
            // NavigationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(404, 450);
            this.Controls.Add(this.navigationCtrl);
            this.Name = "NavigationForm";
            this.Text = "Navigation";
            this.Load += new System.EventHandler(this.Navigation_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private usercontrols.NavigationUserControl navigationCtrl;
    }
}
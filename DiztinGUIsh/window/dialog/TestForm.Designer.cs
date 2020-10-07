using Diz.Core.serialization;

namespace DiztinGUIsh.window.dialog
{
    partial class TestForm
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
            System.Windows.Forms.Label rOMMapModeLabel;
            System.Windows.Forms.Label rOMMapModeLabel1;
            this.project_ImportRomSettingsBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.rOMMapModeComboBox = new System.Windows.Forms.ComboBox();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            rOMMapModeLabel = new System.Windows.Forms.Label();
            rOMMapModeLabel1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.project_ImportRomSettingsBindingSource)).BeginInit();
            this.SuspendLayout();
            // 
            // rOMMapModeLabel
            // 
            rOMMapModeLabel.AutoSize = true;
            rOMMapModeLabel.Location = new System.Drawing.Point(129, 149);
            rOMMapModeLabel.Name = "rOMMapModeLabel";
            rOMMapModeLabel.Size = new System.Drawing.Size(86, 13);
            rOMMapModeLabel.TabIndex = 1;
            rOMMapModeLabel.Text = "ROMMap Mode:";
            // 
            // rOMMapModeLabel1
            // 
            rOMMapModeLabel1.AutoSize = true;
            rOMMapModeLabel1.Location = new System.Drawing.Point(241, 150);
            rOMMapModeLabel1.Name = "rOMMapModeLabel1";
            rOMMapModeLabel1.Size = new System.Drawing.Size(86, 13);
            rOMMapModeLabel1.TabIndex = 1;
            rOMMapModeLabel1.Text = "ROMMap Mode:";
            // 
            // project_ImportRomSettingsBindingSource
            // 
            this.project_ImportRomSettingsBindingSource.DataSource = typeof(ImportRomSettings);
            // 
            // rOMMapModeComboBox
            // 
            this.rOMMapModeComboBox.DataBindings.Add(new System.Windows.Forms.Binding("SelectedValue", this.project_ImportRomSettingsBindingSource, "ROMMapMode", true));
            this.rOMMapModeComboBox.FormattingEnabled = true;
            this.rOMMapModeComboBox.Location = new System.Drawing.Point(333, 147);
            this.rOMMapModeComboBox.Name = "rOMMapModeComboBox";
            this.rOMMapModeComboBox.Size = new System.Drawing.Size(121, 21);
            this.rOMMapModeComboBox.TabIndex = 2;
            this.rOMMapModeComboBox.SelectedIndexChanged += new System.EventHandler(this.rOMMapModeComboBox_SelectedIndexChanged_1);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(501, 215);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 3;
            this.button1.Text = "button1";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(378, 306);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 23);
            this.button2.TabIndex = 4;
            this.button2.Text = "button2";
            this.button2.UseVisualStyleBackColor = true;
            // 
            // TestForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Controls.Add(rOMMapModeLabel1);
            this.Controls.Add(this.rOMMapModeComboBox);
            this.Controls.Add(rOMMapModeLabel);
            this.Name = "TestForm";
            this.Text = "TestForm";
            this.Load += new System.EventHandler(this.TestForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.project_ImportRomSettingsBindingSource)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.BindingSource project_ImportRomSettingsBindingSource;
        private System.Windows.Forms.ComboBox rOMMapModeComboBox;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
    }
}
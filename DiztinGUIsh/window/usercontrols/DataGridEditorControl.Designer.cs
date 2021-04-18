using System.ComponentModel;

namespace DiztinGUIsh.window.usercontrols
{
    partial class DataGridEditorControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.vScrollBar1 = new System.Windows.Forms.VScrollBar();
            this.Table = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.Table)).BeginInit();
            this.SuspendLayout();
            // 
            // vScrollBar1
            // 
            this.vScrollBar1.Dock = System.Windows.Forms.DockStyle.Right;
            this.vScrollBar1.Location = new System.Drawing.Point(1113, 0);
            this.vScrollBar1.Name = "vScrollBar1";
            this.vScrollBar1.Size = new System.Drawing.Size(17, 354);
            this.vScrollBar1.TabIndex = 8;
            this.vScrollBar1.Scroll += new System.Windows.Forms.ScrollEventHandler(this.vScrollBar1_Scroll);
            // 
            // Table
            // 
            this.Table.AllowUserToAddRows = false;
            this.Table.AllowUserToDeleteRows = false;
            this.Table.AllowUserToResizeRows = false;
            this.Table.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.ColumnHeader;
            this.Table.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.Table.CausesValidation = false;
            this.Table.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            this.Table.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.Table.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Table.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.Table.Location = new System.Drawing.Point(0, 0);
            this.Table.Margin = new System.Windows.Forms.Padding(0);
            this.Table.MinimumSize = new System.Drawing.Size(20, 20);
            this.Table.MultiSelect = false;
            this.Table.Name = "Table";
            this.Table.RowHeadersVisible = false;
            this.Table.RowTemplate.Height = 15;
            this.Table.ShowCellErrors = false;
            this.Table.ShowCellToolTips = false;
            this.Table.ShowEditingIcon = false;
            this.Table.ShowRowErrors = false;
            this.Table.Size = new System.Drawing.Size(1113, 354);
            this.Table.TabIndex = 9;
            this.Table.TabStop = false;
            this.Table.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Table_KeyDown);
            // 
            // DataGridEditorControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.Table);
            this.Controls.Add(this.vScrollBar1);
            this.Name = "DataGridEditorControl";
            this.Size = new System.Drawing.Size(1130, 354);
            this.Load += new System.EventHandler(this.DataGridEditorControl_Load);
            this.SizeChanged += new System.EventHandler(this.DataGridEditorControl_SizeChanged);
            ((System.ComponentModel.ISupportInitialize)(this.Table)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.VScrollBar vScrollBar1;
        private System.Windows.Forms.DataGridView Table;
    }
}
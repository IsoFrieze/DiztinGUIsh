namespace DiztinGUIsh
{
    partial class MainWindow
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle247 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle235 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle236 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle237 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle238 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle239 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle240 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle241 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle242 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle243 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle244 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle245 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle246 = new System.Windows.Forms.DataGridViewCellStyle();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.ColumnAlias = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnPC = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnChar = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnHex = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnInstruction = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnEA = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnFlag = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnDB = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnDP = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnM = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnX = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnComment = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.newProjectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openProjectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveProjectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveProjectAsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.exportLogToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importCDLToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.stepOverToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.stepInToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.autoStepSafeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.autoStepHarshToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.gotoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.gotoEffectiveAddressToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.gotoFirstUnreachedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.gotoNearUnreachedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.selectMarkerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.markOneToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.markManyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
            this.setDirectPageToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.setDataBankToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toggleAccumulatorSizeMToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.visualMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.graphicsWindowToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.constantsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.decimalToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.hexadecimalToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.binaryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toggleIndexSizeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addLabelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addCommentToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.unreachedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.opcodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.operandToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bitDataToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.graphicsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.musicToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.emptyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bitDataToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.wordPointerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bitDataToolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            this.longPointerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bitDataToolStripMenuItem3 = new System.Windows.Forms.ToolStripMenuItem();
            this.dWordPointerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.textToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator6 = new System.Windows.Forms.ToolStripSeparator();
            this.fixMisalignedInstructionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewHelpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.openFileDialog2 = new System.Windows.Forms.OpenFileDialog();
            this.saveFileDialog1 = new System.Windows.Forms.SaveFileDialog();
            this.toolStripSeparator7 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.menuStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AllowUserToResizeRows = false;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnAlias,
            this.ColumnPC,
            this.ColumnChar,
            this.ColumnHex,
            this.ColumnInstruction,
            this.ColumnEA,
            this.ColumnFlag,
            this.ColumnDB,
            this.ColumnDP,
            this.ColumnM,
            this.ColumnX,
            this.ColumnComment});
            dataGridViewCellStyle247.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle247.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle247.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle247.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle247.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle247.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle247.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridView1.DefaultCellStyle = dataGridViewCellStyle247;
            this.dataGridView1.Location = new System.Drawing.Point(0, 24);
            this.dataGridView1.Margin = new System.Windows.Forms.Padding(0);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowHeadersWidth = 4;
            this.dataGridView1.RowTemplate.Height = 15;
            this.dataGridView1.Size = new System.Drawing.Size(700, 500);
            this.dataGridView1.TabIndex = 0;
            // 
            // ColumnAlias
            // 
            dataGridViewCellStyle235.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle235.Font = new System.Drawing.Font("Arial Narrow", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ColumnAlias.DefaultCellStyle = dataGridViewCellStyle235;
            this.ColumnAlias.HeaderText = "Alias";
            this.ColumnAlias.MaxInputLength = 20;
            this.ColumnAlias.Name = "ColumnAlias";
            this.ColumnAlias.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // ColumnPC
            // 
            dataGridViewCellStyle236.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle236.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ColumnPC.DefaultCellStyle = dataGridViewCellStyle236;
            this.ColumnPC.HeaderText = "PC";
            this.ColumnPC.MaxInputLength = 6;
            this.ColumnPC.Name = "ColumnPC";
            this.ColumnPC.ReadOnly = true;
            this.ColumnPC.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.ColumnPC.Width = 45;
            // 
            // ColumnChar
            // 
            dataGridViewCellStyle237.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle237.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ColumnChar.DefaultCellStyle = dataGridViewCellStyle237;
            this.ColumnChar.HeaderText = "@";
            this.ColumnChar.MaxInputLength = 1;
            this.ColumnChar.Name = "ColumnChar";
            this.ColumnChar.ReadOnly = true;
            this.ColumnChar.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.ColumnChar.Width = 26;
            // 
            // ColumnHex
            // 
            dataGridViewCellStyle238.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle238.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ColumnHex.DefaultCellStyle = dataGridViewCellStyle238;
            this.ColumnHex.HeaderText = "#";
            this.ColumnHex.MaxInputLength = 3;
            this.ColumnHex.Name = "ColumnHex";
            this.ColumnHex.ReadOnly = true;
            this.ColumnHex.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.ColumnHex.Width = 26;
            // 
            // ColumnInstruction
            // 
            dataGridViewCellStyle239.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle239.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ColumnInstruction.DefaultCellStyle = dataGridViewCellStyle239;
            this.ColumnInstruction.HeaderText = "Instruction";
            this.ColumnInstruction.MaxInputLength = 64;
            this.ColumnInstruction.Name = "ColumnInstruction";
            this.ColumnInstruction.ReadOnly = true;
            this.ColumnInstruction.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // ColumnEA
            // 
            dataGridViewCellStyle240.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle240.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ColumnEA.DefaultCellStyle = dataGridViewCellStyle240;
            this.ColumnEA.HeaderText = "EA";
            this.ColumnEA.MaxInputLength = 6;
            this.ColumnEA.Name = "ColumnEA";
            this.ColumnEA.ReadOnly = true;
            this.ColumnEA.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.ColumnEA.Width = 45;
            // 
            // ColumnFlag
            // 
            dataGridViewCellStyle241.Font = new System.Drawing.Font("Arial Narrow", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ColumnFlag.DefaultCellStyle = dataGridViewCellStyle241;
            this.ColumnFlag.HeaderText = "Flag";
            this.ColumnFlag.Name = "ColumnFlag";
            this.ColumnFlag.ReadOnly = true;
            this.ColumnFlag.Width = 80;
            // 
            // ColumnDB
            // 
            dataGridViewCellStyle242.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle242.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ColumnDB.DefaultCellStyle = dataGridViewCellStyle242;
            this.ColumnDB.HeaderText = "B";
            this.ColumnDB.MaxInputLength = 2;
            this.ColumnDB.Name = "ColumnDB";
            this.ColumnDB.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.ColumnDB.Width = 20;
            // 
            // ColumnDP
            // 
            dataGridViewCellStyle243.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle243.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ColumnDP.DefaultCellStyle = dataGridViewCellStyle243;
            this.ColumnDP.HeaderText = "D";
            this.ColumnDP.MaxInputLength = 4;
            this.ColumnDP.Name = "ColumnDP";
            this.ColumnDP.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.ColumnDP.Width = 32;
            // 
            // ColumnM
            // 
            dataGridViewCellStyle244.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ColumnM.DefaultCellStyle = dataGridViewCellStyle244;
            this.ColumnM.HeaderText = "M";
            this.ColumnM.Name = "ColumnM";
            this.ColumnM.Width = 20;
            // 
            // ColumnX
            // 
            dataGridViewCellStyle245.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ColumnX.DefaultCellStyle = dataGridViewCellStyle245;
            this.ColumnX.HeaderText = "X";
            this.ColumnX.Name = "ColumnX";
            this.ColumnX.Width = 20;
            // 
            // ColumnComment
            // 
            this.ColumnComment.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            dataGridViewCellStyle246.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle246.Font = new System.Drawing.Font("Arial Narrow", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ColumnComment.DefaultCellStyle = dataGridViewCellStyle246;
            this.ColumnComment.HeaderText = "Comment";
            this.ColumnComment.Name = "ColumnComment";
            this.ColumnComment.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.editToolStripMenuItem,
            this.viewToolStripMenuItem,
            this.helpToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(700, 24);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newProjectToolStripMenuItem,
            this.openProjectToolStripMenuItem,
            this.saveProjectToolStripMenuItem,
            this.saveProjectAsToolStripMenuItem,
            this.toolStripSeparator1,
            this.exportLogToolStripMenuItem,
            this.importCDLToolStripMenuItem,
            this.toolStripSeparator7,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // newProjectToolStripMenuItem
            // 
            this.newProjectToolStripMenuItem.Name = "newProjectToolStripMenuItem";
            this.newProjectToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.newProjectToolStripMenuItem.Text = "New Project...";
            this.newProjectToolStripMenuItem.Click += new System.EventHandler(this.newProjectToolStripMenuItem_Click);
            // 
            // openProjectToolStripMenuItem
            // 
            this.openProjectToolStripMenuItem.Name = "openProjectToolStripMenuItem";
            this.openProjectToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.openProjectToolStripMenuItem.Text = "Open Project...";
            this.openProjectToolStripMenuItem.Click += new System.EventHandler(this.openProjectToolStripMenuItem_Click);
            // 
            // saveProjectToolStripMenuItem
            // 
            this.saveProjectToolStripMenuItem.Enabled = false;
            this.saveProjectToolStripMenuItem.Name = "saveProjectToolStripMenuItem";
            this.saveProjectToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.saveProjectToolStripMenuItem.Text = "Save Project";
            this.saveProjectToolStripMenuItem.Click += new System.EventHandler(this.saveProjectToolStripMenuItem_Click);
            // 
            // saveProjectAsToolStripMenuItem
            // 
            this.saveProjectAsToolStripMenuItem.Enabled = false;
            this.saveProjectAsToolStripMenuItem.Name = "saveProjectAsToolStripMenuItem";
            this.saveProjectAsToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.saveProjectAsToolStripMenuItem.Text = "Save Project As...";
            this.saveProjectAsToolStripMenuItem.Click += new System.EventHandler(this.saveProjectAsToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(181, 6);
            // 
            // exportLogToolStripMenuItem
            // 
            this.exportLogToolStripMenuItem.Enabled = false;
            this.exportLogToolStripMenuItem.Name = "exportLogToolStripMenuItem";
            this.exportLogToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.exportLogToolStripMenuItem.Text = "Export Disassembly...";
            // 
            // importCDLToolStripMenuItem
            // 
            this.importCDLToolStripMenuItem.Enabled = false;
            this.importCDLToolStripMenuItem.Name = "importCDLToolStripMenuItem";
            this.importCDLToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.importCDLToolStripMenuItem.Text = "Import CDL...";
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.stepOverToolStripMenuItem,
            this.stepInToolStripMenuItem,
            this.toolStripSeparator2,
            this.autoStepSafeToolStripMenuItem,
            this.autoStepHarshToolStripMenuItem,
            this.toolStripSeparator3,
            this.gotoToolStripMenuItem,
            this.gotoEffectiveAddressToolStripMenuItem,
            this.gotoFirstUnreachedToolStripMenuItem,
            this.gotoNearUnreachedToolStripMenuItem,
            this.toolStripSeparator4,
            this.selectMarkerToolStripMenuItem,
            this.markOneToolStripMenuItem,
            this.markManyToolStripMenuItem,
            this.toolStripSeparator5,
            this.addLabelToolStripMenuItem,
            this.setDirectPageToolStripMenuItem,
            this.setDataBankToolStripMenuItem,
            this.toggleAccumulatorSizeMToolStripMenuItem,
            this.toggleIndexSizeToolStripMenuItem,
            this.addCommentToolStripMenuItem,
            this.toolStripSeparator6,
            this.fixMisalignedInstructionsToolStripMenuItem});
            this.editToolStripMenuItem.Enabled = false;
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.Size = new System.Drawing.Size(39, 20);
            this.editToolStripMenuItem.Text = "Edit";
            // 
            // stepOverToolStripMenuItem
            // 
            this.stepOverToolStripMenuItem.Name = "stepOverToolStripMenuItem";
            this.stepOverToolStripMenuItem.Size = new System.Drawing.Size(205, 22);
            this.stepOverToolStripMenuItem.Text = "Step";
            // 
            // stepInToolStripMenuItem
            // 
            this.stepInToolStripMenuItem.Name = "stepInToolStripMenuItem";
            this.stepInToolStripMenuItem.Size = new System.Drawing.Size(205, 22);
            this.stepInToolStripMenuItem.Text = "Step In";
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(202, 6);
            // 
            // autoStepSafeToolStripMenuItem
            // 
            this.autoStepSafeToolStripMenuItem.Name = "autoStepSafeToolStripMenuItem";
            this.autoStepSafeToolStripMenuItem.Size = new System.Drawing.Size(205, 22);
            this.autoStepSafeToolStripMenuItem.Text = "Auto Step (Safe)";
            // 
            // autoStepHarshToolStripMenuItem
            // 
            this.autoStepHarshToolStripMenuItem.Name = "autoStepHarshToolStripMenuItem";
            this.autoStepHarshToolStripMenuItem.Size = new System.Drawing.Size(205, 22);
            this.autoStepHarshToolStripMenuItem.Text = "Auto Step (Harsh)";
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(202, 6);
            // 
            // gotoToolStripMenuItem
            // 
            this.gotoToolStripMenuItem.Name = "gotoToolStripMenuItem";
            this.gotoToolStripMenuItem.Size = new System.Drawing.Size(205, 22);
            this.gotoToolStripMenuItem.Text = "Goto...";
            // 
            // gotoEffectiveAddressToolStripMenuItem
            // 
            this.gotoEffectiveAddressToolStripMenuItem.Name = "gotoEffectiveAddressToolStripMenuItem";
            this.gotoEffectiveAddressToolStripMenuItem.Size = new System.Drawing.Size(205, 22);
            this.gotoEffectiveAddressToolStripMenuItem.Text = "Goto Effective Address";
            // 
            // gotoFirstUnreachedToolStripMenuItem
            // 
            this.gotoFirstUnreachedToolStripMenuItem.Name = "gotoFirstUnreachedToolStripMenuItem";
            this.gotoFirstUnreachedToolStripMenuItem.Size = new System.Drawing.Size(205, 22);
            this.gotoFirstUnreachedToolStripMenuItem.Text = "Goto First Unreached";
            // 
            // gotoNearUnreachedToolStripMenuItem
            // 
            this.gotoNearUnreachedToolStripMenuItem.Name = "gotoNearUnreachedToolStripMenuItem";
            this.gotoNearUnreachedToolStripMenuItem.Size = new System.Drawing.Size(205, 22);
            this.gotoNearUnreachedToolStripMenuItem.Text = "Goto Near Unreached";
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(202, 6);
            // 
            // selectMarkerToolStripMenuItem
            // 
            this.selectMarkerToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.unreachedToolStripMenuItem,
            this.opcodeToolStripMenuItem,
            this.operandToolStripMenuItem,
            this.bitDataToolStripMenuItem,
            this.graphicsToolStripMenuItem,
            this.musicToolStripMenuItem,
            this.emptyToolStripMenuItem,
            this.bitDataToolStripMenuItem1,
            this.wordPointerToolStripMenuItem,
            this.bitDataToolStripMenuItem2,
            this.longPointerToolStripMenuItem,
            this.bitDataToolStripMenuItem3,
            this.dWordPointerToolStripMenuItem,
            this.textToolStripMenuItem});
            this.selectMarkerToolStripMenuItem.Name = "selectMarkerToolStripMenuItem";
            this.selectMarkerToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.selectMarkerToolStripMenuItem.Text = "Select Marker";
            // 
            // markOneToolStripMenuItem
            // 
            this.markOneToolStripMenuItem.Name = "markOneToolStripMenuItem";
            this.markOneToolStripMenuItem.Size = new System.Drawing.Size(205, 22);
            this.markOneToolStripMenuItem.Text = "Mark One";
            // 
            // markManyToolStripMenuItem
            // 
            this.markManyToolStripMenuItem.Name = "markManyToolStripMenuItem";
            this.markManyToolStripMenuItem.Size = new System.Drawing.Size(205, 22);
            this.markManyToolStripMenuItem.Text = "Mark Many...";
            // 
            // toolStripSeparator5
            // 
            this.toolStripSeparator5.Name = "toolStripSeparator5";
            this.toolStripSeparator5.Size = new System.Drawing.Size(202, 6);
            // 
            // setDirectPageToolStripMenuItem
            // 
            this.setDirectPageToolStripMenuItem.Name = "setDirectPageToolStripMenuItem";
            this.setDirectPageToolStripMenuItem.Size = new System.Drawing.Size(205, 22);
            this.setDirectPageToolStripMenuItem.Text = "Set Direct Page...";
            // 
            // setDataBankToolStripMenuItem
            // 
            this.setDataBankToolStripMenuItem.Name = "setDataBankToolStripMenuItem";
            this.setDataBankToolStripMenuItem.Size = new System.Drawing.Size(205, 22);
            this.setDataBankToolStripMenuItem.Text = "Set Data Bank...";
            // 
            // toggleAccumulatorSizeMToolStripMenuItem
            // 
            this.toggleAccumulatorSizeMToolStripMenuItem.Name = "toggleAccumulatorSizeMToolStripMenuItem";
            this.toggleAccumulatorSizeMToolStripMenuItem.Size = new System.Drawing.Size(205, 22);
            this.toggleAccumulatorSizeMToolStripMenuItem.Text = "Toggle Accumulator Size";
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.visualMapToolStripMenuItem,
            this.graphicsWindowToolStripMenuItem,
            this.constantsToolStripMenuItem});
            this.viewToolStripMenuItem.Enabled = false;
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.viewToolStripMenuItem.Text = "View";
            // 
            // visualMapToolStripMenuItem
            // 
            this.visualMapToolStripMenuItem.Name = "visualMapToolStripMenuItem";
            this.visualMapToolStripMenuItem.Size = new System.Drawing.Size(167, 22);
            this.visualMapToolStripMenuItem.Text = "Visual Map";
            // 
            // graphicsWindowToolStripMenuItem
            // 
            this.graphicsWindowToolStripMenuItem.Name = "graphicsWindowToolStripMenuItem";
            this.graphicsWindowToolStripMenuItem.Size = new System.Drawing.Size(167, 22);
            this.graphicsWindowToolStripMenuItem.Text = "Graphics Window";
            // 
            // constantsToolStripMenuItem
            // 
            this.constantsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.decimalToolStripMenuItem,
            this.hexadecimalToolStripMenuItem,
            this.binaryToolStripMenuItem});
            this.constantsToolStripMenuItem.Name = "constantsToolStripMenuItem";
            this.constantsToolStripMenuItem.Size = new System.Drawing.Size(167, 22);
            this.constantsToolStripMenuItem.Text = "Constants";
            // 
            // decimalToolStripMenuItem
            // 
            this.decimalToolStripMenuItem.Name = "decimalToolStripMenuItem";
            this.decimalToolStripMenuItem.Size = new System.Drawing.Size(142, 22);
            this.decimalToolStripMenuItem.Text = "Decimal";
            // 
            // hexadecimalToolStripMenuItem
            // 
            this.hexadecimalToolStripMenuItem.Name = "hexadecimalToolStripMenuItem";
            this.hexadecimalToolStripMenuItem.Size = new System.Drawing.Size(142, 22);
            this.hexadecimalToolStripMenuItem.Text = "Hexadecimal";
            // 
            // binaryToolStripMenuItem
            // 
            this.binaryToolStripMenuItem.Name = "binaryToolStripMenuItem";
            this.binaryToolStripMenuItem.Size = new System.Drawing.Size(142, 22);
            this.binaryToolStripMenuItem.Text = "Binary";
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1});
            this.statusStrip1.Location = new System.Drawing.Point(0, 524);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(700, 22);
            this.statusStrip1.TabIndex = 2;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toggleIndexSizeToolStripMenuItem
            // 
            this.toggleIndexSizeToolStripMenuItem.Name = "toggleIndexSizeToolStripMenuItem";
            this.toggleIndexSizeToolStripMenuItem.Size = new System.Drawing.Size(205, 22);
            this.toggleIndexSizeToolStripMenuItem.Text = "Toggle Index Size";
            // 
            // addLabelToolStripMenuItem
            // 
            this.addLabelToolStripMenuItem.Name = "addLabelToolStripMenuItem";
            this.addLabelToolStripMenuItem.Size = new System.Drawing.Size(205, 22);
            this.addLabelToolStripMenuItem.Text = "Add Label...";
            // 
            // addCommentToolStripMenuItem
            // 
            this.addCommentToolStripMenuItem.Name = "addCommentToolStripMenuItem";
            this.addCommentToolStripMenuItem.Size = new System.Drawing.Size(205, 22);
            this.addCommentToolStripMenuItem.Text = "Add Comment...";
            // 
            // unreachedToolStripMenuItem
            // 
            this.unreachedToolStripMenuItem.Name = "unreachedToolStripMenuItem";
            this.unreachedToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.unreachedToolStripMenuItem.Text = "Unreached";
            // 
            // opcodeToolStripMenuItem
            // 
            this.opcodeToolStripMenuItem.Name = "opcodeToolStripMenuItem";
            this.opcodeToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.opcodeToolStripMenuItem.Text = "Opcode";
            // 
            // operandToolStripMenuItem
            // 
            this.operandToolStripMenuItem.Name = "operandToolStripMenuItem";
            this.operandToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.operandToolStripMenuItem.Text = "Operand";
            // 
            // bitDataToolStripMenuItem
            // 
            this.bitDataToolStripMenuItem.Name = "bitDataToolStripMenuItem";
            this.bitDataToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.bitDataToolStripMenuItem.Text = "8-Bit Data";
            // 
            // graphicsToolStripMenuItem
            // 
            this.graphicsToolStripMenuItem.Name = "graphicsToolStripMenuItem";
            this.graphicsToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.graphicsToolStripMenuItem.Text = "     Graphics";
            // 
            // musicToolStripMenuItem
            // 
            this.musicToolStripMenuItem.Name = "musicToolStripMenuItem";
            this.musicToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.musicToolStripMenuItem.Text = "     Music";
            // 
            // emptyToolStripMenuItem
            // 
            this.emptyToolStripMenuItem.Name = "emptyToolStripMenuItem";
            this.emptyToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.emptyToolStripMenuItem.Text = "     Empty";
            // 
            // bitDataToolStripMenuItem1
            // 
            this.bitDataToolStripMenuItem1.Name = "bitDataToolStripMenuItem1";
            this.bitDataToolStripMenuItem1.Size = new System.Drawing.Size(180, 22);
            this.bitDataToolStripMenuItem1.Text = "16-Bit Data";
            // 
            // wordPointerToolStripMenuItem
            // 
            this.wordPointerToolStripMenuItem.Name = "wordPointerToolStripMenuItem";
            this.wordPointerToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.wordPointerToolStripMenuItem.Text = "     Word Pointer";
            // 
            // bitDataToolStripMenuItem2
            // 
            this.bitDataToolStripMenuItem2.Name = "bitDataToolStripMenuItem2";
            this.bitDataToolStripMenuItem2.Size = new System.Drawing.Size(180, 22);
            this.bitDataToolStripMenuItem2.Text = "24-Bit Data";
            // 
            // longPointerToolStripMenuItem
            // 
            this.longPointerToolStripMenuItem.Name = "longPointerToolStripMenuItem";
            this.longPointerToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.longPointerToolStripMenuItem.Text = "     Long Pointer";
            // 
            // bitDataToolStripMenuItem3
            // 
            this.bitDataToolStripMenuItem3.Name = "bitDataToolStripMenuItem3";
            this.bitDataToolStripMenuItem3.Size = new System.Drawing.Size(180, 22);
            this.bitDataToolStripMenuItem3.Text = "32-Bit Data";
            // 
            // dWordPointerToolStripMenuItem
            // 
            this.dWordPointerToolStripMenuItem.Name = "dWordPointerToolStripMenuItem";
            this.dWordPointerToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.dWordPointerToolStripMenuItem.Text = "     DWord Pointer";
            // 
            // textToolStripMenuItem
            // 
            this.textToolStripMenuItem.Name = "textToolStripMenuItem";
            this.textToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.textToolStripMenuItem.Text = "Text";
            // 
            // toolStripSeparator6
            // 
            this.toolStripSeparator6.Name = "toolStripSeparator6";
            this.toolStripSeparator6.Size = new System.Drawing.Size(211, 6);
            // 
            // fixMisalignedInstructionsToolStripMenuItem
            // 
            this.fixMisalignedInstructionsToolStripMenuItem.Name = "fixMisalignedInstructionsToolStripMenuItem";
            this.fixMisalignedInstructionsToolStripMenuItem.Size = new System.Drawing.Size(214, 22);
            this.fixMisalignedInstructionsToolStripMenuItem.Text = "Fix Misaligned Instructions";
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.viewHelpToolStripMenuItem,
            this.aboutToolStripMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpToolStripMenuItem.Text = "Help";
            // 
            // viewHelpToolStripMenuItem
            // 
            this.viewHelpToolStripMenuItem.Name = "viewHelpToolStripMenuItem";
            this.viewHelpToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.viewHelpToolStripMenuItem.Text = "View Help";
            this.viewHelpToolStripMenuItem.Click += new System.EventHandler(this.viewHelpToolStripMenuItem_Click);
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.aboutToolStripMenuItem.Text = "About";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.aboutToolStripMenuItem_Click);
            // 
            // toolStripStatusLabel1
            // 
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(56, 17);
            this.toolStripStatusLabel1.Text = "000.000%";
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.Filter = "SNES ROM Images|*.smc;*.sfc|All files|*.*";
            // 
            // openFileDialog2
            // 
            this.openFileDialog2.Filter = "DiztinGUIsh Project Files|*.diz";
            // 
            // saveFileDialog1
            // 
            this.saveFileDialog1.Filter = "DiztinGUIsh Project Files|*.diz|All Files|*.*";
            this.saveFileDialog1.Title = "New Project.diz";
            // 
            // toolStripSeparator7
            // 
            this.toolStripSeparator7.Name = "toolStripSeparator7";
            this.toolStripSeparator7.Size = new System.Drawing.Size(181, 6);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // MainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(700, 546);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.MinimumSize = new System.Drawing.Size(716, 200);
            this.Name = "MainWindow";
            this.Text = "DiztinGUIsh";
            this.Load += new System.EventHandler(this.MainWindow_Load);
            this.SizeChanged += new System.EventHandler(this.MainWindow_SizeChanged);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnAlias;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnPC;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnChar;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnHex;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnInstruction;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnEA;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnFlag;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnDB;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnDP;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnM;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnX;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnComment;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem newProjectToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openProjectToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveProjectToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveProjectAsToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem exportLogToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importCDLToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem stepOverToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem stepInToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem autoStepSafeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem autoStepHarshToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem gotoToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem gotoEffectiveAddressToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem gotoFirstUnreachedToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem gotoNearUnreachedToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripMenuItem selectMarkerToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem markOneToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem markManyToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
        private System.Windows.Forms.ToolStripMenuItem setDirectPageToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem setDataBankToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toggleAccumulatorSizeMToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem visualMapToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem graphicsWindowToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem constantsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem decimalToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem hexadecimalToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem binaryToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addLabelToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toggleIndexSizeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addCommentToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem unreachedToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem opcodeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem operandToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bitDataToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem graphicsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem musicToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem emptyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bitDataToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem wordPointerToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bitDataToolStripMenuItem2;
        private System.Windows.Forms.ToolStripMenuItem longPointerToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bitDataToolStripMenuItem3;
        private System.Windows.Forms.ToolStripMenuItem dWordPointerToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem textToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator6;
        private System.Windows.Forms.ToolStripMenuItem fixMisalignedInstructionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewHelpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.OpenFileDialog openFileDialog2;
        private System.Windows.Forms.SaveFileDialog saveFileDialog1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator7;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
    }
}


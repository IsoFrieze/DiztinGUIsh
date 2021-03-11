using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Diz.Core.model;
using DiztinGUIsh.Properties;
using DiztinGUIsh.window;

namespace DiztinGUIsh.window2
{
    public partial class StartForm : Form
    {
        public App App = new();

        public StartForm()
        {
            InitializeComponent();
            
            // HACK. open last file.
            if (!string.IsNullOrEmpty(Settings.Default.LastOpenedFile))
                App.OpenFileWithNewView(Settings.Default.LastOpenedFile);
        }

        public string PromptForOpenFile()
        {
            var openProjectFile = new OpenFileDialog
            {
                Filter = "DiztinGUIsh Project Files|*.diz;*.dizraw|All Files|*.*",
            };
            return openProjectFile.ShowDialog() != DialogResult.OK ? "" : openProjectFile.FileName;
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var filename = PromptForOpenFile();
            if (string.IsNullOrEmpty(filename))
                return;

            App.OpenFileWithNewView(filename);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            
        }

        private void newViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            
        }

        private void newViewBankC0ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            
        }

        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiztinGUIsh
{
    public partial class MainWindow : Form
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWindow_SizeChanged(object sender, EventArgs e)
        {
            dataGridView1.Height = this.Height - 85;
            dataGridView1.Width = this.Width - 16;
        }

        private void MainWindow_Load(object sender, EventArgs e)
        {
        }

        public void UpdateWindowTitle()
        {
            this.Text =
                (Project.unsavedChanges ? "*" : "") + 
                (Project.currentFile == null ? "New Project" : Project.currentFile) +
                " - DiztinGUIsh";
        }

        private bool ContinueUnsavedChanges()
        {
            if (Project.unsavedChanges)
            {
                DialogResult confirm = MessageBox.Show("You have unsaved changes. They will be lost if you continue.", "Unsaved Changes", MessageBoxButtons.OKCancel);
                return confirm == DialogResult.OK;
            }
            return true;
        }

        public void TriggerSaveOptions(bool save, bool saveas)
        {
            saveProjectToolStripMenuItem.Enabled = save;
            saveProjectAsToolStripMenuItem.Enabled = saveas;
        }

        private void newProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ContinueUnsavedChanges())
            {
                DialogResult result = openFileDialog1.ShowDialog();
                if (result == DialogResult.OK)
                {
                    if (Project.NewProject(openFileDialog1.FileName))
                    {
                        TriggerSaveOptions(false, true);
                        UpdateWindowTitle();
                    }
                }
            }
        }

        private void openProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ContinueUnsavedChanges())
            {
                DialogResult result = openFileDialog2.ShowDialog();
                if (result == DialogResult.OK)
                {
                    if (Project.TryOpenProject(openFileDialog2.FileName))
                    {
                        TriggerSaveOptions(true, true);
                        UpdateWindowTitle();
                    }
                }
            }
        }

        private void saveProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Project.SaveProject(Project.currentFile);
            UpdateWindowTitle();
        }

        private void saveProjectAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result = saveFileDialog1.ShowDialog();
            if (result == DialogResult.OK && saveFileDialog1.FileName != "")
            {
                Project.SaveProject(saveFileDialog1.FileName);
                TriggerSaveOptions(true, true);
                UpdateWindowTitle();
            }
        }

        private void viewHelpToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            About about = new About();
            about.ShowDialog();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ContinueUnsavedChanges())
            {
                Application.Exit();
            }
        }

        private void dataGridView1_KeyDown(object sender, KeyEventArgs e)
        {
        }
    }
}

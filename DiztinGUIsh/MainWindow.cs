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

        private bool ContinueUnsavedChanges()
        {
            if (Project.unsavedChanges)
            {
                DialogResult confirm = MessageBox.Show("You have unsaved changes. They will be lost if you continue.", "Unsaved Changes", MessageBoxButtons.OKCancel);
                return confirm == DialogResult.OK;
            }
            return true;
        }

        private void newProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ContinueUnsavedChanges())
            {
                DialogResult result = openFileDialog1.ShowDialog();
                if (result == DialogResult.OK)
                {
                    Project.NewProject(openFileDialog1.FileName);
                }
            }
        }
    }
}

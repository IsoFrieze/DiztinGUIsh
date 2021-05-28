using System;
using System.Windows.Forms;

namespace DiztinGUIsh.window.dialog
{
    public partial class InOutPointChecker : Form
    {
        public InOutPointChecker()
        {
            InitializeComponent();
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void rescan_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }
    }
}

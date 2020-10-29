using System;
using System.Windows.Forms;

namespace DiztinGUIsh
{
    public partial class InOutPointChecker : Form
    {
        public InOutPointChecker()
        {
            InitializeComponent();
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void rescan_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
        }
    }
}

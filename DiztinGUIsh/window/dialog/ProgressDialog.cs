using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiztinGUIsh.window.dialog
{
    public partial class ProgressDialog : Form
    {
        public ProgressDialog()
        {
            InitializeComponent();
            progressBar1.Value = 0;
            progressBar1.Maximum = 100;
        }

        public void UpdateProgress(int i)
        {
            this.InvokeIfRequired(() =>
            {
                progressBar1.Value = i;
                var percentDone = (int)(100*((float)progressBar1.Value / (float)progressBar1.Maximum));

                label2.Text = $@"{percentDone}%";
            });
        }
    }
}


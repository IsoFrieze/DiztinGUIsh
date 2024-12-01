using System;
using System.Windows.Forms;
using Diz.Core.model.snes;
using Diz.Cpu._65816;

namespace DiztinGUIsh.window.dialog
{
    public partial class MisalignmentChecker : Form
    {
        private Data Data { get; set; }
        public MisalignmentChecker(Data data)
        {
            Data = data;
            InitializeComponent();
        }

        private void cancel_Click(object sender, EventArgs e) => Close();

        private void buttonScan_Click(object sender, EventArgs e)
        {
            var snesData = Data.GetSnesApi();
            if (snesData == null)
                return;

            var (numFound, outputTextLog) = snesData.GenerateMisalignmentReport();
            textLog.Text = outputTextLog;
        }

        private void buttonFix_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }
    }
}
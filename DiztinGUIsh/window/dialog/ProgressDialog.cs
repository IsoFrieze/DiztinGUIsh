using System;
using System.Windows.Forms;
using Diz.Core.util;
using DiztinGUIsh.controller;
using DiztinGUIsh.util;

namespace DiztinGUIsh.window.dialog
{
    public partial class ProgressDialog : Form, IProgressView
    {
        public bool IsMarquee
        {
            get => isMarquee;
            set => this.InvokeIfRequired(() => UpdateProgressBarStyle(value));
        }
        
        public string TextOverride
        {
            get => textOverride;
            set => this.InvokeIfRequired(() => UpdateTextOverride(value));
        }
        
        private bool isMarquee;
        private string textOverride;

        public ProgressDialog()
        {
            InitializeComponent();
            progressBar1.Value = 0;
            progressBar1.Maximum = 100;
        }

        private void UpdateProgressBarStyle(bool isMarqueeType)
        {
            isMarquee = isMarqueeType;
            progressBar1.Style = isMarqueeType
                ? ProgressBarStyle.Marquee
                : ProgressBarStyle.Continuous;
        }

        private void UpdateTextOverride(string value)
        {
            textOverride = value;
            UpdateProgressText();
        }
        
        public void Report(int i)
        {
            this.InvokeIfRequired(() =>
            {
                progressBar1.Value = i;
                UpdateProgressText();
            });
        }

        private void UpdateProgressText()
        {
            if (textOverride != null)
            {
                lblStatusText.Text = textOverride;
                return;
            }

            var percentDone = (int) (100 * (progressBar1.Value / (float) progressBar1.Maximum));
            lblStatusText.Text = $@"{percentDone}%";
        }

        public void SignalJobIsDone() => this.InvokeIfRequired(Close);
        public bool PromptDialog() => ShowDialog() == DialogResult.OK;
    }
}
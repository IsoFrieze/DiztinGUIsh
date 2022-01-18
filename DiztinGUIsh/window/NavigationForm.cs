using System;
using System.Windows.Forms;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Ui.Winforms.util;

namespace DiztinGUIsh.window
{
    public partial class NavigationForm : Form
    {
        private IDizDocument document;
        private ISnesNavigation snesNavigation;

        public IDizDocument Document
        {
            get => document;
            set
            {
                document = value;
                navigationCtrl.Document = Document;
            }
        }

        public ISnesNavigation SnesNavigation
        {
            get => snesNavigation;
            set
            {
                snesNavigation = value;
                navigationCtrl.SnesNavigation = snesNavigation;
            }
        }

        public NavigationForm()
        {
            InitializeComponent();
        }

        private void Navigation_Load(object sender, EventArgs e)
        {
            
        }

        public void Navigate(bool forwardDirection)
        {
            navigationCtrl.Navigate(forwardDirection);
        }

        private void navigationCtrl_Load(object sender, EventArgs e)
        {

        }
    }
}

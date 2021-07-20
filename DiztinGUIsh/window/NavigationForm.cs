using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DiztinGUIsh.controller;
using DiztinGUIsh.window.usercontrols;

namespace DiztinGUIsh.window
{
    public partial class NavigationForm : Form
    {
        private DizDocument document;
        private ISnesNavigation snesNavigation;

        public DizDocument Document
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

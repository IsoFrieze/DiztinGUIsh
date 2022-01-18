using System;
using System.ComponentModel;
using System.Windows.Forms;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core.util;
using Diz.Ui.Winforms.util;

namespace DiztinGUIsh.window.usercontrols
{
    public partial class NavigationUserControl : UserControl
    {
        private IDizDocument document;
        private ISnesNavigation snesNavigation;

        public IDizDocument Document
        {
            get => document;
            set
            {
                document = value;
                navigationEntryBindingSource.DataSource = Document?.NavigationHistory;

                if (navigationEntryBindingSource.DataSource != null)
                {
                    navigationEntryBindingSource.ListChanged += NavigationEntryBindingSourceOnListChanged;
                    navigationEntryBindingSource.PositionChanged += NavigationEntryBindingSourceOnPositionChanged;
                }
            }
        }

        private void NavigationEntryBindingSourceOnPositionChanged(object sender, EventArgs e)
        {
            
        }

        private void NavigationEntryBindingSourceOnListChanged(object sender, ListChangedEventArgs e)
        {
            if (navigationEntryBindingSource.Count == 0)
                return;
            
            SelectDataGridRow(navigationEntryBindingSource.Count - 1);
        }

        public ISnesNavigation SnesNavigation
        {
            get => snesNavigation;
            set
            {
                snesNavigation = value;
            }
        }

        public NavigationUserControl()
        {
            InitializeComponent();

            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.MultiSelect = false;
        }

        public int SelectedIndex => navigationEntryBindingSource.Position; // dataGridView1.SelectedRows[0].Index;

        public void Navigate(bool forwardDirection)
        {
            if (navigationEntryBindingSource == null || navigationEntryBindingSource.Count == 0)
                return;
            
            var navigationEntryToUse = 
                Util.ClampIndex(SelectedIndex + (forwardDirection ? 1 : -1),
                navigationEntryBindingSource.Count);

            NavigateToEntry(navigationEntryToUse);
            SelectDataGridRow(navigationEntryToUse);
        }

        private void NavigateToEntry(int indexToUse)
        {
            NavigateToEntry(GetNavigationEntry(indexToUse));
        }

        private void NavigateToEntry(NavigationEntry navigationEntry)
        {
            var newSnesAddress = navigationEntry?.SnesOffset ?? -1;
            if (newSnesAddress == -1)
                return;
            
            var pcOffset = Document.Project.Data.ConvertSnesToPc(newSnesAddress);
            if (pcOffset == -1) 
                return;
            
            SnesNavigation.SelectOffset(pcOffset);
        }

        private NavigationEntry GetNavigationEntry(int index)
        {
            if (index < 0 || index >= navigationEntryBindingSource.Count)
                return null;
            
            return (NavigationEntry) navigationEntryBindingSource[index];
        }

        private void SelectDataGridRow(int index)
        {
            if (index < 0 || index >= navigationEntryBindingSource.Count)
                return;
            
            navigationEntryBindingSource.Position = index;
        }

        private void navigationEntryBindingSource_CurrentChanged(object sender, System.EventArgs e) => 
            NavigateToCurrentNavigationEntry();

        private void dataGridView1_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e) => 
            NavigateToCurrentNavigationEntry();

        private void NavigateToCurrentNavigationEntry()
        {
            if (navigationEntryBindingSource != null)
                NavigateToEntry(navigationEntryBindingSource.Position);
        }

        private void btnBack_Click(object sender, EventArgs e) => Navigate(false);
        private void btnForward_Click(object sender, EventArgs e) => Navigate(true);
        private void btnClearHistory_Click(object sender, EventArgs e) => navigationEntryBindingSource?.Clear();
    }
}

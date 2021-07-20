using System.ComponentModel;
using System.Windows.Forms;
using Diz.Core.util;
using DiztinGUIsh.controller;

namespace DiztinGUIsh.window.usercontrols
{
    public partial class NavigationUserControl : UserControl
    {
        private DizDocument document;
        private ISnesNavigation snesNavigation;

        public DizDocument Document
        {
            get => document;
            set
            {
                document = value;
                navigationEntryBindingSource.DataSource = Document?.NavigationHistory;
                
                if (navigationEntryBindingSource.DataSource != null)
                    navigationEntryBindingSource.ListChanged += NavigationEntryBindingSourceOnListChanged;
            }
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
            if (navigationEntryBindingSource.Count == 0)
                return;
            
            var indexToUse = 
                Util.ClampIndex(SelectedIndex + (forwardDirection ? 1 : -1),
                navigationEntryBindingSource.Count);

            var newSnesAddress = ((NavigationEntry) navigationEntryBindingSource[indexToUse]).SnesOffset;

            var pcOffset = Document.Project.Data.ConvertSnesToPc(newSnesAddress);
            SnesNavigation.SelectOffset(pcOffset);
            
            SelectDataGridRow(indexToUse);
        }

        private void SelectDataGridRow(int index)
        {
            if (index < 0 || index >= navigationEntryBindingSource.Count)
                return;
            
            navigationEntryBindingSource.Position = index;
        }

        private void navigationEntryBindingSource_CurrentChanged(object sender, System.EventArgs e)
        {

        }
    }
}

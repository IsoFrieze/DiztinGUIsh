using System;
using System.Diagnostics;
using Diz.Core.model;
using Diz.Core.util;

namespace DiztinGUIsh.util
{
    // controls what rows are visible and scrolls intelligently based on our offset
    public class DataSubsetWithSelection<TRow, TItem> : DataSubset<TRow, TItem>
    {
        public TRow SelectedRow =>
            GetOrCreateRow(SelectedLargeIndex);
        
        public int SelectedRowIndex => 
            GetRowIndexFromLargeOffset(SelectedLargeIndex);

        // when this display mode is enabled, the selection will not be allowed to change 
        // outside the range of the currently visible rows
        public bool AlwaysEnsureSelectionWithinVisibleRows
        {
            get => clampSelectionToVisibleRows;
            set
            {
                if (value)
                    ScrollToShowSelection = false;
                
                this.SetField(ref clampSelectionToVisibleRows, value);
                
                ClampSelectionIfNeeded();
            }
        }

        // when this display mode is enabled, when the selection is changed,
        // the starting and ending rows will change to ensure the selected index is visible
        public bool ScrollToShowSelection
        {
            get => autoScrollToShowSelection;
            set
            {
                if (value)
                    AlwaysEnsureSelectionWithinVisibleRows = false;

                this.SetField(ref autoScrollToShowSelection, value);

                EnsureViewContainsLargeIndex(SelectedLargeIndex);
            }
        }
        
        private void EnsureViewContainsLargeIndex(int largeIndex)
        {
            if (RowCount == 0)
                return;

            Debug.Assert(IsValidLargeOffset(largeIndex));

            if (largeIndex < StartingRowLargeIndex)
            {
                StartingRowLargeIndex = largeIndex;
            } 
            else if (largeIndex > EndingRowLargeIndex)
            {
                EndingRowLargeIndex = largeIndex;
            }
        }

        public int SelectedLargeIndex
        {
            get => selectedLargeIndex;
            set
            {
                if (!IsValidLargeOffset(value))
                    throw new ArgumentException("Invalid large value");

                var clampedValue = GetClampedIndexIfNeeded(value);
                
                if (!NotifyPropertyChangedExtensions.FieldCompare(selectedLargeIndex, clampedValue)) 
                    return;
            
                selectedLargeIndex = clampedValue;
                EnsureViewContainsLargeIndex(selectedLargeIndex);
            
                OnPropertyChanged();
            }
        }

        public int LargestPossibleStartingLargeIndex => Items.Count - RowCount;

        private int selectedLargeIndex;

        // display modes, pick one or the other
        private bool autoScrollToShowSelection = true;
        private bool clampSelectionToVisibleRows;


        private void EnsureViewContainsSelectionIfNeeded()
        {
            if (ScrollToShowSelection)
                EnsureViewContainsLargeIndex(SelectedLargeIndex);
        }

        private void ClampSelectionIfNeeded() => SelectedLargeIndex = GetClampedIndexIfNeeded(SelectedLargeIndex);


        private int GetClampedIndexIfNeeded(int largeIndex) =>
            !clampSelectionToVisibleRows 
                ? largeIndex
                : GetLargeIndexClampedToVisibleRows(largeIndex);

        public int GetLargeIndexClampedToVisibleRows(int largeIndexToClamp) => 
            Util.ClampIndex(largeIndexToClamp, StartingRowLargeIndex, EndingRowLargeIndex);

        public void SelectRow(int rowIndex) => 
            SelectedLargeIndex = GetLargeOffsetFromRowOffset(rowIndex);

        protected override void UpdateDimensions(int newRowCount, int newStartingRowLargeIndex, Action updateAction)
        {
            base.UpdateDimensions(newRowCount, newStartingRowLargeIndex, updateAction);
            ClampSelectionIfNeeded();
        }
    }
}
using System;
using System.Diagnostics;
using Diz.Core.util;

namespace Diz.Core.datasubset
{
    // controls what rows are visible and scrolls intelligently based on our offset
    public class DataSubsetWithSelection<TRow, TItem> : DataSubset<TRow, TItem>
    {
        public TRow SelectedRow =>
            RowValueNeededForLargeOffset(SelectedLargeIndex);
        
        public int SelectedRowIndex => 
            GetRowIndexFromLargeOffset(SelectedLargeIndex);

        // when set: when the start or end range changes, the selected row will be
        // clamped to be within the Start..End range.
        public bool WindowResizeKeepsSelectionInRange
        {
            get => windowResizeKeepsSelectionInRange;
            set
            {
                this.SetField(ref windowResizeKeepsSelectionInRange, value);
                ClampSelectionIfNeeded();
            }
        }
        private bool windowResizeKeepsSelectionInRange;

        // when set: when the selection is changed, the start and end points will move
        // to keep the selection inside the range.
        public bool EnsureBoundariesEncompassWhenSelectionChanges
        {
            get => ensureBoundariesEncompassWhenSelectionChanges;
            set
            {
                this.SetField(ref ensureBoundariesEncompassWhenSelectionChanges, value);
                EnsureViewContainsLargeIndex(SelectedLargeIndex);
            }
        }
        private bool ensureBoundariesEncompassWhenSelectionChanges = true;
        
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

                // var clampedValue = GetClampedIndexIfNeeded(value);

                if (NotifyPropertyChangedExtensions.FieldIsEqual(selectedLargeIndex, value))
                    return;

                selectedLargeIndex = value;

                EnsureViewContainsSelectionIfNeeded();
            
                OnPropertyChanged();
            }
        }

        public int LargestPossibleStartingLargeIndex => Items.Count - RowCount;

        private int selectedLargeIndex;


        private void EnsureViewContainsSelectionIfNeeded()
        {
            if (ensureBoundariesEncompassWhenSelectionChanges)
                EnsureViewContainsLargeIndex(SelectedLargeIndex);
        }

        private void ClampSelectionIfNeeded() => 
            SelectedLargeIndex = GetClampedIndexIfNeeded(SelectedLargeIndex);
        
        private int GetClampedIndexIfNeeded(int largeIndex) =>
            !windowResizeKeepsSelectionInRange 
                ? largeIndex
                : GetLargeIndexClampedToVisibleRows(largeIndex);

        public int GetLargeIndexClampedToVisibleRows(int largeIndexToClamp) => 
            Util.Clamp(largeIndexToClamp, StartingRowLargeIndex, EndingRowLargeIndex);

        public void SelectRow(int rowIndex) => 
            SelectedLargeIndex = GetLargeOffsetFromRowOffset(rowIndex);

        protected override void UpdateDimensions(int newRowCount, int newStartingRowLargeIndex, Action updateAction)
        {
            base.UpdateDimensions(newRowCount, newStartingRowLargeIndex, updateAction);
            ClampSelectionIfNeeded();
        }
    }
}
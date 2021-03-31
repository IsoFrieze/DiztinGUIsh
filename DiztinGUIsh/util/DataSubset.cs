using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Diz.Core.model;
using Diz.Core.util;
using DiztinGUIsh.window2;
using JetBrains.Annotations;

namespace DiztinGUIsh.util
{
    // controls what rows are visible and scrolls intelligently based on our offset
    public class DataSubsetWithSelection : DataSubset
    {
        public RomByteDataGridRow SelectedRow =>
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

        public static DataSubsetWithSelection Create(Data data, List<RomByteData> romBytes, IBytesGridViewer<RomByteData> view)
        {
            return new()
            {
                Data = data,
                RomBytes = romBytes,
                RowLoader = new DataSubsetLookaheadCacheLoader()
                {
                    View = view
                }
            };
        }

        public void SelectRow(int rowIndex) => 
            SelectedLargeIndex = GetLargeOffsetFromRowOffset(rowIndex);

        protected override void UpdateDimensions(int newRowCount, int newStartingRowLargeIndex, Action updateAction)
        {
            base.UpdateDimensions(newRowCount, newStartingRowLargeIndex, updateAction);
            ClampSelectionIfNeeded();
        }
    }

    public class DataSubset : INotifyPropertyChangedExt
    {
        public Data Data
        {
            get => data;
            set
            {
                DropRowCache();
                this.SetField(PropertyChanged, ref data, value);
            }
        }

        // must be a subset of .Data
        public List<RomByteData> RomBytes
        {
            get => romBytes;
            set
            {
                DropRowCache();
                this.SetField(PropertyChanged, ref romBytes, value);
            }
        }

        public DataSubsetLookaheadCacheLoaderBase RowLoader { get; init; }

        // rows (relative)
        public int StartingRowLargeIndex
        {
            get => startingRowLargeIndex;
            set
            {
                if (Data == null)
                    throw new ArgumentException("Data must be set before setting view dimensions");
            
                if (RomBytes == null)
                    throw new ArgumentException("RomBytes must be set before setting view dimensions");
                
                if (!IsValidLargeOffset(value))
                    throw new ArgumentException("StartingRowLargeIndex is out of range");

                // validate window range is OK.
                if (value + RowCount > RomBytes.Count)
                    throw new ArgumentException("Window size is out of range");
                
                UpdateDimensions(RowCount, value, 
                    () => this.SetField(PropertyChanged, ref startingRowLargeIndex, value));
            }
        }

        // zero is OK.
        public int RowCount
        {
            get => rowCount;
            set
            {
                if (value < 0) 
                    throw new ArgumentOutOfRangeException(nameof(value));
                
                if (Data == null)
                    throw new ArgumentException("Data must be set before setting view dimensions");
            
                if (RomBytes == null)
                    throw new ArgumentException("RomBytes must be set before setting view dimensions");

                if (value != 0 && !IsValidLargeOffset(value - 1))
                    throw new ArgumentException("Count out of range");

                // validate window range is OK.
                if (!IsValidLargeOffset(StartingRowLargeIndex) || StartingRowLargeIndex + value > RomBytes.Count)
                    throw new ArgumentException("Window size is out of range");

                UpdateDimensions(value, StartingRowLargeIndex, 
                    () => this.SetField(PropertyChanged, ref rowCount, value));
            }
        }

        protected virtual void UpdateDimensions(int newRowCount, int newStartingRowLargeIndex, Action updateAction)
        {
            if (newRowCount != RowCount || newStartingRowLargeIndex != StartingRowLargeIndex)
                OnWindowDimensionsChanging(newStartingRowLargeIndex, newRowCount);

            updateAction();
        }

        // called right before we change StartingRowLargeIndex and RowCount
        private void OnWindowDimensionsChanging(int newRowStartingIndex, int newRowCount)
        {
            DropRowCache();
        }
        
        public int EndingRowLargeIndex
        {
            get => StartingRowLargeIndex + RowCount - 1;
            set => StartingRowLargeIndex = value - RowCount + 1;
        }
        
        // main idea here is, this list never changes until we scroll, in which case we drop and re-add
        // everything.  recalculating this list should never do anything that involves a lot of processing.
        // instead, we'll leave the heavy lifting to cachedRows, which can do fancier things if needed
        // like predict which rows might be needed later.
        public List<RomByteDataGridRow> OutputRows
        {
            get
            {
                if (outputRows != null)
                    return outputRows;

                DropRowCache();
                CacheRows();

                return outputRows;
            }
            
            private set => this.SetField(ref outputRows, value);
        }

        private int startingRowLargeIndex;
        private int rowCount;
        private List<RomByteDataGridRow> outputRows;
        private Data data;
        private List<RomByteData> romBytes;

        // this only ever needs to happen when the dimensions change
        // if startingIndex and count don't change, this doesn't need to be recalculated.
        private void CacheRows()
        {
            Debug.Assert(outputRows == null);
            
            outputRows = new List<RomByteDataGridRow>(RowCount);
            
            RowLoader.OnBigWindowChangeStart(this);
            for (var i = StartingRowLargeIndex; i < StartingRowLargeIndex + RowCount; ++i)
            {
                outputRows.Add(GetOrCreateRow(i));
            }
            RowLoader.OnBigWindowChangeFinished(this);
        }

        private void DropRowCache() => outputRows = null;

        // key thing: we cache outputRows as long as the view doesn't change.
        // RowLoader will cache both the visible rows and potentially lots more of the most
        // recently loaded rows as well.
        //
        // the goal is: for small amounts of scrolling, make sure repopulating outputRows
        // is a quick operation. this will be true if RowLoader does a good job saving recently
        // cached rows.
        //
        // this also keeps the complex caching logic can stay out of this class and in RowLoader.
        protected RomByteDataGridRow GetOrCreateRow(int largeOffset) =>
            RowLoader.GetOrCreateRow(largeOffset, this);

        private RomByteDataGridRow GetOrCreateRowAtRow(int row) =>
            GetOrCreateRow(GetLargeOffsetFromRowOffset(row));

        private bool IsRowOffsetValid(int rowOffset) =>
            rowOffset >= 0 && rowOffset < RowCount;

        private bool IsLargeOffsetContainedInVisibleRows(int largeOffset) =>
            largeOffset >= startingRowLargeIndex && largeOffset <= EndingRowLargeIndex;

        protected bool IsValidLargeOffset(int largeOffset) =>
            largeOffset >= 0 && largeOffset < RomBytes?.Count;

        public int GetRowIndexFromLargeOffset(int largeOffset) =>
            !IsLargeOffsetContainedInVisibleRows(largeOffset)
                ? -1
                : largeOffset - startingRowLargeIndex;

        protected int GetLargeOffsetFromRowOffset(int rowOffset) =>
            !IsRowOffsetValid(rowOffset)
                ? -1
                : rowOffset + startingRowLargeIndex;

        public event PropertyChangedEventHandler? PropertyChanged;

        [NotifyPropertyChangedInvocator]
        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
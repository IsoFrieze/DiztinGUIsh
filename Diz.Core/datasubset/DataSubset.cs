using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Diz.Core.util;
using JetBrains.Annotations;

namespace Diz.Core.datasubset
{
    public interface IDataSubsetLoader<TRow, TItem>
    {
        // provide a row (either retrieve from cache or make a new one, either way)
        TRow RowValueNeeded(int largeOffset, DataSubset<TRow, TItem> subset);
        
        void OnBigWindowChangeStart(DataSubset<TRow, TItem> subset);
        void OnBigWindowChangeFinished(DataSubset<TRow, TItem> subset);
    }

    public class DataSubset<TRow, TItem> : INotifyPropertyChangedExt
    {
        // the full range of items to pick from.
        // anything that deals with "largeIndex" refers to an index into THIS list.
        //
        // note: client may filter or sort this list ahead of time, it doesn't have to be 1:1 with the underlying data
        public List<TItem> Items
        {
            get => items;
            set
            {
                DropRowCache();
                this.SetField(PropertyChanged, ref items, value);
            }
        }

        public IDataSubsetLoader<TRow, TItem> RowLoader { get; init; }

        // rows (relative)
        public int StartingRowLargeIndex
        {
            get => startingRowLargeIndex;
            set
            {
                if (Items == null)
                    throw new ArgumentException("RomBytes must be set before setting view dimensions");
                
                if (!IsValidLargeOffset(value))
                    throw new ArgumentException("StartingRowLargeIndex is out of range");

                // validate window range is OK.
                if (value + RowCount > Items.Count)
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
                    throw new ArgumentOutOfRangeException(nameof(RowCount));
                
                if (Items == null)
                    throw new ArgumentException("RomBytes must be set before setting view dimensions");

                if (value != 0 && !IsValidLargeOffset(value - 1))
                    throw new ArgumentException("Count out of range");

                // validate window range is OK.
                if (!IsValidLargeOffset(StartingRowLargeIndex))
                    throw new ArgumentException("starting large index is out of range");
                        
                if (StartingRowLargeIndex + value > Items.Count)
                {
                    EndingRowLargeIndex = Items.Count - 1;
                }

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
        public List<TRow> OutputRows
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
        private List<TRow> outputRows;
        private List<TItem> items;

        // this only ever needs to happen when the dimensions change
        // if startingIndex and count don't change, this doesn't need to be recalculated.
        private void CacheRows()
        {
            Debug.Assert(outputRows == null);
            
            outputRows = new List<TRow>(RowCount);
            
            RowLoader.OnBigWindowChangeStart(this);
            for (var i = StartingRowLargeIndex; i < StartingRowLargeIndex + RowCount; ++i)
            {
                var newRow = RowValueNeededForLargeOffset(i);
                outputRows.Add(newRow);
            }
            SetNotifyChangedForAllRows(register: true);
            RowLoader.OnBigWindowChangeFinished(this);
        }

        private void DropRowCache()
        {
            if (outputRows == null)
                return;

            SetNotifyChangedForAllRows(register: false);

            outputRows = null;
        }

        private void SetNotifyChangedForAllRows(bool register)
        {
            foreach (var row in outputRows)
            {
                if (!(row is INotifyPropertyChanged iNotify))
                    continue;

                if (register)
                    iNotify.PropertyChanged += OnRowPropertyChanged;
                else
                    iNotify.PropertyChanged -= OnRowPropertyChanged;
            }
        }

        private void OnRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // underlying data in one of the visible rows just changed, so pass that along so
            // listeners can get a notification that they should refresh the data.
            
            PropertyChanged?.Invoke(sender, e);
        }

        // key thing: this class (DataSubset) will itself cache the current outputRows
        // which don't need to change as long as the view doesn't change.
        //
        // RowLoader's job is:
        // - must cache all visible rows
        // - optionally, selectively cache some rows no longer in view anymore
        //
        // the goal is: for small amounts of scrolling, make sure repopulating outputRows
        // is a quick operation. this will be true if RowLoader does a good job saving recently
        // cached rows and predicting which ones might be needed soon.
        //
        // this also keeps the complex caching logic can stay out of this class and in RowLoader.
        //
        // example: if a user is looking at 10 rows in the middle of a 100 count data source,
        // the screen GUI only need 10 row objects to exist.  however, RowLoader might choose to also cache
        // an extra +/- 25 most recently used and rows that might be probably used in the near future.
        // so if the user is scrolling around in the same area, they might hit some of the non-visible cache
        // when rows are needed. that will speed up the GUI operations.
        //
        // if needed, in the future, predictive row caching could be done on a background thread as well.
        protected TRow RowValueNeededForLargeOffset(int largeOffset) =>
            RowLoader.RowValueNeeded(largeOffset, this);

        private TRow RowValueNeededForRowIndex(int row) =>
            RowValueNeededForLargeOffset(GetLargeOffsetFromRowOffset(row));

        public bool IsRowOffsetValid(int rowOffset) =>
            rowOffset >= 0 && rowOffset < RowCount;

        public bool IsLargeOffsetContainedInVisibleRows(int largeOffset) =>
            largeOffset >= startingRowLargeIndex && largeOffset <= EndingRowLargeIndex;

        public bool IsValidLargeOffset(int largeOffset) =>
            largeOffset >= 0 && largeOffset < Items?.Count;

        public int GetRowIndexFromLargeOffset(int largeOffset) =>
            !IsLargeOffsetContainedInVisibleRows(largeOffset)
                ? -1
                : largeOffset - startingRowLargeIndex;

        public int GetLargeOffsetFromRowOffset(int rowOffset) =>
            !IsRowOffsetValid(rowOffset)
                ? -1
                : rowOffset + startingRowLargeIndex;

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
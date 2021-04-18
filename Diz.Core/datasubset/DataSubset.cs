using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Diz.Core.util;
using JetBrains.Annotations;

namespace Diz.Core.datasubset
{
    public abstract class DataSubsetLookaheadCacheLoaderBase<TRow, TItem>
    {
        public abstract TRow GetOrCreateRow(int largeOffset, DataSubset<TRow, TItem> subset);
        public abstract void OnBigWindowChangeStart(DataSubset<TRow, TItem> subset);
        public abstract void OnBigWindowChangeFinished(DataSubset<TRow, TItem> subset);
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

        public DataSubsetLookaheadCacheLoaderBase<TRow, TItem> RowLoader { get; init; }

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
                    throw new ArgumentOutOfRangeException(nameof(value));
                
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
                var newRow = GetOrCreateRow(i);
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

        // key thing: we cache outputRows as long as the view doesn't change.
        // RowLoader will cache both the visible rows and potentially lots more of the most
        // recently loaded rows as well.
        //
        // the goal is: for small amounts of scrolling, make sure repopulating outputRows
        // is a quick operation. this will be true if RowLoader does a good job saving recently
        // cached rows.
        //
        // this also keeps the complex caching logic can stay out of this class and in RowLoader.
        protected TRow GetOrCreateRow(int largeOffset) =>
            RowLoader.GetOrCreateRow(largeOffset, this);

        private TRow GetOrCreateRowAtRow(int row) =>
            GetOrCreateRow(GetLargeOffsetFromRowOffset(row));

        private bool IsRowOffsetValid(int rowOffset) =>
            rowOffset >= 0 && rowOffset < RowCount;

        private bool IsLargeOffsetContainedInVisibleRows(int largeOffset) =>
            largeOffset >= startingRowLargeIndex && largeOffset <= EndingRowLargeIndex;

        protected bool IsValidLargeOffset(int largeOffset) =>
            largeOffset >= 0 && largeOffset < Items?.Count;

        public int GetRowIndexFromLargeOffset(int largeOffset) =>
            !IsLargeOffsetContainedInVisibleRows(largeOffset)
                ? -1
                : largeOffset - startingRowLargeIndex;

        protected int GetLargeOffsetFromRowOffset(int rowOffset) =>
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
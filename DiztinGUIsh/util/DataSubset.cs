using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Diz.Core.model;
using Diz.Core.util;
using DiztinGUIsh.window2;

namespace DiztinGUIsh.util
{
    // controls what rows are visible and scrolls intelligently based on our offset
    public class DataSubsetWithSelection : DataSubset
    {
        public bool AlwaysEnsureSelectionWithinVisibleRows
        {
            get => alwaysEnsureSelectionWithinVisibleRows;
            set
            {
                alwaysEnsureSelectionWithinVisibleRows = value;
                ClampSelectionIfNeeded();
            }
        }

        private void ClampSelectionIfNeeded()
        {
            if (alwaysEnsureSelectionWithinVisibleRows)
                ClampSelectionToVisibleRows();
        }

        public void ClampSelectionToVisibleRows() =>
            Util.ClampIndex(SelectedLargeIndex, StartingLargeIndexInView, EndingLargeOffsetInView);

        public int SelectedLargeIndex
        {
            get => selectedLargeIndex;
            set
            {
                if (!IsValidLargeOffset(value))
                    throw new ArgumentException("Invalid large value");

                selectedLargeIndex = value;
            }
        }

        public RomByteDataGridRow CurrentlySelectedRow =>
            GetOrCreateRow(SelectedLargeIndex);

        private int selectedLargeIndex;
        private bool alwaysEnsureSelectionWithinVisibleRows;

        public override void SetViewTo(int startIndex, int count)
        {
            base.SetViewTo(startIndex, count);
            ClampSelectionIfNeeded();
        }

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
    }

    public class DataSubset
    {
        public Data Data
        {
            get => data;
            set
            {
                data = value;
                Invalidate();
            }
        }

        // must be a subset of .Data
        public List<RomByteData> RomBytes
        {
            get => romBytes;
            set
            {
                romBytes = value;
                Invalidate();
            }
        }

        public DataSubsetLookaheadCacheLoaderBase RowLoader { get; init; }

        // rows (relative)
        public int StartingRowIndex => startingRowIndex;
        public int RowCount => rowCount;


        // large offset into all of RomBytes, doesn't care about #rows (absolute, relative to RomBytes)
        public int StartingLargeIndexInView => GetLargeOffsetFromRowOffset(StartingRowIndex);
        public int EndingLargeOffsetInView => startingRowIndex + RowCount - 1;
        
        // main idea here is, this list never changes until we scroll, in which case we drop and re-add
        // everything.  recalculating this list should never do anything that involves a lot of processing.
        // instead, we'll leave the heavy lifting to cachedRows, which can do fancier things if needed
        // like predict which rows might be needed later.
        public List<RomByteDataGridRow> Rows
        {
            get
            {
                if (cachedOutputRows.Count > 0)
                    return cachedOutputRows;

                RefreshOutputRows();

                return cachedOutputRows;
            }
        }

        private int startingRowIndex;
        private int rowCount;
        protected readonly List<RomByteDataGridRow> cachedOutputRows = new();
        private Data data;
        private List<RomByteData> romBytes;

        protected void CreateOutputRows()
        {
            Debug.Assert(cachedOutputRows.Count == 0);

            RowLoader.OnBigWindowChangeStart(this);

            for (var i = StartingRowIndex; i < StartingRowIndex + RowCount; ++i)
            {
                cachedOutputRows.Add(GetOrCreateRow(i));
            }

            RowLoader.OnBigWindowChangeFinished(this);
        }

        public virtual void SetViewTo(int startIndex, int count)
        {
            if (Data == null)
                throw new ArgumentException("Data must be set before setting view dimensions");
            
            if (RomBytes == null)
                throw new ArgumentException("RomBytes must be set before setting view dimensions");

            if (count < 0 || count > RomBytes.Count)
                throw new ArgumentException("Count out of range");

            if (startIndex < 0 || startIndex >= RomBytes.Count)
                throw new ArgumentException("Index out of range");

            // validate window range is OK.
            if (startIndex + count > RomBytes.Count)
                throw new ArgumentException("Window size is out of range");

            startingRowIndex = startIndex;
            rowCount = count;

            Invalidate();
        }

        protected void RefreshOutputRows()
        {
            Invalidate();
            CreateOutputRows();
        }

        public void Invalidate() => cachedOutputRows.Clear();

        protected RomByteDataGridRow GetOrCreateRow(int largeOffset) =>
            RowLoader.GetOrCreateRow(largeOffset, this);

        protected RomByteDataGridRow GetOrCreateRowAtRow(int row) =>
            GetOrCreateRow(GetLargeOffsetFromRowOffset(row));

        public bool IsRowOffsetValid(int rowOffset) =>
            rowOffset >= 0 && rowOffset < RowCount;

        public bool IsLargeOffsetContainedInVisibleRows(int largeOffset) =>
            largeOffset >= startingRowIndex && largeOffset <= EndingLargeOffsetInView;

        protected bool IsValidLargeOffset(int value) =>
            value >= 0 && value <= RomBytes?.Count;

        public int GetRowOffsetFromLargeOffset(int largeOffset) =>
            !IsLargeOffsetContainedInVisibleRows(largeOffset)
                ? -1
                : largeOffset - startingRowIndex;

        public int GetLargeOffsetFromRowOffset(int rowOffset) =>
            !IsRowOffsetValid(rowOffset)
                ? -1
                : rowOffset + startingRowIndex;

        public RomByteDataGridRow TryGetRow(int row) => 
            !IsRowOffsetValid(row) 
                ? null 
                : GetOrCreateRowAtRow(row);
    }
}
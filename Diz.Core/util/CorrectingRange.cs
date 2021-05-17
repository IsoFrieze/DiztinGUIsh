using System;
using System.Diagnostics;

namespace Diz.Core.util
{
    // when any parameter is set, the others will adjust to be in range (if possible)
    // i.e. if you set a starting index, the count will adjust itself to make sure the invariant holds
    public class CorrectingRange : IDataRange
    {
        public int MaxCount { get; }

        private int rangeStartIndex;
        private int rangeCount;

        public CorrectingRange(int maxCount)
        {
            MaxCount = maxCount;
        }

        public int EndIndex
        {
            // EndIndex is just a convenience helper, we'll route everything through StartIndex
            get => RangeCount == 0 
                ? -1 
                : StartIndex + RangeCount - 1;
            set => StartIndex = value - RangeCount + 1;
        }

        public int StartIndex
        {
            get => rangeStartIndex;
            set
            {
                rangeStartIndex = ClampIndex(value);
                OnStartIndexChanged();
                AssertValid();
            }
        }
        
        public int RangeCount
        {
            get => rangeCount;
            set
            {
                rangeCount = ClampCount(value);
                OnRangeCountChanged();
                AssertValid();
            }
        }

        private void OnStartIndexChanged() => UpdateRangeCountToBounds();
        private void OnRangeCountChanged() => UpdateStartIndexToBounds();

        private void UpdateRangeCountToBounds() => rangeCount = ClampCount(rangeCount);
        private void UpdateStartIndexToBounds() => rangeStartIndex = ClampIndex(rangeStartIndex);

        private int ClampCount(int count)
        { 
            if (count <= 0)
                return 0;

            if (!IsValidIndex(rangeStartIndex + count - 1)) 
                count = MaxCount - rangeStartIndex;

            Debug.Assert(Util.IsBetween(count, MaxCount));
            return count;
        }

        private bool IsValidIndex(int index) =>
            index >= 0 && index < MaxCount;
        
        private int ClampIndex(int index)
        {
            var clampedIndex = Util.ClampIndex(index, MaxCount);
            if (!IsValidIndex(clampedIndex))
                throw new ArgumentOutOfRangeException(nameof(index));
            
            return clampedIndex;
        }
        
        [Conditional("DEBUG")]
        private void AssertValid()
        {
            Debug.Assert(MaxCount >= 0);
            
            Debug.Assert(rangeCount >= 0);
            Debug.Assert(rangeStartIndex >= 0);

            if (rangeCount == 0)
            {
                Debug.Assert(EndIndex == -1);
            }
            else
            {
                Debug.Assert(EndIndex >= 0);
            }

            Debug.Assert(rangeCount <= MaxCount);
            Debug.Assert(rangeStartIndex < MaxCount);
            Debug.Assert(EndIndex < MaxCount);
        }
    }
}
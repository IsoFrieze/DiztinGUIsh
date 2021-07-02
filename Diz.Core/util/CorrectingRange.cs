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
        private int rangeEndIndex;

        public CorrectingRange(int maxCount)
        {
            MaxCount = maxCount;
        }
        
        public int StartIndex
        {
            get => rangeStartIndex;
            set
            {
                var existingCount = RangeCount;
                rangeStartIndex = ClampIndex(value);
                rangeEndIndex = ClampIndex(StartIndex + existingCount - 1);
                AssertValid();
            }
        }
        
        public int EndIndex
        {
            get => rangeEndIndex;
            set
            {
                var existingCount = RangeCount;
                rangeEndIndex = ClampIndex(value);
                rangeStartIndex = ClampIndex(rangeEndIndex + 1 - existingCount);
                AssertValid();
            }
        }

        public bool ChangeRangeCountShouldChangeEnd { get; set; } = true;
        
        public int RangeCount
        {
            get => EndIndex + 1 - StartIndex;
            set
            {
                if (ChangeRangeCountShouldChangeEnd)
                {
                    EndIndex = StartIndex + value - 1;
                }
                else
                {
                    StartIndex = EndIndex + 1 - value;
                }
            }
        }

        /*
        private int ClampCount(int count)
        { 
            if (count <= 0)
                return 0;

            if (!IsValidIndex(rangeStartIndex + count - 1)) 
                count = MaxCount - rangeStartIndex;

            Debug.Assert(Util.IsBetween(count, MaxCount));
            return count;
        }
        */

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
            
            Debug.Assert(RangeCount >= 0);
            Debug.Assert(rangeStartIndex >= 0);

            if (RangeCount == 0)
            {
                Debug.Assert(EndIndex == -1);
            }
            else
            {
                Debug.Assert(EndIndex >= 0);
            }

            Debug.Assert(RangeCount <= MaxCount);
            Debug.Assert(rangeStartIndex < MaxCount);
            Debug.Assert(EndIndex < MaxCount);
        }
    }
}
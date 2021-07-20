namespace Diz.Core
{
    public interface IDataRange
    {
        public int MaxCount { get; }
        
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public int RangeCount { get; set; }

        public void ManualUpdate(int newStartIndex, int newRangeCount);
    }
}
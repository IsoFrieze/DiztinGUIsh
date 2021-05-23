namespace Diz.Core
{
    public interface IDataRange
    {
        public int MaxCount { get; }
        
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public int RangeCount { get; set; }
    }
}
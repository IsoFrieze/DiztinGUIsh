namespace Diz.Core
{
    public interface IDizApplication
    {
        public class Args
        {
            public string FileToOpen { get; set; }
        }
        
        void Run(Args args);
        void OpenProjectFileWithNewView(string filename);
        void OpenNewViewOfLastLoadedProject();
    }
    
    public interface IDataRange
    {
        public int MaxCount { get; set; }
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public int RangeCount { get; set; }
    }
}
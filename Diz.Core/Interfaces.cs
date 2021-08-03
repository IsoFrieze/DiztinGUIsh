using System;
using System.Windows.Forms;
using Diz.Core.model;

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
    
    public interface IViewLabels
    {
        void Show();
        Project Project { get; set; }
        public event FormClosedEventHandler FormClosed;
    }
}
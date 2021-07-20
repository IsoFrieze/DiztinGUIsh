using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Diz.Core.model;
using Diz.Core.util;

namespace DiztinGUIsh.controller
{
    public class NavigationEntry
    {
        [Browsable(false)]
        public Data Data { get; }
        
        [Browsable(false)]
        public int SnesOffset { get; }
        
        public NavigationEntry(int snesOffset, ISnesNavigation.HistoryArgs historyArgs, Data data)
        {
            SnesOffset = snesOffset;
            Description = historyArgs?.Description ?? "";
            Position = historyArgs?.Position ?? "";
            
            Data = data;
        }

        [DisplayName("SNES Offset")]
        [Editable(false)]
        public string Address => Util.ToHexString6(SnesOffset);
        
        [Editable(false)]
        public string Description { get; }
        
        [Editable(false)]
        public string Position { get; }
    }
}
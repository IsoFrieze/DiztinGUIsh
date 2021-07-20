

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
        
        public NavigationEntry(int snesOffset, NavigationType type, Data data)
        {
            SnesOffset = snesOffset;
            Type = type;
            Data = data;
        }

        public enum NavigationType
        {
            NextInstruction,
            Jump,
        }
        
        [DisplayName("SNES Offset")]
        [Editable(false)]
        public string Pc => Util.ToHexString6(SnesOffset);

        [DisplayName("Type")]
        [Editable(false)]
        public NavigationType Type { get; }
    }
}
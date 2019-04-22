using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiztinGUIsh
{
    public class ROMByte
    {
        public byte Rom { get; set; }
        public byte DataBank { get; set; }
        public int DirectPage { get; set; }
        public bool XFlag { get; set; }
        public bool MFlag { get; set; }
        public Data.FlagType TypeFlag { get; set; }
        public Data.Architechture Arch { get; set; }
        public Data.InOutPoint Point { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiztinGUIsh.core;
using DiztinGUIsh.core.util;

namespace DiztinGUIsh.loadsave
{
    public class ImportRomSettings : PropertyNotifyChanged
    {
        private Data.ROMMapMode mode;
        private Data.ROMSpeed romSpeed;
        private byte[] romBytes;
        private string romFilename;

        public Data.ROMMapMode ROMMapMode
        {
            get => mode;
            set => SetField(ref mode, value);
        }

        public Data.ROMSpeed ROMSpeed
        {
            get => romSpeed;
            set => SetField(ref romSpeed, value);
        }

        // todo: add INotify stuff if we care. probably dont need to.
        public Dictionary<int, Label> InitialLabels { get; set; }
        public Dictionary<int, Data.FlagType> InitialHeaderFlags { get; set; }

        public byte[] RomBytes
        {
            get => romBytes;
            set => SetField(ref romBytes, value);
        }

        public string RomFilename
        {
            get => romFilename;
            set => SetField(ref romFilename, value);
        }
    }
}
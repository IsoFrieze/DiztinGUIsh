using System.Collections.Generic;
using System.ComponentModel;
using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Core.serialization
{
    public class ImportRomSettings : INotifyPropertyChanged
    {
        private RomMapMode mode;
        private RomSpeed romSpeed;
        private byte[] romBytes;
        private string romFilename;
        private Dictionary<int, FlagType> initialHeaderFlags = new();
        private Dictionary<int, Label> initialLabels = new();

        public RomMapMode RomMapMode
        {
            get => mode;
            set => this.SetField(PropertyChanged, ref mode, value);
        }
        
        public int RomSettingsOffset => RomUtil.GetRomSettingOffset(RomMapMode);
        
        public RomSpeed RomSpeed
        {
            get => romSpeed;
            set => this.SetField(PropertyChanged, ref romSpeed, value);
        }
        
        /// <summary>
        /// List of Rom offsets (not SNES addresses) for labels for initial vector tables/etc
        /// </summary>
        public Dictionary<int, Label> InitialLabels
        {
            get => initialLabels;
            set => this.SetField(PropertyChanged, ref initialLabels, value);
        }

        public Dictionary<int, FlagType> InitialHeaderFlags
        {
            get => initialHeaderFlags;
            set => this.SetField(PropertyChanged, ref initialHeaderFlags, value);
        }

        public byte[] RomBytes
        {
            get => romBytes;
            set => this.SetField(PropertyChanged, ref romBytes, value);
        }

        public string RomFilename
        {
            get => romFilename;
            set => this.SetField(PropertyChanged, ref romFilename, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
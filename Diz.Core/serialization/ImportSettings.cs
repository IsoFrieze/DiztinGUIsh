using System.Collections.Generic;
using System.ComponentModel;
using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Core.serialization
{
    public class ImportRomSettings : INotifyPropertyChanged
    {
        private RomMapMode mode;
        private byte[] romBytes;
        private string romFilename;

        public RomMapMode RomMapMode
        {
            get => mode;
            set => this.SetField(PropertyChanged, ref mode, value);
        }
        
        public int RomSettingsOffset => RomUtil.GetRomSettingOffset(RomMapMode);

        public RomSpeed RomSpeed => RomBytes != null ? RomUtil.GetRomSpeed(RomSettingsOffset, RomBytes) : default;

        // todo: add INotify stuff if we care. probably dont need to.
        public Dictionary<int, Label> InitialLabels { get; set; }
        public Dictionary<int, FlagType> InitialHeaderFlags { get; set; }

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

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
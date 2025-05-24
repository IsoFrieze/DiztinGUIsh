using System.Collections.Generic;
using System.ComponentModel;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Core.serialization
{
    public interface IRomImportSettings : INotifyPropertyChanged
    {
        public RomMapMode RomMapMode  { get; }
        public int RomSettingsOffset  { get; }
        public RomSpeed RomSpeed  { get; }
        public Dictionary<int, Label> InitialLabels  { get; }
        public Dictionary<int, FlagType> InitialHeaderFlags { get; }
        public IReadOnlyList<byte> RomBytes { get; }
        public string RomFilename { get; }
    } 
    
    public class ImportRomSettings : IRomImportSettings
    {
        public RomMapMode RomMapMode
        {
            get => mode;
            set => this.SetField(PropertyChanged, ref mode, value);
        }
        private RomMapMode mode;
        
        public int RomSettingsOffset => 
            RomUtil.GetRomSettingOffset(RomMapMode);
        
        public RomSpeed RomSpeed
        {
            get => romSpeed;
            set => this.SetField(PropertyChanged, ref romSpeed, value);
        }
        private RomSpeed romSpeed;
        
        /// <summary>
        /// List of Rom offsets (not SNES addresses) for labels for initial vector tables/etc
        /// TODO: actually... we probably want these to be SNES addresses after all.
        /// </summary>
        public Dictionary<int, Label> InitialLabels
        {
            get => initialLabels;
            set => this.SetField(PropertyChanged, ref initialLabels, value);
        }
        private Dictionary<int, Label> initialLabels = new();

        public Dictionary<int, FlagType> InitialHeaderFlags
        {
            get => initialHeaderFlags;
            set => this.SetField(PropertyChanged, ref initialHeaderFlags, value);
        }
        private Dictionary<int, FlagType> initialHeaderFlags = new();

        public List<byte> RomBytes
        {
            get => romBytes;
            set => this.SetField(PropertyChanged, ref romBytes, value);
        }
        private List<byte> romBytes;
        IReadOnlyList<byte> IRomImportSettings.RomBytes => romBytes;

        public string RomFilename
        {
            get => romFilename;
            set => this.SetField(PropertyChanged, ref romFilename, value);
        }
        private string romFilename;

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
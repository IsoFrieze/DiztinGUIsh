using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Diz.Core.model;
using Diz.Core.util;
using JetBrains.Annotations;

namespace Diz.Core.serialization
{
    public class ImportRomSettings : INotifyPropertyChanged
    {
        private RomMapMode mode;
        private RomSpeed romSpeed;
        private byte[] romBytes;
        private string romFilename;

        public RomMapMode RomMapMode
        {
            get => mode;
            set => this.SetField(PropertyChanged, ref mode, value);
        }

        public RomSpeed RomSpeed
        {
            get => romSpeed;
            set => this.SetField(PropertyChanged, ref romSpeed, value);
        }

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
using System.Collections.ObjectModel;
using Diz.Core.model;
using Diz.Core.util;

namespace Diz.Test.Utils
{
    public class EmptyRom : Data
    {
        public EmptyRom() : base()
        {
            RomMapMode = RomMapMode.LoRom;
            RomSpeed = RomSpeed.FastRom;

            // note: slow.
            while (RomBytes.Count < 0xFFFF * 64)
                RomBytes.Add(new RomByte());
        }
    }
}
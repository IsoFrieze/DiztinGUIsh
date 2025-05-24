using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.model.snes;

namespace Diz.Test.Utils;

public class EmptyRom : Data
{
    public EmptyRom()
    {
        RomMapMode = RomMapMode.LoRom;
        RomSpeed = RomSpeed.FastRom;

        // note: slow.
        while (RomBytes.Count < 0xFFFF * 64)
            RomBytes.Add(new RomByte());
    }
}
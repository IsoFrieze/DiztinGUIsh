#nullable enable

using Diz.Core.Interfaces;
using Diz.Core.util;

namespace Diz.Core.model.snes;

public class MemoryBus
{
    
}

// represents: main CPU, address bus, etc, on a SNES, with a particular memory mapping for the cartridge and ROM loaded
// technically should be called more like "Main SNES System" to disgintguish from SPC700 and/or SA-1, etc.
public class SnesArchitecture(Data cartRom) :
    IArchitecture,
    IRomSize,
    IRomMapProvider
{
    public int GetRomSize() => 
        cartRom.GetRomSize();

    public int GetBankSize() => 
        RomUtil.GetBankSize(RomMapMode);

    public RomMapMode RomMapMode { get; set; }
    public RomSpeed RomSpeed { get; set; }
}
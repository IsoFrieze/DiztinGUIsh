using System.Diagnostics.CodeAnalysis;

namespace Diz.Cpu._65816.import;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class SnesVectorNames
{
    // Note: All of these are valid 65816 vectors,
    // but not all are actually used by the SNES hardware.

    // Native Mode Vectors ($FFE0-$FFEF)
    public const string Native_Reserved1__ignored = "Native_Reserved1__ignored";     // $FFE0 - reserved for future use, also unused by SNES
    public const string Native_Reserved2__ignored = "Native_Reserved2__ignored";     // $FFE2 - reserved for future use, also unused by SNES
    public const string Native_COP = "Native_COP";                                   // $FFE4 - ! USED by SNES !
    public const string Native_BRK = "Native_BRK";                                   // $FFE6 - ! USED by SNES !
    public const string Native_ABORT = "Native_ABORT__ignored";                      // $FFE8 - unused by SNES
    public const string Native_NMI = "Native_NMI";                                   // $FFEA - ! USED by SNES ! - important
    public const string Native_RESET__ignored = "Native_RESET__ignored";             // $FFEC - unused by SNES - native reset vector
    public const string Native_IRQ = "Native_IRQ";                                   // $FFEE -  ! USED by SNES ! - important

    // Emulation Mode Vectors ($FFF0-$FFFF)
    public const string Emulation_Reserved1__ignored = "Emulation_Reserved1__ignored"; // $FFF0 - reserved for future use, also unused by SNES
    public const string Emulation_Reserved2__ignored = "Emulation_Reserved2__ignored"; // $FFF2 - reserved for future use, also unused by SNES
    public const string Emulation_COP = "Emulation_COP";                               // $FFF4 - unused by SNES
    public const string Emulation_Reserved3__ignored = "Emulation_Reserved3__ignored"; // $FFF6 - reserved for future use, also unused by SNES
    public const string Emulation_ABORT = "Emulation_ABORT__ignored";                  // $FFF8 - unused by SNES
    public const string Emulation_NMI = "Emulation_NMI";                               // $FFFA - unused by SNES
    public const string Emulation_RESET = "Emulation_RESET";                           // $FFFC - ! USED by SNES ! - main entry point. most ROMs only care about this one and ignore the rest of emulation vectors
    public const string Emulation_IRQBRK = "Emulation_IRQBRK";                         // $FFFE - IRQ and BRK
}
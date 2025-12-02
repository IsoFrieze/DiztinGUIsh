using Diz.Cpu._65816.import;

namespace Diz.Cpu._65816;

public static class CpuVectorTable
{
    // = FFE0 (start of vector table) minus FFD5 (start of settings after the cart title, it's the offset of the ROM map mode in the header).
    // vector table may be in different ROM locations for different rom map modes, but, it's always the same offset within the cart header.
    // in this file we only care about relative offset to the start of the tables
    public const int VectorTableSettingsOffset = 11;
    public record VectorRomEntry(int AbsoluteRomOffset, VectorTableEntry VectorTableEntry);
    public record VectorTableEntry(string Name, int VectorTableOffset);
    
    private static readonly List<VectorTableEntry> VectorTableEntries =
            // these must be kept in order, with no gaps, starting from the start of the vector table.
            // we're going to include all of the vector table entries, including the stuff that's not used by the 65816 CPU,
            // and also the stuff not used by the SNES hardware
            new List<string> {
                // Native Mode Vectors (snes $FFE0-$FFEF)
                SnesVectorNames.Native_Reserved1__ignored,
                SnesVectorNames.Native_Reserved2__ignored,
                SnesVectorNames.Native_COP,
                SnesVectorNames.Native_BRK,
                SnesVectorNames.Native_ABORT,
                SnesVectorNames.Native_NMI,
                SnesVectorNames.Native_RESET__ignored,
                SnesVectorNames.Native_IRQ,
            
                // Emulation Mode Vectors (snes $FFF0-$FFFF)
                SnesVectorNames.Emulation_Reserved1__ignored,
                SnesVectorNames.Emulation_Reserved2__ignored,
                SnesVectorNames.Emulation_COP,
                SnesVectorNames.Emulation_Reserved3__ignored,
                SnesVectorNames.Emulation_ABORT,
                SnesVectorNames.Emulation_NMI,
                SnesVectorNames.Emulation_RESET,
                SnesVectorNames.Emulation_IRQBRK
            }
                .Select((name, index) => new VectorTableEntry(
                    Name: name, 
                    // relative offset from the start of the vector table, starting from 00 and going up by 2 bytes
                    VectorTableOffset: index*2
                ))
                .ToList();
    
    public static IEnumerable<VectorRomEntry> ComputeVectorTableNamesAndOffsets(int settingsAbsoluteRomOffset)
    {
        // compute the absolute ROM address of all vector table entries (including the invalid ones)
        // settingsAbsoluteRomOffset is the ROM address of the start of the "snes settings" i.e. the value after the cart title.
        // examples: ROM offset 0xFFD5 for hirom, 0x7FD5 for lorom 
        return VectorTableEntries.Select(entry => 
            new VectorRomEntry(
                AbsoluteRomOffset: settingsAbsoluteRomOffset + VectorTableSettingsOffset + entry.VectorTableOffset, 
                VectorTableEntry: entry
            ));
    }
}
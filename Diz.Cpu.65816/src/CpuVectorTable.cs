using Diz.Cpu._65816.import;

namespace Diz.Cpu._65816;

public static class CpuVectorTable
{
    public const int VectorTableBaseOffset = 15;
    
    public record VectorTableEntry(string Name, int SettingsOffset);
    public record VectorTable(string Name, int SettingsOffset, VectorTableEntry[] Entries);

    public static readonly VectorTable[] VectorTables = 
    {
        new("Native", VectorTableBaseOffset, new []
        {
            new VectorTableEntry(SnesVectorNames.Native_COP, 0x00),
            new VectorTableEntry(SnesVectorNames.Native_BRK, 0x02),
            new VectorTableEntry(SnesVectorNames.Native_ABORT, 0x04),
            new VectorTableEntry(SnesVectorNames.Native_NMI, 0x06),
            new VectorTableEntry(SnesVectorNames.Native_RESET, 0x08),
            new VectorTableEntry(SnesVectorNames.Native_IRQ, 0x10)
        }),
        new("Emulation", VectorTableBaseOffset + 0x12, new [] // prob this is wrong, 0x12?
        {
            new VectorTableEntry(SnesVectorNames.Emulation_COP, 0x00),
            new VectorTableEntry(SnesVectorNames.Emulation_Unknown, 0x02),
            new VectorTableEntry(SnesVectorNames.Emulation_ABORT, 0x04),
            new VectorTableEntry(SnesVectorNames.Emulation_NMI, 0x06),
            new VectorTableEntry(SnesVectorNames.Emulation_RESET, 0x08),
            new VectorTableEntry(SnesVectorNames.Emulation_IRQBRK, 0x10)
        }),
    };

    public record VectorRomEntry(int AbsoluteRomOffset, VectorTableEntry Name, VectorTable ParentTable);
    
    public static IEnumerable<VectorRomEntry> ComputeVectorTableNamesAndOffsets(int settingsOffset)
    {
        return VectorTables
            .Select(table => (romOffset: settingsOffset + table.SettingsOffset, entries: table.Entries, parentTable: table))
            .SelectMany(table => table.entries
                .Select(entry => new VectorRomEntry(entry.SettingsOffset + table.romOffset, entry, table.parentTable))
            );
    }
}
using Diz.Cpu._65816.import;

namespace Diz.Cpu._65816;

public static class CpuVectorTable
{
    public const int VectorTableBaseOffset = 15;
    
    public record VectorTableEntry(string Name, int SettingsOffset);
    public record VectorTable(int SettingsOffset, VectorTableEntry[] Entries);
    
    /*
     * see:
     *   https://github.com/IsoFrieze/DiztinGUIsh/issues/74
     *   
     * TODO, pretty sure vector table below is generating incorrect output.
     *    it SHOULD be made to match this (examples from some random ROM):
     
                       // values if lowrom                                 
Native_unused_vector1:
                       dw $0000                             ;00FFE0|        |000000; unused by SNES

Native_unused_vector2:
                       dw $0000                             ;00FFE2|        |000000; unused by SNES
                                                            ;      |        |      ;
           Native_COP:
                       dw fn_NOP_rti                        ;00FFE4|        |008138; Fzero: NO-OP
                                                            ;      |        |      ;
           Native_BRK:
                       dw fn_NOP_rti                        ;00FFE6|        |008138; Fzero: NO-OP
                                                            ;      |        |      ;
  Native_ABORT_unused:
                       dw fn_NOP_rti                        ;00FFE8|        |008138; unused by SNES
                                                            ;      |        |      ;
           Native_NMI:
                       dw fn_native_NMI_routine             ;00FFEA|        |0080D9;
                                                            ;      |        |      ;
Native_unused_vector3:
                       dw PTR16_00FFFF                      ;00FFEC|        |00FFFF; unused by SNES
                                                            ;      |        |      ;
           Native_IRQ:
                       dw CODE_008601                       ;00FFEE|        |008601;
                                                            ;      |        |      ;
Emulation_unused_vector1:
                       dw $0000                             ;00FFF0|        |000000; unused by SNES
                                                            ;      |        |      ;
Emulation_unused_vector2:
                       dw $0000                             ;00FFF2|        |000000; unused by SNES
                                                            ;      |        |      ;
        Emulation_COP:
                       dw fn_NOP_rti                        ;00FFF4|        |008138; Fzero: NO-OP
                                                            ;      |        |      ;
Emulation_unused_vector3:
                       dw PTR16_00FFFF                      ;00FFF6|        |00FFFF; unused by SNES
                                                            ;      |        |      ;
      Emulation_ABORT:
                       dw fn_NOP_rti                        ;00FFF8|        |008138; unused by SNES
                                                            ;      |        |      ;
        Emulation_NMI:
                       dw fn_NOP_rti                        ;00FFFA|        |008138; Fzero: NO-OP
                                                            ;      |        |      ;
      Emulation_RESET:
                       dw cpu_startup                       ;00FFFC|        |008000; entrypoint of entire game
                                                            ;      |        |      ;
        Emulation_IRQ:
                       dw fn_NOP_rti                        ;00FFFE|        |008138; Fzero: NO-OP

     
     */

    public static readonly VectorTable[] VectorTables = 
    {
        // https://ersanio.gitbook.io/assembly-for-the-snes/deep-dives/vector
        // TODO: strong suspect some of this is wrong. see above.
        new( VectorTableBaseOffset, new []
        {
            new VectorTableEntry(SnesVectorNames.Native_COP, 0x00),
            new VectorTableEntry(SnesVectorNames.Native_BRK, 0x02),
            new VectorTableEntry(SnesVectorNames.Native_ABORT, 0x04),
            new VectorTableEntry(SnesVectorNames.Native_NMI, 0x06),
            new VectorTableEntry(SnesVectorNames.Native_RESET, 0x08),
            new VectorTableEntry(SnesVectorNames.Native_IRQ, 0x10)
        }),
        new(VectorTableBaseOffset + 0x12, new [] // prob this is wrong, 0x12?
        {
            new VectorTableEntry(SnesVectorNames.Emulation_COP, 0x00),
            new VectorTableEntry(SnesVectorNames.Emulation_Unknown, 0x02),
            new VectorTableEntry(SnesVectorNames.Emulation_ABORT, 0x04),
            new VectorTableEntry(SnesVectorNames.Emulation_NMI, 0x06),
            new VectorTableEntry(SnesVectorNames.Emulation_RESET, 0x08),
            new VectorTableEntry(SnesVectorNames.Emulation_IRQBRK, 0x10)
        }),
    };

    public record VectorRomEntry(int AbsoluteRomOffset, VectorTableEntry Entry, VectorTable ParentTable);
    
    public static IEnumerable<VectorRomEntry> ComputeVectorTableNamesAndOffsets(int settingsOffset)
    {
        return VectorTables
            .Select(table => (romOffset: settingsOffset + table.SettingsOffset, entries: table.Entries, parentTable: table))
            .SelectMany(table => table.entries
                .Select(entry => new VectorRomEntry(entry.SettingsOffset + table.romOffset, entry, table.parentTable))
            );
    }
}
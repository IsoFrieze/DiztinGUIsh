namespace Diz.Cpu._65816.import;


public interface IVectorTableCacheData
{
    public List<CpuVectorTable.VectorRomEntry>? Entries { get; }
    public int? RomSettingsOffsetUsed { get;  }   
}

public interface IVectorTableCache : IVectorTableCacheData
{
    void RegenerateEntriesFor(int romSettingsOffset);
    void Clear();
}


public class CachedVectorTableEntries : IVectorTableCache
{
    public List<CpuVectorTable.VectorRomEntry>? Entries { get; private set; }
    public int? RomSettingsOffsetUsed { get; private set; }

    public void RegenerateEntriesFor(int romSettingsOffset)
    {
        if (RomSettingsOffsetUsed == romSettingsOffset)
            return;
        
        Entries = CpuVectorTable.ComputeVectorTableNamesAndOffsets(romSettingsOffset).ToList();
        RomSettingsOffsetUsed = romSettingsOffset;
    }

    public void Clear()
    {
        Entries = null;
        RomSettingsOffsetUsed = null;
    }
}
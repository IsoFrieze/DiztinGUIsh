using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Diz.Core.Interfaces;

// how a region's bytes are emitted when exporting assembly.
// NOTE: 'Assembly' must stay the zero value. projects saved before this property existed
// will deserialize to it, which is the original behavior (inline db bytes).
public enum RegionExportType
{
    // normal: bytes are emitted inline in the .asm as `db $xx,$xx,...`
    Assembly = 0,

    // bytes are written to a sidecar .bin, and the .asm gets `incbin "<file>"` instead.
    // the .bin is the canonical copy; Diz's stored bytes become a display cache.
    Binary,

    // like Binary, but also writes an asset manifest describing how to decode the bytes
    // (e.g. gfx.snes.2bpp). an external tool (gfxpack) turns the .bin into an editable
    // PNG and back. Diz deliberately does NOT encode the PNG itself -- see AssetType.
    Asset,
}

public interface IRegion : INotifyPropertyChanged
{
    int StartSnesAddress { get; set; }
    int EndSnesAddress { get; set; }
    
    // Must be unique in this project
    string RegionName { get; set; }
    
    // ------------------------------------------------------------
    // region effects (if these get more complex, split them out)
    // ------------------------------------------------------------
    
    // labels inside this region should look for an alternative label context to apply
    // if a label has a context that matches this name, it will use THAT as the label name,
    // instead of its normal name.
    // i.e. if we define a region called BATTLE with context "Battle", and a label called "tmp50" is within this region
    // and matches the context name "Battle", it'll use the its alternate label name (say, "player_hp") here.
    // this is super-useful to help deal with different parts of the game re-using the same address for different things
    // i.e. menu vs battle vs overworld all using RAM address 0x50 for different stuff depending on which mode the game is in.
    string ContextToApply { get; set; }
    
    // higher number = higher priority = wins. its primary purpose is breaking ties between
    // overlapping regions that both set ContextToApply -- when a snes address falls inside
    // more than one region, the one with the highest Priority is used first.
    int Priority { get; set; }
    
    // if true, when exporting assembly, this region will go into a separate file.
    // overlapping regions will either be disallowed for this, or go in priority order.
    bool ExportSeparateFile { get; set; }

    // ------------------------------------------------------------
    // asset export
    // ------------------------------------------------------------

    // how this region's bytes get emitted. defaults to Assembly (inline db bytes).
    RegionExportType ExportType { get; set; }

    // for ExportType.Asset: the codec contract, e.g. "gfx.snes.2bpp".
    // this string is written verbatim into the manifest's "type" field and is what
    // tells the external tool how to interpret the bytes. Diz never decodes it itself.
    string AssetType { get; set; }

    // for ExportType.Asset: codec version, e.g. "v1". empty/null means "latest",
    // which is the default. a mismatch at build time is a hard error, never silent.
    string AssetVersion { get; set; }

    // logical asset name, used as the path within an asset layer root and as the
    // .bin/manifest filename stem. e.g. "gfx/font" resolves to
    // "assets/src/gfx/font.{bin,json}". if empty, RegionName is used.
    string AssetName { get; set; }

    // for ExportType.Asset: free-form JSON object merged into the manifest under
    // "options". empty/null (the normal case) omits the key entirely.
    //
    // a deliberate escape hatch: it lets a project author codec parameters Diz has no UI
    // for yet, without adding a field-per-knob. Diz does NOT interpret the contents -- it
    // only checks the text parses as a JSON object -- EXCEPT for "cell_h", which it must
    // read because the manifest's own tile count depends on it.
    // e.g. {"cell_h": 12}   or   {"view": {"order": "column_major", "rows": 12}}
    string AssetOptions { get; set; }
}

public interface IReadOnlyContextMapping : INotifyPropertyChanged
{
    string Context { get; }
    string NameOverride  { get; }
}

public interface IContextMapping : IReadOnlyContextMapping
{
    new string Context { get; set;  }
    new string NameOverride  { get; set; }
}


public interface IReadOnlyLabel
{
    string Name { get; }
    string Comment { get; }
    IEnumerable<IReadOnlyContextMapping> ContextMappings { get; }

}

public interface IAnnotationLabel : IReadOnlyLabel
{
    // name used for default context
    new string Name { get; set; }
    new string Comment { get; set; }
    
    // label names can change based on which "context" they're in
    // by default, this is empty but can be overridden
    new ObservableCollection<IContextMapping> ContextMappings { get; }
    
    // get a label name using a specific context, if it exists. otherwise return the default name
    string GetName(string contextName = "");
}
    
// what happened to the label set. lets consumers do a targeted update instead of a full rebuild.
public enum LabelChangeKind
{
    Added,
    Removed,
    Replaced,
    BulkReset,
}

public sealed class LabelChangedEventArgs : EventArgs
{
    public required LabelChangeKind Kind { get; init; }

    // the address affected. -1 for BulkReset (the whole set changed).
    public int SnesAddress { get; init; }
}

public interface IReadOnlyLabelProvider
{
    public IEnumerable<KeyValuePair<int, IAnnotationLabel>> Labels { get; }

    // payloaded change notification. observing is a read-side concern, so it lives here
    // rather than on ILabelProvider: consumers that only ever reach labels through
    // IReadOnlyLabels.Labels (SnesData, CPU65C816, ILogCreatorDataSource) still need it.
    //
    // this is ADDITIVE. the older payload-less LabelsServiceWithTemp.OnLabelChanged still
    // fires exactly as before, at the same sites, for every existing subscriber.
    event EventHandler<LabelChangedEventArgs> LabelsChanged;

    IAnnotationLabel? GetLabel(int snesAddress);
    string? GetLabelName(int snesAddress);
    string GetLabelComment(int snesAddress);
    
    // optimization: optional: get a provider that can give a smaller subset
    // of labels for assembly logging output, to reduce search space and improve export speed.
    public IMirroredLabelCacheSearch? MirroredLabelCacheSearch { get; }
}

public interface IMirroredLabelCacheSearch
{
    (int labelAddress, IAnnotationLabel? labelEntry) SearchOptimizedForMirroredLabel(int snesAddress);
}

public interface ILabelProvider : IReadOnlyLabelProvider
{
    void AddLabel(int snesAddress, IAnnotationLabel label, bool overwrite = false);
    void DeleteAllLabels();
        
    // if any labels exist at this address, remove them
    void RemoveLabel(int snesAddress);
    
    void SetAll(Dictionary<int, IAnnotationLabel> newLabels);
    void AppendLabels(Dictionary<int, IAnnotationLabel> newLabels, bool smartMerge = false);
    
    void SortLabels();
}

public interface IReadOnlyLabels
{
    public IReadOnlyLabelProvider Labels { get; }
}
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Diz.Core.Interfaces;

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
    
    // higher number = higher priority in case of overlapping regions
    int Priority { get; set; } 
    
    // if true, when exporting assembly, this region will go into a separate file.
    // overlapping regions will either be disallowed for this, or go in priority order.
    bool ExportSeparateFile { get; set; }
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
    
public interface IReadOnlyLabelProvider
{
    public IEnumerable<KeyValuePair<int, IAnnotationLabel>> Labels { get; }

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
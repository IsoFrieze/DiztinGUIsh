using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Diz.Core.Interfaces;

public interface IRegion : INotifyPropertyChanged
{
    int StartSnesAddress { get; set; }
    int EndSnesAddress { get; set; }
    string RegionName { get; set; }
    
    
    // region effects (if these get more complex, split them out)
    string ContextToApply { get; set; }
    int Priority { get; set; } // higher number = higher priority in case of overlapping regions
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
}

public interface IReadOnlyLabels
{
    public IReadOnlyLabelProvider Labels { get; }
}
namespace Diz.Core.Interfaces;

public interface IReadOnlyLabel
{
    string Name { get; }
    string Comment { get; }
}

public interface IAnnotationLabel : IReadOnlyLabel
{
    new string Name { get; set; }
    new string Comment { get; set; }
}
    
public interface IReadOnlyLabelProvider
{
    public IEnumerable<KeyValuePair<int, IAnnotationLabel>> Labels { get; }

    IAnnotationLabel? GetLabel(int snesAddress);
    string GetLabelName(int snesAddress);
    string GetLabelComment(int snesAddress);
    
    // optimization: optional: get a provider that can give a smaller subset
    // of labels for assembly logging output, to reduce search space and improve export speed.
    public IExporterCache? ExporterCache { get; }
}

public interface IExporterCache
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
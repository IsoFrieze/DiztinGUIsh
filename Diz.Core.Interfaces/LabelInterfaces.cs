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
}
    
public interface ILabelProvider // : IReadOnlyLabelProvider [if you want, in the future]
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
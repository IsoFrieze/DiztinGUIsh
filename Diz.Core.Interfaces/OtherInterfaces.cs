using System.Collections.ObjectModel;

namespace Diz.Core.Interfaces;

public interface IRegionProvider
{
    ObservableCollection<IRegion> Regions { get; }
    IRegion? GetRegion(int snesAddress);

    // create a new region (doesn't add it to collection)
    IRegion? CreateNewRegion();
}

public interface ICommentTextProvider
{
    // search both ROM comments and applicable label comments
    string GetCommentText(int snesAddress);
    
    // search just ROM comments
    string? GetComment(int snesAddress);
}

#if DIZ_3_BRANCH
    public interface IAnnotationProvider
    {
        public T GetOneAnnotationAtPc<T>(int pcOffset) where T : Annotation, new();   
}

    public interface IByteGraphProvider
    {
        ByteEntry BuildFlatByteEntryForSnes(int snesAddress);
        ByteEntry BuildFlatByteEntryForRom(int snesAddress);
    }
#endif

// utility for getting info about the running app
public interface IAppVersionInfo
{
    enum AppVersionInfoType
    {
        Version,
        FullDescription,
    }
    
    string GetVersionInfo(AppVersionInfoType type);
}
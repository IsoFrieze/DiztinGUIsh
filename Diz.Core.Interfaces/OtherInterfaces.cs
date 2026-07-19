using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Diz.Core.Interfaces;

public interface IRegionProvider
{
    ObservableCollection<IRegion> Regions { get; }
    IRegion? GetRegion(int snesAddress);

    // every region covering snesAddress, ordered most-specific-first (narrowest extent
    // first, Priority descending as tiebreak). NOTE this is a LIST, not a chain: annotation
    // regions may partially overlap each other, so the result is not guaranteed to nest.
    IReadOnlyList<IRegion> GetRegionPath(int snesAddress);

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
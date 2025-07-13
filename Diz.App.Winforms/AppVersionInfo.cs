using System.Diagnostics.CodeAnalysis;
using Diz.Core.Interfaces;
using Diz.Core.serialization.xml_serializer;

namespace Diz.App.Winforms;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class AppVersionInfo : IAppVersionInfo
{
    private readonly string versionStr;
    private readonly string fullBuildDescription;
    
    public AppVersionInfo()
    {
        // cache these, they won't change
        versionStr = ThisAssembly.Git.Tag;
        fullBuildDescription =
            $"Build info:\r\n------------\r\n" +
            $"Version: {versionStr}\r\n" +
            $"Git branch: {ThisAssembly.Git.Branch}\r\n" +
            $"Git commit: {ThisAssembly.Git.Commit}\r\n" +
            $"Git repo URL: {ThisAssembly.Git.RepositoryUrl}\r\n" +
            $"Git tag: {ThisAssembly.Git.Tag}\r\n" +
            $"Git last commit date: {ThisAssembly.Git.CommitDate}\r\n" +
            $"Git IsDirty: {ThisAssembly.Git.IsDirtyString}\r\n" +
            $"Git Commits on top of base: {ThisAssembly.Git.Commits}\r\n" +
            "\r\n\r\n" +
            $"Diz app savefile format ver: {ProjectXmlSerializer.LatestSaveFormatVerion}";
    }
    
    [SuppressMessage("ReSharper", "HeuristicUnreachableCode")]
    public string GetVersionInfo(IAppVersionInfo.AppVersionInfoType type)
    {
        return type switch
        {
            IAppVersionInfo.AppVersionInfoType.FullDescription => fullBuildDescription,
            IAppVersionInfo.AppVersionInfoType.Version => versionStr,
            _ => ""
        };
    }
}
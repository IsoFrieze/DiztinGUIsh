using Diz.Core.Interfaces;
using Diz.Core.serialization.xml_serializer;

namespace Diz.App.Winforms;

public class AppVersionInfo : IAppVersionInfo
{
    public string GetVersionInfo(IAppVersionInfo.AppVersionInfoType type)
    {
        var versionStr = ThisAssembly.Git.Tag;
        if (string.IsNullOrWhiteSpace(versionStr))
        {
            versionStr = $"commit-{ThisAssembly.Git.Commit}";
        }

        if (ThisAssembly.Git.IsDirty)
            versionStr += "-dirty";
        
        return type switch
        {
            IAppVersionInfo.AppVersionInfoType.FullDescription =>
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
                $"Diz app savefile format ver: {ProjectXmlSerializer.LatestSaveFormatVerion}",
            IAppVersionInfo.AppVersionInfoType.Version =>
                $"{versionStr}",
            _ => ""
        };
    }
}
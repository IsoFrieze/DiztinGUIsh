using System.Diagnostics;
using Diz.Core.model;
using Diz.Core.serialization;
using Diz.Test.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Diz.Test;

public class LoadSavePerformanceTests : ContainerFixture
{
    private readonly IProjectFileManager projectFileManager = null!;
    private readonly IProjectXmlSerializer projectXmlSerializer = null!;
    
    private readonly ITestOutputHelper output;
    public LoadSavePerformanceTests(ITestOutputHelper output)
    {
        this.output = output;
    }
    
    private static Project OpenProject(string openFile, IProjectFileManager projectFileManager)
    {
        var projectOpenResult = projectFileManager.Open(openFile);

        Assert.Equal("", projectOpenResult.OpenResult.Warning);
        var project = projectOpenResult.Root.Project;
        Assert.True(project.Data.RomBytes.Count >= 0x1000 * 64);
            
        return project;
    }

    [Fact(Skip = "Performance Test")]
    private void OpenFilePerformanceTest()
    {
        var s = Stopwatch.StartNew();
        s.Start();
            
        const string openFile = "INSERT YOUR FILE HERE BEFORE RUNNING THIS TEST.dizraw";
        var project = OpenProject(openFile, projectFileManager);

        s.Stop();

        output.WriteLine($"runtime: {s.ElapsedMilliseconds:N0}, #bytes={project.Data.RomBytes.Count}");
    }


    [Fact(Skip = "Performance Test")]
    private void SaveFilePerformanceTest()
    {
        const string openFile = "INSERT YOUR FILE HERE BEFORE RUNNING THIS TEST.dizraw";
        var project = OpenProject(openFile, projectFileManager);
            
        var s = Stopwatch.StartNew();
        s.Start();

        var data = projectXmlSerializer.Save(project);

        s.Stop();
            
        Assert.True(data.Length != 0);

        output.WriteLine($"runtime: {s.ElapsedMilliseconds:N0}");
    }
}
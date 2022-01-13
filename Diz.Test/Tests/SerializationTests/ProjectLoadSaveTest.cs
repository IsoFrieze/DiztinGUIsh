using System;
using System.Diagnostics;
using System.Linq;
using Diz.Core.model;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using Diz.Cpu._65816;
using Diz.Test.Tests;
using FluentAssertions;
using LightInject.xUnit2;
using Xunit;
using Xunit.Abstractions;

namespace Diz.Test;

public class LoadSaveTest
{
    [Theory, InjectData]
    private void FullSerializeAndDeserialize(IProjectXmlSerializer serializer, ISnesSampleProjectFactory sampleProjectCreator)
    {
        var srcProject = sampleProjectCreator.Create() as Project;
            
        var expectedTitle = SnesSampleRomDataFactory.GetSampleUtf8CartridgeTitle();

        srcProject.Data.Comments.Count.Should().BeGreaterThan(0);
        srcProject.Data.Labels.Labels.Count().Should().BeGreaterThan(0);

        // extract the bytes that would normally be in the SMC file (they only exist in code for this sample data)
        var romFileBytes = srcProject.Data.GetFileBytes().ToList();

        // save it to create an output byte stream, we'd normally write this to the disk
        
        var outputBytes = serializer.Save(srcProject);

        // now do the reverse and load our output back as the input
        var projectOpenInfo = serializer.Load(outputBytes);
        var deserializedRoot = projectOpenInfo.Root;
        var warning = projectOpenInfo.OpenResult.Warning;
            
        // final step, the loading process doesn't save the actual SMC file bytes, so we do it ourselves here
        deserializedRoot.Project.Data.RomBytes.CopyRomDataIn(romFileBytes);

        // now we can do a full compare between the original project, and the project which has been cycled through
        // serialization and deserialization
        warning.Should().Be("");
        deserializedRoot.Project.Data.Labels.Labels.Count().Should().Be(srcProject.Data.Labels.Labels.Count());

        void TestEquivalent(Func<Project, object> func, ProjectSerializedRoot root, Project project) => 
            func(root.Project).Should().BeEquivalentTo(func(project));

        TestEquivalent(x => x.Data.RomBytes, deserializedRoot, srcProject);
        TestEquivalent(x => x.Data.Comments, deserializedRoot, srcProject);
        TestEquivalent(x => x.Data.Labels, deserializedRoot, srcProject);

        deserializedRoot.Project.Should().BeEquivalentTo(srcProject);

        deserializedRoot.Project.Data.Comments.Count.Should().BePositive();
        deserializedRoot.Project.Data.Labels.Labels.Count().Should().BePositive();

        CartNameTests.TestRomCartTitle(deserializedRoot.Project, expectedTitle);
    }

    private readonly ITestOutputHelper output;

    public LoadSaveTest(ITestOutputHelper output)
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

    [Theory(Skip = "Performance Test"), InjectData]
    private void OpenFilePerformanceTest(IProjectFileManager projectFileManager)
    {
        var s = Stopwatch.StartNew();
        s.Start();
            
        const string openFile = "INSERT YOUR FILE HERE BEFORE RUNNING THIS TEST.dizraw";
        var project = OpenProject(openFile, projectFileManager);

        s.Stop();

        output.WriteLine($"runtime: {s.ElapsedMilliseconds:N0}, #bytes={project.Data.RomBytes.Count}");
    }


    [Theory(Skip = "Performance Test"), InjectData]
    private void SaveFilePerformanceTest(IProjectFileManager projectFileManager, IProjectXmlSerializer projectXmlSerializer)
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
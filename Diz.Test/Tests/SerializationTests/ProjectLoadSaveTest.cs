using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using Diz.Core;
using Diz.Core.model;
using Diz.Core.model.snes;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using Diz.Core.util;
using Diz.Cpu._65816;
using Diz.Test.Tests;
using Diz.Test.Utils;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Diz.Test;

public class LoadSaveTest
{
    [Fact]
    private void FullSerializeAndDeserialize()
    {
        // use the sample data to fake a project
        var srcProject = BuildSampleProject2();
            
        var expectedTitle = SampleRomData.GetSampleUtf8CartridgeTitle();

        srcProject.Data.Comments.Count.Should().BeGreaterThan(0);
        srcProject.Data.Labels.Labels.Count().Should().BeGreaterThan(0);

        // extract the bytes that would normally be in the SMC file (they only exist in code for this sample data)
        var romFileBytes = srcProject.Data.GetFileBytes().ToList();

        // save it to create an output byte stream, we'd normally write this to the disk
        var serializer = new ProjectXmlSerializer();
        var outputBytes = serializer.Save(srcProject);

        // now do the reverse and load our output back as the input
        var (deserializedRoot, warning) = serializer.Load(outputBytes);
            
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

    public static Project BuildSampleProject2()
    {
        var project2 = new Project
        {
            Data = SampleRomData.CreateSampleData().data,
        };

        project2.Data.GetSnesApi().CacheVerificationInfoFor(project2);

        return project2;
    }

    private readonly ITestOutputHelper output;

    public LoadSaveTest(ITestOutputHelper output)
    {
        this.output = output;
        
        AppServicesForTests.RegisterNormalAppServices();
    }
        
    public static Project OpenProject(string openFile)
    {
        var projectFileManager = new ProjectFileManager();
        var (project, warning) = projectFileManager.Open(openFile);

        Assert.Equal("", warning);
        Assert.True(project.Data.RomBytes.Count >= 0x1000 * 64);
            
        return project;
    }

    [Fact(Skip = "Performance Test")]
    private void OpenFilePerformanceTest()
    {
        var s = Stopwatch.StartNew();
        s.Start();
            
        var openFile = "INSERT YOUR FILE HERE BEFORE RUNNING THIS TEST.dizraw";
        var project = OpenProject(openFile);

        s.Stop();

        output.WriteLine($"runtime: {s.ElapsedMilliseconds:N0}, #bytes={project.Data.RomBytes.Count}");
    }


    [Fact(Skip = "Performance Test")]
    private void SaveFilePerformanceTest()
    {
        var openFile = "INSERT YOUR FILE HERE BEFORE RUNNING THIS TEST.dizraw";
        var project = OpenProject(openFile);
            
        var s = Stopwatch.StartNew();
        s.Start();

        var data = new ProjectXmlSerializer().Save(project);

        s.Stop();
            
        Assert.True(data.Length != 0);

        output.WriteLine($"runtime: {s.ElapsedMilliseconds:N0}");
    }
}
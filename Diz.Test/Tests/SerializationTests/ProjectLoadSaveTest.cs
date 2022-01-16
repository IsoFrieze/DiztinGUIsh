using System;
using System.Linq;
using Diz.Core.model;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using Diz.Cpu._65816;
using Diz.Test.Tests;
using Diz.Test.Tests.RomInterfaceTests;
using Diz.Test.Utils;
using FluentAssertions;
using Xunit;

namespace Diz.Test;

public class LoadSaveTest : ContainerFixture
{
    [Inject] private readonly IProjectXmlSerializer serializer = null!;
    [Inject] private readonly ISnesSampleProjectFactory sampleProjectCreator = null!;

    [Fact]
    public void FullSerializeAndDeserialize()
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
        warning.Should().Be(null);
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
}
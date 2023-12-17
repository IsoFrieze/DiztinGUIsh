using System;
using System.Linq;
using Diz.Core.model;
using Diz.Core.serialization;
using Diz.Core.serialization.xml_serializer;
using Diz.Cpu._65816;
using Diz.Test.Tests.RomInterfaceTests;
using Diz.Test.Utils;
using FluentAssertions;
using Xunit;

namespace Diz.Test.Tests.SerializationTests;

public class LoadSaveTest : ContainerFixture
{
    [Inject] private readonly IProjectXmlSerializer serializer = null!;
    [Inject] private readonly ISnesSampleProjectFactory sampleProjectCreator = null!;

    [Fact]
    public void FullSerializeAndDeserialize()
    {
        var srcProject = sampleProjectCreator.Create() as Project ?? throw new Exception("can't create sample project data");
            
        var expectedTitle = SnesSampleRomDataFactory.GetSampleUtf8CartridgeTitle();

        srcProject.Data.Comments.Count.Should().BePositive();
        srcProject.Data.Labels.Labels.Count().Should().BePositive();

        // extract the bytes that would normally be in the SMC file (they only exist in code for this sample data)
        var romFileBytes = srcProject.Data.GetFileBytes().ToList();

        // save it to create an output byte stream, we'd normally write this to the disk
        
        var outputBytes = serializer.Save(srcProject);

        // now do the reverse and load our output back as the input
        var projectOpenInfo = serializer.Load(outputBytes);
        var deserializedRoot = projectOpenInfo.Root;
        var warning = projectOpenInfo.OpenResult.Warnings;
            
        // final step, the loading process doesn't save the actual SMC file bytes, so we do it ourselves here
        deserializedRoot.Project.Data.RomBytes.CopyRomDataIn(romFileBytes);

        // now we can do a full compare between the original project, and the project which has been cycled through
        // serialization and deserialization
        warning.Should().BeEmpty("there should be no warnings");
        deserializedRoot.Project.Data.Labels.Labels.Count().Should().Be(srcProject.Data.Labels.Labels.Count());

        TestEquivalent(x => x.Data.RomBytes, deserializedRoot, srcProject);
        TestEquivalent(x => x.Data.Comments, deserializedRoot, srcProject);
        TestEquivalent(x => x.Data.Labels, deserializedRoot, srcProject);

        deserializedRoot.Project.Should().BeEquivalentTo(srcProject);

        deserializedRoot.Project.Data.Comments.Count.Should().BePositive();
        deserializedRoot.Project.Data.Labels.Labels.Count().Should().BePositive();

        CartNameTests.TestRomCartTitle(deserializedRoot.Project, expectedTitle);
    }

    private static void TestEquivalent(Func<Project, object> func, ProjectXmlSerializer.Root root, Project project) => 
        func(root.Project).Should().BeEquivalentTo(func(project));
}